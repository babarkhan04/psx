using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

public class HeldSale : StoreScopedEntity
{
    public string HeldNumber { get; set; } = string.Empty;
    public Guid RegisterId { get; set; }
    public Guid? ShiftId { get; set; }
    public string CashierUserId { get; set; } = string.Empty;
    public HeldSaleStatus Status { get; set; } = HeldSaleStatus.Open;

    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Walk-In Customer";

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }
    public DateTime HeldOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public DateTime? ExpiredOn { get; set; }
    public string? ExpiryReason { get; set; }
    public DateTime? RetrievedOn { get; set; }
    public string? RetrievedByUserId { get; set; }
    public DateTime? CancelledOn { get; set; }
    public string? CancelledByUserId { get; set; }

    public Register? Register { get; set; }
    public Shift? Shift { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<HeldSaleLine> Lines { get; set; } = [];
}
