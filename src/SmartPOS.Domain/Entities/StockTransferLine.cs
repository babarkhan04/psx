using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class StockTransferLine : AuditableEntity
{
    public Guid StockTransferId { get; set; }
    public int LineNumber { get; set; }

    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }

    public decimal QuantityRequested { get; set; }
    public decimal QuantityDispatched { get; set; }
    public decimal QuantityReceived { get; set; }

    public StockTransfer? StockTransfer { get; set; }
    public Product? Product { get; set; }
    public ProductVariant? ProductVariant { get; set; }
}
