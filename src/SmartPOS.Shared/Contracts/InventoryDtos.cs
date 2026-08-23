using System.ComponentModel.DataAnnotations;

namespace SmartPOS.Shared.Contracts;

public sealed class StockListItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
}

public sealed class StockLedgerDto
{
    public Guid Id { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal QuantityAfter { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public sealed class StockAdjustmentLineDto
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
}

public sealed class StockAdjustmentDto
{
    [Required]
    public Guid StoreId { get; set; }

    [Required]
    public string MovementType { get; set; } = "Adjustment";

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IList<StockAdjustmentLineDto> Lines { get; set; } = [];
}
