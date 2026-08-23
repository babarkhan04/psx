using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class AlertService(
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    IUserContextAccessor userContext) : IAlertService
{
    private const int DeadStockDays = 60;
    private const int StaleOpenShiftHours = 12;
    private const decimal HighDiscountPercent = 20m;
    private const decimal CriticalCashVariance = 1000m;

    public async Task<SmartAlertsPageDto> GetAlertsAsync(SmartAlertQueryDto query, CancellationToken ct = default)
    {
        var tenantId = tenantContext.TenantId;
        var principal = userContext.Principal;

        await using var scope = scopeFactory.CreateAsyncScope();

        if (principal is not null)
        {
            scope.ServiceProvider.GetRequiredService<IUserContextAccessor>().SetPrincipal(principal);
        }

        if (tenantId is { } id)
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(id);
        }

        var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await GetAlertsCoreAsync(scopedDb, query, ct);
    }

    private async Task<SmartAlertsPageDto> GetAlertsCoreAsync(AppDbContext db, SmartAlertQueryDto query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var fromUtc = query.FromUtc ?? now.Date.AddDays(-7);
        var toUtc = query.ToUtc ?? now.Date.AddDays(1);

        if (toUtc <= fromUtc)
        {
            toUtc = fromUtc.AddDays(1);
        }

        var stores = await db.Stores.AsNoTracking().ToDictionaryAsync(s => s.Id, s => FormatStore(s.Code, s.Name), ct);
        var alerts = new List<SmartAlertDto>();

        await AddStockAlertsAsync(db, alerts, query.StoreId, stores, now, ct);
        await AddPricingAlertsAsync(db, alerts, query.StoreId, stores, now, ct);
        await AddSalesRiskAlertsAsync(db, alerts, query.StoreId, stores, fromUtc, toUtc, now, ct);
        await AddCashAndShiftAlertsAsync(db, alerts, query.StoreId, stores, fromUtc, toUtc, now, ct);
        await AddTransferAlertsAsync(db, alerts, query.StoreId, stores, now, ct);
        await AddDeadStockAlertsAsync(db, alerts, query.StoreId, stores, now, ct);

        alerts = alerts
            .Where(a => query.IncludeInfo || !a.Severity.Equals("Info", StringComparison.OrdinalIgnoreCase))
            .Where(a => query.Severity.Equals("All", StringComparison.OrdinalIgnoreCase) || a.Severity.Equals(query.Severity, StringComparison.OrdinalIgnoreCase))
            .Where(a => query.Type.Equals("All", StringComparison.OrdinalIgnoreCase) || a.Type.Equals(query.Type, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => SeverityRank(a.Severity))
            .ThenByDescending(a => a.CreatedOnUtc)
            .ThenBy(a => a.Type)
            .Take(500)
            .ToList();

        return new SmartAlertsPageDto
        {
            Summary = BuildSummary(alerts),
            Alerts = alerts,
            SeverityBreakdown = alerts.GroupBy(a => a.Severity)
                .Select(g => new AlertBreakdownDto { Name = g.Key, Count = g.Count() })
                .OrderBy(x => SeverityRank(x.Name))
                .ToList(),
            TypeBreakdown = alerts.GroupBy(a => a.Type)
                .Select(g => new AlertBreakdownDto { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .ToList()
        };
    }

    private async Task AddStockAlertsAsync(AppDbContext db, List<SmartAlertDto> alerts, Guid? storeId, IReadOnlyDictionary<Guid, string> stores, DateTime now, CancellationToken ct)
    {
        var query = db.Stocks.AsNoTracking()
            .Include(s => s.Product)
            .Where(s => s.Product != null && s.Product.IsActive && s.Product.ReorderLevel > 0);

        if (storeId is { } id)
        {
            query = query.Where(s => s.StoreId == id);
        }

        var rows = await query.ToListAsync(ct);

        foreach (var row in rows.Where(s => s.QuantityOnHand <= s.Product!.ReorderLevel))
        {
            var product = row.Product!;
            var critical = row.QuantityOnHand <= 0 || row.QuantityOnHand <= product.ReorderLevel * 0.25m;
            alerts.Add(new SmartAlertDto
            {
                AlertKey = $"stock-{row.StoreId}-{row.ProductId}-{row.ProductVariantId}",
                Severity = critical ? "Critical" : "Warning",
                Type = "Stock",
                Title = row.QuantityOnHand <= 0 ? "Out of stock" : "Low stock",
                Message = $"{product.Sku} · {product.Name} has {row.QuantityOnHand:0.###} on hand. Reorder level is {product.ReorderLevel:0.###}.",
                StoreName = StoreName(stores, row.StoreId),
                ProductSku = product.Sku,
                ProductName = product.Name,
                CreatedOnUtc = now,
                Value = row.QuantityOnHand,
                Threshold = product.ReorderLevel,
                ActionText = "Open inventory",
                ActionUrl = "/inventory"
            });
        }
    }

    private async Task AddPricingAlertsAsync(AppDbContext db, List<SmartAlertDto> alerts, Guid? storeId, IReadOnlyDictionary<Guid, string> stores, DateTime now, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.CostPrice > 0 && p.SellPrice < p.CostPrice)
            .OrderBy(p => p.Sku)
            .Take(100)
            .ToListAsync(ct);

        foreach (var product in products)
        {
            alerts.Add(new SmartAlertDto
            {
                AlertKey = $"price-product-{product.Id}",
                Severity = "Critical",
                Type = "Pricing",
                Title = "Selling price below cost",
                Message = $"{product.Sku} · {product.Name} sells at {product.SellPrice:N2}, but cost is {product.CostPrice:N2}.",
                StoreName = storeId is null ? "All stores" : StoreName(stores, storeId.Value),
                ProductSku = product.Sku,
                ProductName = product.Name,
                CreatedOnUtc = now,
                Value = product.SellPrice,
                Threshold = product.CostPrice,
                ActionText = "Open products",
                ActionUrl = "/products"
            });
        }

        var storePricesQuery = db.ProductPrices.AsNoTracking()
            .Include(p => p.Product)
            .Where(p => p.Product != null && p.Product.IsActive && p.Product.CostPrice > 0 && p.Price < p.Product.CostPrice);

        if (storeId is { } id)
        {
            storePricesQuery = storePricesQuery.Where(p => p.StoreId == id);
        }

        var storePrices = await storePricesQuery.Take(100).ToListAsync(ct);
        foreach (var price in storePrices)
        {
            var product = price.Product!;
            alerts.Add(new SmartAlertDto
            {
                AlertKey = $"price-store-{price.StoreId}-{price.ProductId}",
                Severity = "Critical",
                Type = "Pricing",
                Title = "Store price below cost",
                Message = $"{product.Sku} · {product.Name} store price is {price.Price:N2}, but cost is {product.CostPrice:N2}.",
                StoreName = StoreName(stores, price.StoreId),
                ProductSku = product.Sku,
                ProductName = product.Name,
                CreatedOnUtc = now,
                Value = price.Price,
                Threshold = product.CostPrice,
                ActionText = "Open products",
                ActionUrl = "/products"
            });
        }
    }

    private async Task AddSalesRiskAlertsAsync(
        AppDbContext db,
        List<SmartAlertDto> alerts,
        Guid? storeId,
        IReadOnlyDictionary<Guid, string> stores,
        DateTime fromUtc,
        DateTime toUtc,
        DateTime now,
        CancellationToken ct)
    {
        var saleQuery = db.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Where(s => s.CompletedOn >= fromUtc && s.CompletedOn < toUtc);

        if (storeId is { } id)
        {
            saleQuery = saleQuery.Where(s => s.StoreId == id);
        }

        var sales = await saleQuery.ToListAsync(ct);
        foreach (var sale in sales.Where(s => s.Status == SaleStatus.Voided))
        {
            alerts.Add(new SmartAlertDto
            {
                AlertKey = $"void-{sale.Id}",
                Severity = "Warning",
                Type = "Sales",
                Title = "Voided sale",
                Message = $"Invoice {sale.SaleNumber} was voided. Review reason and approval trail.",
                StoreName = StoreName(stores, sale.StoreId),
                CreatedOnUtc = sale.CompletedOn,
                Value = sale.TotalAmount,
                ActionText = "Open sales",
                ActionUrl = "/sales"
            });
        }

        foreach (var sale in sales.Where(s => s.Status != SaleStatus.Voided && s.Subtotal > 0))
        {
            var lineDiscount = sale.Lines.Sum(l => l.DiscountAmount);
            var totalDiscount = sale.DiscountAmount + lineDiscount;
            var discountPercent = sale.Subtotal == 0 ? 0 : totalDiscount / sale.Subtotal * 100m;

            if (discountPercent >= HighDiscountPercent)
            {
                alerts.Add(new SmartAlertDto
                {
                    AlertKey = $"discount-{sale.Id}",
                    Severity = discountPercent >= 40m ? "Critical" : "Warning",
                    Type = "Discount",
                    Title = "High discount sale",
                    Message = $"Invoice {sale.SaleNumber} has {discountPercent:0.#}% discount ({totalDiscount:N2}).",
                    StoreName = StoreName(stores, sale.StoreId),
                    CreatedOnUtc = sale.CompletedOn,
                    Value = totalDiscount,
                    Threshold = HighDiscountPercent,
                    ActionText = "Open reports",
                    ActionUrl = "/reports"
                });
            }
        }

        var returnQuery = db.SaleReturns.AsNoTracking()
            .Where(r => r.ProcessedOn >= fromUtc && r.ProcessedOn < toUtc && r.Status == SaleReturnStatus.Completed);

        if (storeId is { } store)
        {
            returnQuery = returnQuery.Where(r => r.StoreId == store);
        }

        var returnsByStore = await returnQuery
            .GroupBy(r => r.StoreId)
            .Select(g => new { StoreId = g.Key, Count = g.Count(), Total = g.Sum(r => r.TotalAmount), Latest = g.Max(r => r.ProcessedOn) })
            .ToListAsync(ct);

        foreach (var row in returnsByStore.Where(r => r.Count >= 3 || r.Total > 0))
        {
            alerts.Add(new SmartAlertDto
            {
                AlertKey = $"returns-{row.StoreId}-{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}",
                Severity = row.Count >= 5 ? "Warning" : "Info",
                Type = "Returns",
                Title = "Return/refund activity",
                Message = $"{row.Count} return(s) totaling {row.Total:N2} in the selected period.",
                StoreName = StoreName(stores, row.StoreId),
                CreatedOnUtc = row.Latest,
                Value = row.Total,
                ActionText = "Open reports",
                ActionUrl = "/reports"
            });
        }
    }

    private async Task AddCashAndShiftAlertsAsync(
        AppDbContext db,
        List<SmartAlertDto> alerts,
        Guid? storeId,
        IReadOnlyDictionary<Guid, string> stores,
        DateTime fromUtc,
        DateTime toUtc,
        DateTime now,
        CancellationToken ct)
    {
        var closedShiftQuery = db.Shifts.AsNoTracking()
            .Where(s => s.ClosedOn != null && s.ClosedOn >= fromUtc && s.ClosedOn < toUtc && s.Variance != 0);

        if (storeId is { } id)
        {
            closedShiftQuery = closedShiftQuery.Where(s => s.StoreId == id);
        }

        var shifts = await closedShiftQuery.ToListAsync(ct);
        foreach (var shift in shifts)
        {
            var absVariance = Math.Abs(shift.Variance);
            alerts.Add(new SmartAlertDto
            {
                AlertKey = $"cash-variance-{shift.Id}",
                Severity = absVariance >= CriticalCashVariance ? "Critical" : "Warning",
                Type = "Cash",
                Title = "Cash variance",
                Message = $"Shift {shift.ShiftNumber} variance is {shift.Variance:N2}. Expected {shift.ExpectedCash:N2}, counted {shift.CountedCash:N2}.",
                StoreName = StoreName(stores, shift.StoreId),
                CreatedOnUtc = shift.ClosedOn ?? now,
                Value = shift.Variance,
                Threshold = CriticalCashVariance,
                ActionText = "Open shifts",
                ActionUrl = "/shifts"
            });
        }

        var staleLimit = now.AddHours(-StaleOpenShiftHours);
        var openShiftQuery = db.Shifts.AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Open && s.OpenedOn <= staleLimit);

        if (storeId is { } store)
        {
            openShiftQuery = openShiftQuery.Where(s => s.StoreId == store);
        }

        var openShifts = await openShiftQuery.ToListAsync(ct);
        foreach (var shift in openShifts)
        {
            var hours = Math.Round((now - shift.OpenedOn).TotalHours, 1);
            alerts.Add(new SmartAlertDto
            {
                AlertKey = $"open-shift-{shift.Id}",
                Severity = hours >= 24 ? "Critical" : "Warning",
                Type = "Shift",
                Title = "Shift still open",
                Message = $"Shift {shift.ShiftNumber} has been open for {hours:0.#} hour(s).",
                StoreName = StoreName(stores, shift.StoreId),
                CreatedOnUtc = shift.OpenedOn,
                Value = (decimal)hours,
                Threshold = StaleOpenShiftHours,
                ActionText = "Open shifts",
                ActionUrl = "/shifts"
            });
        }
    }

    private async Task AddTransferAlertsAsync(AppDbContext db, List<SmartAlertDto> alerts, Guid? storeId, IReadOnlyDictionary<Guid, string> stores, DateTime now, CancellationToken ct)
    {
        var staleRequestedLimit = now.AddDays(-2);
        var staleDispatchedLimit = now.AddDays(-1);

        var query = db.StockTransfers.AsNoTracking()
            .Where(t => t.Status == StockTransferStatus.Requested ||
                        t.Status == StockTransferStatus.Approved ||
                        t.Status == StockTransferStatus.Dispatched ||
                        t.Status == StockTransferStatus.PartiallyReceived);

        if (storeId is { } id)
        {
            query = query.Where(t => t.StoreId == id || t.DestinationStoreId == id);
        }

        var transfers = await query.ToListAsync(ct);
        foreach (var transfer in transfers)
        {
            var ageDate = transfer.DispatchedOn ?? transfer.ApprovedOn ?? transfer.RequestedOn;
            var isStale = transfer.Status == StockTransferStatus.Dispatched
                ? ageDate <= staleDispatchedLimit
                : ageDate <= staleRequestedLimit;

            if (!isStale)
            {
                continue;
            }

            alerts.Add(new SmartAlertDto
            {
                AlertKey = $"transfer-{transfer.Id}",
                Severity = transfer.Status == StockTransferStatus.Dispatched ? "Warning" : "Info",
                Type = "Transfer",
                Title = "Stock transfer needs attention",
                Message = $"Transfer {transfer.TransferNumber} is {transfer.Status} from {StoreName(stores, transfer.StoreId)} to {StoreName(stores, transfer.DestinationStoreId)}.",
                StoreName = $"{StoreName(stores, transfer.StoreId)} → {StoreName(stores, transfer.DestinationStoreId)}",
                CreatedOnUtc = ageDate,
                ActionText = "Open transfers",
                ActionUrl = "/transfers"
            });
        }
    }

    private async Task AddDeadStockAlertsAsync(AppDbContext db, List<SmartAlertDto> alerts, Guid? storeId, IReadOnlyDictionary<Guid, string> stores, DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-DeadStockDays);
        var stocksQuery = db.Stocks.AsNoTracking()
            .Include(s => s.Product)
            .Where(s => s.Product != null && s.Product.IsActive && s.QuantityOnHand > 0);

        if (storeId is { } id)
        {
            stocksQuery = stocksQuery.Where(s => s.StoreId == id);
        }

        var stocks = await stocksQuery.ToListAsync(ct);
        if (stocks.Count == 0)
        {
            return;
        }

        var productIds = stocks.Select(s => s.ProductId).Distinct().ToList();
        var recentSalesQuery = db.SaleLines.AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId) &&
                        l.Sale != null &&
                        l.Sale.Status == SaleStatus.Completed &&
                        l.Sale.CompletedOn >= cutoff);

        if (storeId is { } recentStoreId)
        {
            recentSalesQuery = recentSalesQuery.Where(l => l.Sale != null && l.Sale.StoreId == recentStoreId);
        }

        var recentSellerIds = await recentSalesQuery
            .Select(l => l.ProductId)
            .Distinct()
            .ToListAsync(ct);

        var recentSellerSet = recentSellerIds.ToHashSet();

        foreach (var row in stocks.Where(s => !recentSellerSet.Contains(s.ProductId)).OrderByDescending(s => s.QuantityOnHand).Take(50))
        {
            var product = row.Product!;
            alerts.Add(new SmartAlertDto
            {
                AlertKey = $"dead-stock-{row.StoreId}-{row.ProductId}-{row.ProductVariantId}",
                Severity = "Info",
                Type = "Dead Stock",
                Title = "No recent sales",
                Message = $"{product.Sku} · {product.Name} has {row.QuantityOnHand:0.###} on hand and no sale in the last {DeadStockDays} days.",
                StoreName = StoreName(stores, row.StoreId),
                ProductSku = product.Sku,
                ProductName = product.Name,
                CreatedOnUtc = now,
                Value = row.QuantityOnHand,
                Threshold = DeadStockDays,
                ActionText = "Open reports",
                ActionUrl = "/reports"
            });
        }
    }

    private static SmartAlertSummaryDto BuildSummary(IReadOnlyList<SmartAlertDto> alerts) =>
        new()
        {
            Total = alerts.Count,
            Critical = alerts.Count(a => a.Severity == "Critical"),
            Warning = alerts.Count(a => a.Severity == "Warning"),
            Info = alerts.Count(a => a.Severity == "Info"),
            StockAlerts = alerts.Count(a => a.Type == "Stock"),
            CashAlerts = alerts.Count(a => a.Type == "Cash" || a.Type == "Shift"),
            SalesAlerts = alerts.Count(a => a.Type == "Sales" || a.Type == "Discount" || a.Type == "Returns"),
            TransferAlerts = alerts.Count(a => a.Type == "Transfer"),
            PricingAlerts = alerts.Count(a => a.Type == "Pricing"),
            DeadStockAlerts = alerts.Count(a => a.Type == "Dead Stock")
        };

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 0,
        "Warning" => 1,
        "Info" => 2,
        _ => 3
    };

    private static string StoreName(IReadOnlyDictionary<Guid, string> stores, Guid storeId) =>
        stores.TryGetValue(storeId, out var name) ? name : "Unknown store";

    private static string FormatStore(string code, string name) =>
        string.IsNullOrWhiteSpace(code) ? name : $"{code} · {name}";
}
