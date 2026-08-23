namespace SmartPOS.Shared.Contracts;

public sealed class ReorderQueryDto
{
    public Guid? StoreId { get; set; }
    public string? Search { get; set; }
    public int LookbackDays { get; set; } = 30;
    public int LeadTimeDays { get; set; } = 7;
    public int SafetyStockDays { get; set; } = 3;
    public int DeadStockDays { get; set; } = 60;
    public bool IncludeHealthy { get; set; }
    public bool IncludeDeadStock { get; set; } = true;
    public bool PreferTransfers { get; set; } = true;
    public int Take { get; set; } = 500;
}

public sealed class ReorderDashboardDto
{
    public ReorderSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<ReorderSuggestionDto> Suggestions { get; set; } = [];
    public IReadOnlyList<ReorderSuggestionDto> PurchaseSuggestions { get; set; } = [];
    public IReadOnlyList<ReorderSuggestionDto> TransferSuggestions { get; set; } = [];
    public IReadOnlyList<ReorderSuggestionDto> DeadStockWarnings { get; set; } = [];
}

public sealed class ReorderSummaryDto
{
    public int TotalSuggestions { get; set; }
    public int CriticalCount { get; set; }
    public int PurchaseCount { get; set; }
    public int TransferCount { get; set; }
    public int DeadStockCount { get; set; }
    public int StockoutCount { get; set; }
    public int StockoutSoonCount { get; set; }
    public decimal EstimatedPurchaseValue { get; set; }
    public decimal EstimatedTransferQuantity { get; set; }
}

public sealed class ReorderSuggestionDto
{
    public string Key => $"{StoreId:N}-{ProductId:N}-{ProductVariantId?.ToString("N") ?? "product"}";

    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string? CategoryName { get; set; }
    public string? BrandName { get; set; }

    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal QuantitySold { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal NetQuantitySold { get; set; }
    public decimal AverageDailySales { get; set; }
    public decimal? DaysOfStockLeft { get; set; }
    public DateTime? LastSoldOn { get; set; }

    public decimal LeadTimeDemand { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal TargetStock { get; set; }
    public decimal IncomingPurchaseQuantity { get; set; }
    public decimal IncomingTransferQuantity { get; set; }
    public decimal SuggestedPurchaseQuantity { get; set; }
    public decimal SuggestedTransferQuantity { get; set; }

    public Guid? SuggestedSourceStoreId { get; set; }
    public string? SuggestedSourceStoreName { get; set; }
    public decimal SuggestedSourceAvailableQuantity { get; set; }

    public Guid? SuggestedSupplierId { get; set; }
    public string? SuggestedSupplierName { get; set; }
    public decimal LastUnitCost { get; set; }
    public decimal EstimatedPurchaseCost { get; set; }

    public string Severity { get; set; } = "Info";
    public string ActionType { get; set; } = "Review";
    public string Reason { get; set; } = string.Empty;
    public bool IsDeadStock { get; set; }
    public bool IsStockout { get; set; }
    public bool IsStockoutSoon { get; set; }
    public bool IsHealthy { get; set; }
}

public sealed class CreateReorderPurchaseDraftDto
{
    public Guid StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public string? Notes { get; set; }
    public IList<CreateReorderPurchaseDraftLineDto> Lines { get; set; } = [];
}

public sealed class CreateReorderPurchaseDraftLineDto
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public sealed class CreateReorderTransferRequestDto
{
    public Guid SourceStoreId { get; set; }
    public Guid DestinationStoreId { get; set; }
    public string? Notes { get; set; }
    public IList<CreateReorderTransferRequestLineDto> Lines { get; set; } = [];
}

public sealed class CreateReorderTransferRequestLineDto
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
}
