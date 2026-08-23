using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class PurchaseLine : AuditableEntity
{
    public Guid PurchaseId { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }

    public Purchase? Purchase { get; set; }
    public Product? Product { get; set; }
}
