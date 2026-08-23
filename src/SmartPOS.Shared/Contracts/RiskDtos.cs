namespace SmartPOS.Shared.Contracts;

public sealed class RiskQueryDto
{
    public Guid? StoreId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string RiskLevel { get; set; } = "All";
}

public sealed class RiskSummaryDto
{
    public int CashierCount { get; set; }
    public int HighRiskCount { get; set; }
    public int MediumRiskCount { get; set; }
    public int LowRiskCount { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalVoids { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal TotalCashVariance { get; set; }
    public int ApprovalCount { get; set; }
}

public sealed class CashierRiskRowDto
{
    public string CashierUserId { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string StoreNames { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal NetSales { get; set; }
    public decimal AverageBasket { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountRatePercent { get; set; }
    public int VoidCount { get; set; }
    public decimal VoidAmount { get; set; }
    public int ReturnCount { get; set; }
    public decimal ReturnAmount { get; set; }
    public int CashOutCount { get; set; }
    public int CashAdjustmentCount { get; set; }
    public decimal CashVariance { get; set; }
    public int ApprovalRequestCount { get; set; }
    public int PriceOverrideApprovalCount { get; set; }
    public int DiscountApprovalCount { get; set; }
    public int RefundApprovalCount { get; set; }
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = "Low";
    public string KeyIssues { get; set; } = string.Empty;
    public DateTime? LastActivityOn { get; set; }
}

public sealed class RiskEventDto
{
    public DateTime EventOn { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Severity { get; set; } = "Info";
    public string Details { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
}

public sealed class RiskBreakdownDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public sealed class RiskDashboardDto
{
    public RiskSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<CashierRiskRowDto> Cashiers { get; set; } = [];
    public IReadOnlyList<RiskEventDto> Events { get; set; } = [];
    public IReadOnlyList<RiskBreakdownDto> RiskBreakdown { get; set; } = [];
    public IReadOnlyList<RiskBreakdownDto> EventBreakdown { get; set; } = [];
}
