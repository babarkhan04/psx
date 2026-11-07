using System.Globalization;
using Radzen;
using SmartPOS.Shared.Contracts;
using SmartPOS.Web.Components.Shared;

namespace SmartPOS.Web.Components.Pages.Modules;

public partial class Reports
{
    private sealed record ReportDefinition(string Id, string Group, string Title, string Description, string Icon);
    private sealed record StoreOption(string Text, Guid? Value);
    private sealed record TrendPoint(DateTime Date, decimal Sales, decimal Profit);

    private static readonly IReadOnlyList<ReportDefinition> Definitions =
    [
        new("overview", "Overview", "Business overview", "A clear view of sales, profit and the items that need your attention.", "space_dashboard"),
        new("daily", "Sales", "Daily sales", "Sales, discounts, refunds and gross profit by transaction date in UTC.", "calendar_today"),
        new("invoices", "Sales", "Sales register", "Invoice details. Refunds here relate to listed invoices and were recorded in this period; see Returns for all period refunds.", "receipt_long"),
        new("stores", "Sales", "Store performance", "Compare revenue and profitability across your accessible stores.", "storefront"),
        new("cashiers", "Sales", "Cashier performance", "Transactions, discounts, refunds and average baskets by cashier.", "badge"),
        new("registers", "Sales", "Register performance", "Completed-sale receipts before refunds, split by register and payment method.", "point_of_sale"),
        new("hourly", "Sales", "Hourly sales", "Understand your busiest trading hours. Transaction hours are in UTC.", "schedule"),
        new("weekdays", "Sales", "Weekday performance", "Average sales include every calendar occurrence of each weekday in the selected range, including days with no sales.", "date_range"),
        new("products", "Products", "Product profitability", "All matching products and variants, with sales, cost and gross profit after recorded refunds.", "trending_up"),
        new("categories", "Products", "Category performance", "Revenue and gross profit grouped by product category.", "category"),
        new("brands", "Products", "Brand performance", "Compare revenue, units and gross profit across brands.", "sell"),
        new("inventory", "Inventory", "Stock & valuation", "Current stock at current catalog cost, including variant-specific costs. This snapshot is independent of the date range.", "inventory_2"),
        new("low-stock", "Inventory", "Low & out-of-stock", "Current stock lines at or below their reorder level, plus zero and negative quantities.", "warning_amber"),
        new("slow-stock", "Inventory", "Slow-moving stock", "Stock currently on hand with five or fewer units sold in the selected period. Cover estimates use gross units sold; refunds do not reduce that count.", "hourglass_empty"),
        new("ledger", "Inventory", "Stock ledger", "Every recorded stock movement in the selected period, with the balance after each transaction. Times are UTC.", "format_list_bulleted"),
        new("movements", "Inventory", "Movement summary", "Opening, purchase, sale, damage, loss, transfer and return quantities for the selected period.", "swap_vert"),
        new("transfers", "Inventory", "Store transfers", "Track requested, dispatched and received quantities for transfers requested during this period.", "local_shipping"),
        new("payments", "Operations", "Payment mix", "Payment receipts from completed sales. Refunds are listed separately in Returns.", "account_balance_wallet"),
        new("discounts", "Operations", "Discount details", "Invoice and line discounts, with the cashier and recorded reason.", "percent"),
        new("returns", "Operations", "Returns & refunds", "All completed refunds processed in the selected period, including refunds for older invoices.", "assignment_return"),
        new("voids", "Operations", "Voided sales", "Voided invoices completed in the selected period.", "block"),
        new("shifts", "Operations", "Shift cash", "Expected cash, counted cash and variances for shifts opened in the selected period.", "payments"),
        new("purchases", "Purchasing & tax", "Purchase register", "Received purchases only, filtered by receipt date. Draft and cancelled purchases are excluded.", "shopping_bag"),
        new("suppliers", "Purchasing & tax", "Purchases by supplier", "Received purchase totals and receipt counts grouped by supplier.", "handshake"),
        new("tax", "Purchasing & tax", "Tax summary", "Tax by rate for sales in the period. The overview shows tax collected after period refunds.", "calculate"),
        new("customers", "Customers", "Customer performance", "Top 100 customers by period spend. Sales follow the selected store and dates; balances are current company-wide balances.", "group"),
    ];
    private static readonly HashSet<string> NewReportIds = ["invoices", "purchases", "ledger", "slow-stock", "weekdays", "brands", "registers", "low-stock"];
    private ReportsPageDto? _report;
    private ReportQueryDto? _appliedQuery;
    private readonly List<StoreOption> _storeOptions = [new("All accessible stores", null)];
    private Guid? _storeId;
    private DateTime? _from = DateTime.UtcNow.Date.AddDays(-29);
    private DateTime? _to = DateTime.UtcNow.Date;
    private DateTime _loadedAt;
    private string _selectedId = "overview";
    private string _navigationSearch = string.Empty;
    private bool _loading;
    private string? _error;
    private IReadOnlyList<TrendPoint> _trend = [];
    private int _trendBucketDays = 1;

