namespace SmartPOS.Shared.Contracts;

public sealed class SmartAlertQueryDto
{
    public Guid? StoreId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string Severity { get; set; } = "All";
    public string Type { get; set; } = "All";
    public bool IncludeInfo { get; set; } = true;
}

public sealed class SmartAlertsPageDto
{
    public SmartAlertSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<SmartAlertDto> Alerts { get; set; } = [];
    public IReadOnlyList<AlertBreakdownDto> SeverityBreakdown { get; set; } = [];
    public IReadOnlyList<AlertBreakdownDto> TypeBreakdown { get; set; } = [];
}

public sealed class SmartAlertSummaryDto
{
    public int Total { get; set; }
    public int Critical { get; set; }
    public int Warning { get; set; }
    public int Info { get; set; }
    public int StockAlerts { get; set; }
    public int CashAlerts { get; set; }
    public int SalesAlerts { get; set; }
    public int TransferAlerts { get; set; }
    public int PricingAlerts { get; set; }
    public int DeadStockAlerts { get; set; }
}

public sealed class AlertBreakdownDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class SmartAlertDto
{
    public string AlertKey { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public string? ProductName { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public decimal? Value { get; set; }
    public decimal? Threshold { get; set; }
    public string? ActionText { get; set; }
    public string? ActionUrl { get; set; }
}
