using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class SaleLine : AuditableEntity
{
    public Guid SaleId { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }

    public Sale? Sale { get; set; }
    public Product? Product { get; set; }
    public ProductVariant? ProductVariant { get; set; }
}