    private IEnumerable<ReportDefinition> VisibleDefinitions => Definitions.Where(item =>
        string.IsNullOrWhiteSpace(_navigationSearch) ||
        $"{item.Title} {item.Group} {item.Description}".Contains(_navigationSearch.Trim(), StringComparison.OrdinalIgnoreCase));
    private ReportDefinition ActiveDefinition => Definitions.First(item => item.Id == _selectedId);
    private bool IsSnapshot => _selectedId is "inventory" or "low-stock";
    private string AppliedStoreName => _storeOptions.FirstOrDefault(store => store.Value == _appliedQuery?.StoreId)?.Text ?? "All accessible stores";
    private string PeriodLabel => _appliedQuery is null ? string.Empty : $"{_appliedQuery.FromUtc:dd MMM yyyy} – {_appliedQuery.ToUtc?.AddDays(-1):dd MMM yyyy}";
    private bool HasPendingChanges => _appliedQuery is not null &&
        (_storeId != _appliedQuery.StoreId || AsUtc(_from) != _appliedQuery.FromUtc || AsUtc(_to)?.AddDays(1) != _appliedQuery.ToUtc);
    private string TrendCaption => _trendBucketDays == 1 ? "Daily totals · days with no activity included" : $"Totals grouped into {_trendBucketDays}-day intervals";
    private int ActiveRowCount => _report is null ? 0 : _selectedId switch
    {
        "daily" => _report.DailySales.Count,
        "invoices" => _report.SalesRegister.Count,
        "stores" => _report.SalesByStore.Count,
        "cashiers" => _report.SalesByCashier.Count,
        "registers" => _report.SalesByRegister.Count,
        "hourly" => _report.HourlySales.Count,
        "weekdays" => _report.WeekdaySales.Count,
        "products" => _report.ProductPerformance.Count,
        "categories" => _report.SalesByCategory.Count,
        "brands" => _report.SalesByBrand.Count,
        "inventory" => _report.CurrentStock.Count,
        "low-stock" => _report.CurrentStock.Count(row => row.IsLowStock || row.IsOutOfStock),
        "slow-stock" => _report.SlowMovingStock.Count,
        "ledger" => _report.StockLedger.Count,
        "movements" => _report.StockMovements.Count,
        "transfers" => _report.StockTransfers.Count,
        "payments" => _report.PaymentMix.Count,
        "discounts" => _report.Discounts.Count,
        "returns" => _report.Returns.Count,
        "voids" => _report.Voids.Count,
        "shifts" => _report.Shifts.Count,
        "purchases" => _report.PurchaseRegister.Count,
        "suppliers" => _report.Purchases.BySupplier.Count,
        "tax" => _report.Tax.ByRate.Count,
        "customers" => _report.Customers.Count,
        _ => 0
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var stores = await StoreService.GetAllAsync(includeInactive: false);
            _storeOptions.AddRange(stores.Select(store => new StoreOption($"{store.Code} · {store.Name}", store.Id)));
            if (StoreContext.StoreId is { } selected && stores.Any(store => store.Id == selected)) _storeId = selected;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to initialize the reports workspace");
            _error = "Reports could not be loaded. Please try applying the filters again.";
        }
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        if (_from is null || _to is null || _to.Value.Date < _from.Value.Date || _to.Value.Date == DateTime.MaxValue.Date)
        {
            _error = "Choose a valid start and end date. The end date must be on or after the start date.";
            return;
        }
        var query = new ReportQueryDto { StoreId = _storeId, FromUtc = AsUtc(_from), ToUtc = AsUtc(_to)?.AddDays(1) };
        _loading = true;
        _error = null;
        try
        {
            var report = await ReportService.GetReportsAsync(query);
            _report = report;
            _appliedQuery = query;
            _loadedAt = DateTime.UtcNow;
            BuildTrend(report, query.FromUtc!.Value, query.ToUtc!.Value);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to refresh reports");
            _error = "Reports could not be refreshed. Check your connection and try again. Previously loaded results remain available.";
        }
        finally
        {
            _loading = false;
        }
    }

    private void SelectReport(string id) => _selectedId = id;

    private static (DateTime From, DateTime To) PresetDates(int preset)
    {
        var today = DateTime.UtcNow.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        return preset switch
        {
            -1 => (month, today),
            -2 => (month.AddMonths(-1), month.AddDays(-1)),
            _ => (today.AddDays(-preset), today)
        };
    }

    private bool IsPreset(int preset)
    {
        var range = PresetDates(preset);
        return _from?.Date == range.From && _to?.Date == range.To;
    }

    private async Task SetPresetAsync(int preset)
    {
        if (_loading) return;
        var range = PresetDates(preset);
        _from = range.From;
        _to = range.To;
        await LoadAsync();
    }

    private void BuildTrend(ReportsPageDto report, DateTime from, DateTime to)
    {
        var days = Math.Max(1, (int)(to.Date - from.Date).TotalDays);
        _trendBucketDays = Math.Max(1, (int)Math.Ceiling(days / 45m));
        var rows = report.DailySales.GroupBy(row => (int)(row.Date.Date - from.Date).TotalDays / _trendBucketDays)
            .ToDictionary(group => group.Key, group => (Sales: group.Sum(row => row.NetSales), Profit: group.Sum(row => row.GrossProfit)));
        _trend = Enumerable.Range(0, (days + _trendBucketDays - 1) / _trendBucketDays).Select(index =>
        {
            var totals = rows.GetValueOrDefault(index);
            return new TrendPoint(from.AddDays(index * _trendBucketDays), totals.Sales, totals.Profit);
        }).ToList();
    }

    private string ExportUrl
    {
        get
        {
            if (_appliedQuery is null) return string.Empty;
            var parts = new List<string>();
            if (_appliedQuery.StoreId is { } store) parts.Add($"storeId={store}");
            if (_appliedQuery.FromUtc is { } from) parts.Add($"fromUtc={Uri.EscapeDataString(from.ToString("O"))}");
            if (_appliedQuery.ToUtc is { } to) parts.Add($"toUtc={Uri.EscapeDataString(to.ToString("O"))}");
            return "api/reports/export?" + string.Join("&", parts);
        }
    }

    private static DateTime? AsUtc(DateTime? date) => date is { } value ? DateTime.SpecifyKind(value.Date, DateTimeKind.Utc) : null;
    private static string ChartMoney(object value) => Convert.ToDecimal(value, CultureInfo.CurrentCulture).ToString("N0");
    private async Task ShowStoreInsightAsync(StoreSalesRowDto row) =>
        await ShowInsightAsync($"Store · {row.StoreName}", new Dictionary<string, string>
        {
            ["Transactions"] = row.Transactions.ToString("N0"),
            ["Gross Sales"] = Money(row.GrossSales),
            ["Returns"] = Money(row.Returns),
            ["Net Sales"] = Money(row.NetSales),
            ["Discounts"] = Money(row.Discounts),
            ["Gross Profit"] = Money(row.GrossProfit),
            ["Margin"] = Pct(row.MarginPercent),
            ["Average Basket"] = Money(row.AverageBasket)
        });

    private async Task ShowCashierInsightAsync(CashierSalesRowDto row) =>
        await ShowInsightAsync($"Cashier · {row.CashierName}", new Dictionary<string, string>
        {
            ["Transactions"] = row.Transactions.ToString("N0"),
            ["Void Count"] = row.VoidCount.ToString("N0"),
            ["Return Count"] = row.ReturnCount.ToString("N0"),
            ["Gross Sales"] = Money(row.GrossSales),
            ["Discounts"] = Money(row.Discounts),
            ["Returns"] = Money(row.Returns),
            ["Net Sales"] = Money(row.NetSales),
            ["Average Basket"] = Money(row.AverageBasket)
        });

    private async Task ShowProductInsightAsync(ProductSalesRowDto row) =>
        await ShowInsightAsync($"Product · {row.ProductName}", new Dictionary<string, string>
        {
            ["SKU"] = row.Sku,
            ["Category"] = row.CategoryName,
            ["Brand"] = row.BrandName,
            ["Quantity Sold"] = Qty(row.QuantitySold),
            ["Quantity Returned"] = Qty(row.QuantityReturned),
            ["Net Quantity"] = Qty(row.NetQuantity),
            ["Revenue"] = Money(row.Revenue),
            ["Cost"] = Money(row.Cost),
            ["Gross Profit"] = Money(row.GrossProfit),
            ["Margin"] = Pct(row.MarginPercent),
            ["Discount"] = Money(row.DiscountAmount)
        });

    private async Task ShowInsightAsync(string title, Dictionary<string, string> details)
    {
        await DialogService.OpenAsync<ReportInsightDialog>(title,
            new Dictionary<string, object?>
            {
                [nameof(ReportInsightDialog.Details)] = details
            },
            new DialogOptions { Width = "520px", CloseDialogOnOverlayClick = true });
    }

    private static string Money(decimal value) => value.ToString("C2");
    private static string Money0(decimal value) => value.ToString("C0");
    private static string Qty(decimal value) => value.ToString("0.###");
    private static string Pct(decimal value) => value.ToString("0.#") + "%";
}
