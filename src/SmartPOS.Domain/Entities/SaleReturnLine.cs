using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class SaleReturnLine : AuditableEntity
{
    public Guid SaleReturnId { get; set; }
    public Guid OriginalSaleLineId { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityReturned { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public bool Restock { get; set; } = true;
    public string Disposition { get; set; } = "Restock";
    public string? LineReason { get; set; }

    public SaleReturn? SaleReturn { get; set; }
    public SaleLine? OriginalSaleLine { get; set; }
}
