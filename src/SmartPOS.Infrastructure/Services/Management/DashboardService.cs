using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class DashboardService(IServiceScopeFactory scopeFactory, IDateTimeProvider clock, IStoreContext storeContext) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await GetSummaryCoreAsync(db, ct);
    }

    public async Task<NavStatsDto> GetNavStatsAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await GetNavStatsCoreAsync(db, ct);
    }

    public async Task<IReadOnlyList<GlobalSearchResultDto>> SearchAsync(string query, int take = 12, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await SearchCoreAsync(db, query, take, ct);
    }

    private async Task<DashboardSummaryDto> GetSummaryCoreAsync(AppDbContext db, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var todayUtc = now.Date;
        var weekStart = todayUtc.AddDays(-6);
        var selectedStoreId = storeContext.StoreId;

        var productQuery = db.Products.AsNoTracking().AsQueryable();
        var stockQuery = db.Stocks.AsNoTracking().AsQueryable();
        var salesQuery = db.Sales.AsNoTracking().AsQueryable();
        var ledgerQuery = db.StockLedgers.AsNoTracking().AsQueryable();
        var storeQuery = db.Stores.AsNoTracking().Where(s => s.IsActive);
        var registerQuery = db.Registers.AsNoTracking().Where(r => r.IsActive);

        if (selectedStoreId is { } storeId)
        {
            stockQuery = stockQuery.Where(s => s.StoreId == storeId);
            salesQuery = salesQuery.Where(s => s.StoreId == storeId);
            ledgerQuery = ledgerQuery.Where(l => l.StoreId == storeId);
            storeQuery = storeQuery.Where(s => s.Id == storeId);
            registerQuery = registerQuery.Where(r => r.StoreId == storeId);
        }

        var productCount = selectedStoreId is null
            ? await productQuery.CountAsync(ct)
            : await stockQuery.Select(s => s.ProductId).Distinct().CountAsync(ct);
        var activeProductCount = selectedStoreId is null
            ? await productQuery.CountAsync(p => p.IsActive, ct)
            : await stockQuery.Where(s => s.Product != null && s.Product.IsActive).Select(s => s.ProductId).Distinct().CountAsync(ct);
        var storeCount = await storeQuery.CountAsync(ct);
        var registerCount = await registerQuery.CountAsync(ct);
        var movementsToday = await ledgerQuery.CountAsync(l => l.CreatedOn >= todayUtc, ct);

        var storeLookup = await db.Stores.AsNoTracking()
            .Select(s => new { s.Id, Name = s.Code + " · " + s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var weekSales = await salesQuery
            .Where(s => s.Status == SaleStatus.Completed && s.CompletedOn >= weekStart)
            .Select(s => new
            {
                s.Id,
                s.StoreId,
                s.TotalAmount,
                s.DiscountAmount,
                s.TaxAmount,
                s.CompletedOn
            })
            .ToListAsync(ct);

        var saleIds = weekSales.Select(s => s.Id).ToList();

        var saleLines = await db.SaleLines.AsNoTracking()
            .Where(l => saleIds.Contains(l.SaleId))
            .Select(l => new
            {
                l.SaleId,
                l.Quantity,
                l.CostPrice,
                l.TaxAmount,
                l.LineTotal
            })
            .ToListAsync(ct);

        var profitBySale = saleLines
            .GroupBy(l => l.SaleId)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(g.Sum(l => (l.LineTotal - l.TaxAmount) - (l.CostPrice * l.Quantity)), 2));

        var todaySales = weekSales.Where(s => s.CompletedOn.Date == todayUtc).ToList();
        var todaySalesTotal = todaySales.Sum(s => s.TotalAmount);
        var todayTxCount = todaySales.Count;
        var todayProfit = todaySales.Sum(s => profitBySale.GetValueOrDefault(s.Id));
        var todayDiscounts = todaySales.Sum(s => s.DiscountAmount);
        var todayMargin = todaySalesTotal <= 0 ? 0 : Math.Round(todayProfit / todaySalesTotal * 100m, 1);
        var averageBasket = todayTxCount == 0 ? 0 : Math.Round(todaySalesTotal / todayTxCount, 2);

        var salesTrend = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                var daySales = weekSales.Where(s => s.CompletedOn.Date == day).ToList();
                return new DashboardTrendPointDto
                {
                    Label = day.ToString("ddd"),
                    Count = daySales.Count,
                    Amount = daySales.Sum(s => s.TotalAmount),
                    Profit = daySales.Sum(s => profitBySale.GetValueOrDefault(s.Id))
                };
            })
            .ToList();

        var stockRows = await stockQuery
            .Include(s => s.Product)
            .Include(s => s.Store)
            .Where(s => s.Product != null)
            .Select(s => new
            {
                s.QuantityOnHand,
                ReorderLevel = s.Product!.ReorderLevel,
                SellPrice = s.Product.SellPrice,
                StoreName = s.Store != null ? s.Store.Code + " · " + s.Store.Name : string.Empty,
                ProductSku = s.Product.Sku,
                ProductName = s.Product.Name,
                s.StoreId
            })
            .ToListAsync(ct);

        var lowStock = stockRows
            .Where(s => s.ReorderLevel > 0 && s.QuantityOnHand <= s.ReorderLevel)
            .ToList();

        var critical = lowStock
            .Where(s => s.QuantityOnHand <= 0 || s.QuantityOnHand <= s.ReorderLevel * 0.25m)
            .ToList();

        var retailValue = stockRows.Sum(s => s.QuantityOnHand * s.SellPrice);

        var alerts = lowStock
            .OrderBy(s => s.QuantityOnHand)
            .Take(8)
            .Select(s => new DashboardAlertDto
            {
                Priority = s.QuantityOnHand <= 0 ? "Critical" : "Warning",
                Message = $"{s.ProductSku} · {s.ProductName} — {s.QuantityOnHand:0.###} on hand (reorder {s.ReorderLevel:0.###})",
                Store = s.StoreName
            })
            .ToList();

        var storeSnapshots = stockRows
            .GroupBy(s => new { s.StoreId, s.StoreName })
            .Select(g => new DashboardStoreSnapshotDto
            {
                StoreId = g.Key.StoreId,
                StoreName = g.Key.StoreName,
                RetailValue = g.Sum(x => x.QuantityOnHand * x.SellPrice),
                LowStockItems = g.Count(x => x.ReorderLevel > 0 && x.QuantityOnHand <= x.ReorderLevel)
            })
            .OrderByDescending(s => s.RetailValue)
            .ToList();

        var movementTrendRaw = await ledgerQuery
            .Where(l => l.CreatedOn >= weekStart)
            .GroupBy(l => l.StoreId)
            .Select(g => new { StoreId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var snapshot in storeSnapshots)
        {
            snapshot.MovementsLast7Days = movementTrendRaw
                .FirstOrDefault(m => m.StoreId == snapshot.StoreId)?.Count ?? 0;
        }

        var stockByStore = storeSnapshots.ToDictionary(s => s.StoreId, s => s);

        var storePerformance = weekSales
            .GroupBy(s => s.StoreId)
            .Select(g =>
            {
                var netSales = g.Sum(x => x.TotalAmount);
                var profit = g.Sum(x => profitBySale.GetValueOrDefault(x.Id));
                stockByStore.TryGetValue(g.Key, out var stock);
                return new DashboardStorePerformanceDto
                {
                    StoreId = g.Key,
                    StoreName = storeLookup.GetValueOrDefault(g.Key, stock?.StoreName ?? "Unknown store"),
                    NetSales = netSales,
                    GrossProfit = profit,
                    MarginPercent = netSales <= 0 ? 0 : Math.Round(profit / netSales * 100m, 1),
                    Transactions = g.Count(),
                    InventoryValue = stock?.RetailValue ?? 0,
                    LowStockItems = stock?.LowStockItems ?? 0
                };
            })
            .OrderByDescending(s => s.NetSales)
            .ToList();

        // Show stores with stock but no sales too, so the owner can still see idle outlets.
        foreach (var stock in storeSnapshots.Where(s => storePerformance.All(p => p.StoreId != s.StoreId)))
        {
            storePerformance.Add(new DashboardStorePerformanceDto
            {
                StoreId = stock.StoreId,
                StoreName = stock.StoreName,
                InventoryValue = stock.RetailValue,
                LowStockItems = stock.LowStockItems
            });
        }

        var payments = await db.Payments.AsNoTracking()
            .Where(p => saleIds.Contains(p.SaleId))
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync(ct);

        var paymentTotal = payments.Sum(p => p.Amount);
        var paymentMix = payments
            .GroupBy(p => p.Method.ToString())
            .Select(g => new DashboardPaymentMixDto
            {
                Method = g.Key,
                Amount = g.Sum(x => x.Amount),
                Count = g.Count(),
                SharePercent = paymentTotal <= 0 ? 0 : Math.Round(g.Sum(x => x.Amount) / paymentTotal * 100m, 1)
            })
            .OrderByDescending(p => p.Amount)
            .ToList();

        return new DashboardSummaryDto
        {
            ProductCount = productCount,
            ActiveProductCount = activeProductCount,
            LowStockCount = lowStock.Count,
            CriticalStockCount = critical.Count,
            InventoryRetailValue = retailValue,
            MovementsToday = movementsToday,
            StoreCount = storeCount,
            RegisterCount = registerCount,
            SalesLive = true,
            TodaySales = todaySalesTotal,
            TodayTransactions = todayTxCount,
            TodayProfit = todayProfit,
            TodayMarginPercent = todayMargin,
            TodayDiscounts = todayDiscounts,
            AverageBasket = averageBasket,
            Alerts = alerts,
            StoreSnapshots = storeSnapshots,
            ActivityTrend = salesTrend,
            StorePerformance = storePerformance,
            PaymentMix = paymentMix
        };
    }

    private async Task<NavStatsDto> GetNavStatsCoreAsync(AppDbContext db, CancellationToken ct)
    {
        var query = db.Stocks.AsNoTracking().Include(s => s.Product).AsQueryable();

        if (storeContext.StoreId is { } storeId)
        {
            query = query.Where(s => s.StoreId == storeId);
        }

        var count = await query
            .CountAsync(s => s.Product != null && s.Product.ReorderLevel > 0 && s.QuantityOnHand <= s.Product.ReorderLevel, ct);

        return new NavStatsDto { LowStockCount = count };
    }

    private async Task<IReadOnlyList<GlobalSearchResultDto>> SearchCoreAsync(AppDbContext db, string query, int take, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var term = query.Trim();
        var like = $"%{term}%";
        var results = new List<GlobalSearchResultDto>();

        // 1. Navigation / pages — instant jumps to any module.
        results.AddRange(NavTargets
            .Where(n => n.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || n.Keywords.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Select(n => new GlobalSearchResultDto
            {
                Kind = "Page",
                Title = n.Title,
                Subtitle = "Go to page",
                Path = n.Path,
                Icon = n.Icon
            }));

        // 2. Products (the bulk of what cashiers look for).
        var products = await db.Products.AsNoTracking()
            .Where(p =>
                EF.Functions.Like(p.Name, like) ||
                EF.Functions.Like(p.Sku, like) ||
                (p.Barcode != null && EF.Functions.Like(p.Barcode, like)))
            .OrderBy(p => p.Name)
            .Take(take)
            .Select(p => new GlobalSearchResultDto
            {
                Kind = "Product",
                Title = p.Name,
                Subtitle = p.Sku,
                Path = "/products",
                Icon = "inventory_2"
            })
            .ToListAsync(ct);
        results.AddRange(products);

        // 3. Categories.
        var categories = await db.Categories.AsNoTracking()
            .Where(c => EF.Functions.Like(c.Name, like))
            .OrderBy(c => c.Name)
            .Take(5)
            .Select(c => new GlobalSearchResultDto
            {
                Kind = "Category",
                Title = c.Name,
                Subtitle = "Catalog category",
                Path = "/catalog/setup",
                Icon = "category"
            })
            .ToListAsync(ct);
        results.AddRange(categories);

        // 4. Brands.
        var brands = await db.Brands.AsNoTracking()
            .Where(b => EF.Functions.Like(b.Name, like))
            .OrderBy(b => b.Name)
            .Take(5)
            .Select(b => new GlobalSearchResultDto
            {
                Kind = "Brand",
                Title = b.Name,
                Subtitle = "Catalog brand",
                Path = "/catalog/setup",
                Icon = "sell"
            })
            .ToListAsync(ct);
        results.AddRange(brands);

        // 5. Stores.
        var stores = await db.Stores.AsNoTracking()
            .Where(s => s.IsActive && (
                EF.Functions.Like(s.Name, like) ||
                EF.Functions.Like(s.Code, like)))
            .OrderBy(s => s.Name)
            .Take(5)
            .Select(s => new GlobalSearchResultDto
            {
                Kind = "Store",
                Title = s.Name,
                Subtitle = s.Code,
                Path = "/setup/stores",
                Icon = "store"
            })
            .ToListAsync(ct);
        results.AddRange(stores);

        return results.Take(take).ToList();
    }

    private sealed record NavTarget(string Title, string Path, string Icon, string Keywords);

    private static readonly NavTarget[] NavTargets =
    [
        new("Dashboard",        "/dashboard",        "dashboard",            "home overview kpi"),
        new("Point of Sale",    "/pos",              "point_of_sale",        "pos sell billing cashier checkout"),
        new("Sales & Returns",  "/sales",            "receipt_long",         "sales invoices receipts returns refunds history"),
        new("Products",         "/products",         "inventory_2",          "catalog items sku"),
        new("Catalog Setup",    "/catalog/setup",    "tune",                 "categories brands units tax"),
        new("Inventory",        "/inventory",        "warehouse",            "stock on hand adjustments ledger"),
        new("Customers",        "/customers",        "groups",               "clients buyers"),
        new("Suppliers",        "/suppliers",        "handshake",            "vendors"),
        new("Reports",          "/reports",          "assessment",           "analytics insights"),
        new("Company",          "/setup/company",    "business",             "settings profile"),
        new("Stores",           "/setup/stores",     "store",                "branches outlets"),
        new("Registers",        "/setup/registers",  "desktop_windows",      "terminals tills"),
        new("Users",            "/setup/users",      "manage_accounts",      "staff employees accounts"),
        new("Roles & Permissions","/setup/roles",    "admin_panel_settings", "access security"),
    ];
}
