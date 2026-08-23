namespace SmartPOS.Shared.Contracts;

public sealed class DashboardSummaryDto
{
    public int ProductCount { get; set; }
    public int ActiveProductCount { get; set; }
    public int LowStockCount { get; set; }
    public int CriticalStockCount { get; set; }
    public decimal InventoryRetailValue { get; set; }
    public int MovementsToday { get; set; }
    public int StoreCount { get; set; }
    public int RegisterCount { get; set; }

    /// <summary>True once POS billing is live; until then sales KPIs show placeholders.</summary>
    public bool SalesLive { get; set; }

    public decimal TodaySales { get; set; }
    public int TodayTransactions { get; set; }
    public decimal TodayProfit { get; set; }
    public decimal TodayMarginPercent { get; set; }
    public decimal TodayDiscounts { get; set; }
    public decimal AverageBasket { get; set; }

    public IReadOnlyList<DashboardAlertDto> Alerts { get; set; } = [];
    public IReadOnlyList<DashboardStoreSnapshotDto> StoreSnapshots { get; set; } = [];
    public IReadOnlyList<DashboardTrendPointDto> ActivityTrend { get; set; } = [];
    public IReadOnlyList<DashboardStorePerformanceDto> StorePerformance { get; set; } = [];
    public IReadOnlyList<DashboardPaymentMixDto> PaymentMix { get; set; } = [];
}

public sealed class DashboardAlertDto
{
    public string Priority { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
}

public sealed class DashboardStoreSnapshotDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal RetailValue { get; set; }
    public int LowStockItems { get; set; }
    public int MovementsLast7Days { get; set; }
}

public sealed class DashboardTrendPointDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
    public decimal Profit { get; set; }
}

public sealed class DashboardStorePerformanceDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal NetSales { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
    public int Transactions { get; set; }
    public decimal InventoryValue { get; set; }
    public int LowStockItems { get; set; }
}

public sealed class DashboardPaymentMixDto
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal SharePercent { get; set; }
}

public sealed class NavStatsDto
{
    public int LowStockCount { get; set; }
}

public sealed class GlobalSearchResultDto
{
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Icon { get; set; } = "search";
}
