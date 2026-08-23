using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

public class Sale : StoreScopedEntity
{
    public string SaleNumber { get; set; } = string.Empty;
    public Guid RegisterId { get; set; }
    public Guid? ShiftId { get; set; }
    public string CashierUserId { get; set; } = string.Empty;
    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Walk-In Customer";

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public string? DiscountApprovedByUserId { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeDue { get; set; }

    public string? Notes { get; set; }
    public DateTime CompletedOn { get; set; }

    public Register? Register { get; set; }
    public Shift? Shift { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<SaleLine> Lines { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<ReceiptReprintLog> ReceiptReprintLogs { get; set; } = [];
}
