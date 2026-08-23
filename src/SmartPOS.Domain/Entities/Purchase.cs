using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

public class Purchase : StoreScopedEntity
{
    public string PurchaseNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;
    public string ReceivedByUserId { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime? OrderedOn { get; set; }
    public DateTime? ReceivedOn { get; set; }

    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseLine> Lines { get; set; } = [];
}
