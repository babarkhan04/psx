using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class DashboardService(IServiceScopeFactory scopeFactory, IDateTimeProvider clock, IStoreContext storeContext, ITenantContext tenantContext, IUserContextAccessor userContext) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        await using var scope = CreateReadScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var allowed = await scope.ServiceProvider.GetRequiredService<IStoreAccessService>().GetAllowedStoreIdsAsync(ct);
        if (allowed.Count == 0)
            return new DashboardSummaryDto();
        return await GetSummaryCoreAsync(db, allowed, ct);
    }

    public async Task<NavStatsDto> GetNavStatsAsync(CancellationToken ct = default)
    {
        await using var scope = CreateReadScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var allowed = await scope.ServiceProvider.GetRequiredService<IStoreAccessService>().GetAllowedStoreIdsAsync(ct);
        return await GetNavStatsCoreAsync(db, allowed, ct);
    }

    public async Task<IReadOnlyList<GlobalSearchResultDto>> SearchAsync(string query, int take = 12, CancellationToken ct = default)
    {
        await using var scope = CreateReadScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var allowed = await scope.ServiceProvider.GetRequiredService<IStoreAccessService>().GetAllowedStoreIdsAsync(ct);
        if (allowed.Count == 0)
            return [];
        return await SearchCoreAsync(db, allowed, query, take, ct);
    }

    private async Task<DashboardSummaryDto> GetSummaryCoreAsync(AppDbContext db, IReadOnlyList<Guid> allowed, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var todayUtc = now.Date;
        var weekStart = todayUtc.AddDays(-6);
        var selectedStoreId = storeContext.StoreId;
        if (selectedStoreId is { } selected && !allowed.Contains(selected))
            throw new UnauthorizedAccessException("The selected store is not accessible.");

        var productQuery = db.Products.AsNoTracking().AsQueryable();
        var stockQuery = db.Stocks.AsNoTracking().Where(row => allowed.Contains(row.StoreId));
        var salesQuery = db.Sales.AsNoTracking().Where(row => allowed.Contains(row.StoreId));
        var ledgerQuery = db.StockLedgers.AsNoTracking().Where(row => allowed.Contains(row.StoreId));
        var storeQuery = db.Stores.AsNoTracking().Where(s => s.IsActive && allowed.Contains(s.Id));
        var registerQuery = db.Registers.AsNoTracking().Where(r => r.IsActive && allowed.Contains(r.StoreId));

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

        var storeLookup = await db.Stores.AsNoTracking().Where(store => allowed.Contains(store.Id))
            .Select(s => new { s.Id, Name = s.Code + " · " + s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var weekSales = await salesQuery.Include(sale => sale.Lines).Include(sale => sale.Payments)
            .Where(sale => sale.Status != SaleStatus.Voided && sale.CompletedOn >= weekStart && sale.CompletedOn <= now)
            .ToListAsync(ct);
        var returnsQuery = db.SaleReturns.AsNoTracking().Include(item => item.Lines)
            .ThenInclude(line => line.OriginalSaleLine)
            .Where(item => allowed.Contains(item.StoreId) && item.Status == SaleReturnStatus.Completed &&
                item.ProcessedOn >= weekStart && item.ProcessedOn <= now);
        if (selectedStoreId is { } returnStore)
            returnsQuery = returnsQuery.Where(item => item.StoreId == returnStore);
        var weekReturns = await returnsQuery.ToListAsync(ct);
        var events = weekSales.Select(sale => new FinancialEvent(sale.StoreId, sale.CompletedOn,
            sale.TotalAmount, sale.TotalAmount - sale.TaxAmount - sale.Lines.Sum(line => line.Quantity * line.CostPrice),
            sale.DiscountAmount, 1)).ToList();
        events.AddRange(weekReturns.Select(item => new FinancialEvent(item.StoreId, item.ProcessedOn,
            -item.TotalAmount,
            -(item.TotalAmount - item.TaxAmount) + item.Lines.Where(line => line.Restock)
                .Sum(line => line.QuantityReturned * (line.OriginalSaleLine?.CostPrice ?? 0m)), 0m, 0)));
        var todayEvents = events.Where(item => item.Date.Date == todayUtc).ToList();
        var todaySalesTotal = todayEvents.Sum(item => item.Amount);
        var todayTxCount = todayEvents.Sum(item => item.Transactions);
        var todayProfit = todayEvents.Sum(item => item.Profit);
        var todayDiscounts = todayEvents.Sum(item => item.Discounts);
        var todayMargin = todaySalesTotal <= 0 ? 0 : Math.Round(todayProfit / todaySalesTotal * 100m, 1);
        var averageBasket = todayTxCount == 0 ? 0 : Math.Round(todaySalesTotal / todayTxCount, 2);
        var salesTrend = Enumerable.Range(0, 7).Select(offset =>
        {
            var day = weekStart.AddDays(offset);
            var dayEvents = events.Where(item => item.Date.Date == day).ToList();
            return new DashboardTrendPointDto
            {
                Label = day.ToString("ddd"),
                Count = dayEvents.Sum(item => item.Transactions),
                Amount = dayEvents.Sum(item => item.Amount),
                Profit = dayEvents.Sum(item => item.Profit)
            };
        }).ToList();

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

        var storePerformance = events
            .GroupBy(s => s.StoreId)
            .Select(g =>
            {
                var netSales = g.Sum(x => x.Amount);
                var profit = g.Sum(x => x.Profit);
                stockByStore.TryGetValue(g.Key, out var stock);
                return new DashboardStorePerformanceDto
                {
                    StoreId = g.Key,
                    StoreName = storeLookup.GetValueOrDefault(g.Key, stock?.StoreName ?? "Unknown store"),
                    NetSales = netSales,
                    GrossProfit = profit,
                    MarginPercent = netSales <= 0 ? 0 : Math.Round(profit / netSales * 100m, 1),
                    Transactions = g.Sum(x => x.Transactions),
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

        var payments = weekSales.SelectMany(sale => sale.Payments)
            .Select(payment => new PaymentEvent(payment.Method.ToString(), payment.Amount)).ToList();
        var returnNumbers = weekReturns.Select(item => item.ReturnNumber).ToList();
        var creditReversals = await db.CustomerLedgers.AsNoTracking()
            .Where(entry => entry.EntryType == CustomerLedgerEntryType.Refund && entry.Reference != null &&
                returnNumbers.Contains(entry.Reference)).ToListAsync(ct);
        foreach (var item in weekReturns)
        {
            var credit = Math.Min(item.TotalAmount, Math.Max(0m,
                -creditReversals.Where(entry => entry.Reference == item.ReturnNumber).Sum(entry => entry.Amount)));
            if (credit > 0)
                payments.Add(new PaymentEvent(PaymentMethod.Credit.ToString(), -credit));
            if (item.TotalAmount > credit)
                payments.Add(new PaymentEvent(item.RefundMethod.ToString(), -(item.TotalAmount - credit)));
        }
        var paymentTotal = payments.Sum(payment => payment.Amount);
        var paymentMix = payments.GroupBy(payment => payment.Method).Select(group => new DashboardPaymentMixDto
        {
            Method = group.Key,
            Amount = group.Sum(payment => payment.Amount),
            Count = group.Count(payment => payment.Amount > 0),
            SharePercent = paymentTotal <= 0 ? 0 : Math.Round(group.Sum(payment => payment.Amount) / paymentTotal * 100m, 1)
        }).OrderByDescending(item => item.Amount).ToList();

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

    private async Task<NavStatsDto> GetNavStatsCoreAsync(AppDbContext db, IReadOnlyList<Guid> allowed, CancellationToken ct)
    {
        var query = db.Stocks.AsNoTracking().Include(s => s.Product).Where(stock => allowed.Contains(stock.StoreId));

        if (storeContext.StoreId is { } storeId)
        {
            query = query.Where(s => s.StoreId == storeId);
        }

        var count = await query
            .CountAsync(s => s.Product != null && s.Product.ReorderLevel > 0 && s.QuantityOnHand <= s.Product.ReorderLevel, ct);

        return new NavStatsDto { LowStockCount = count };
    }

    private async Task<IReadOnlyList<GlobalSearchResultDto>> SearchCoreAsync(AppDbContext db, IReadOnlyList<Guid> allowed, string query, int take, CancellationToken ct)
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
            .Where(s => allowed.Contains(s.Id) && s.IsActive && (
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

    private AsyncServiceScope CreateReadScope()
    {
        var scope = scopeFactory.CreateAsyncScope();
        if (userContext.Principal is { } principal)
            scope.ServiceProvider.GetRequiredService<IUserContextAccessor>().SetPrincipal(principal);
        if (tenantContext.TenantId is { } tenantId)
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        return scope;
    }

    private sealed record FinancialEvent(Guid StoreId, DateTime Date, decimal Amount, decimal Profit, decimal Discounts, int Transactions);
    private sealed record PaymentEvent(string Method, decimal Amount);

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
