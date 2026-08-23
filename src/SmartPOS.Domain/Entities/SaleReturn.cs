using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

public class SaleReturn : StoreScopedEntity
{
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid OriginalSaleId { get; set; }
    public Guid RegisterId { get; set; }
    public Guid? ShiftId { get; set; }
    public string ProcessedByUserId { get; set; } = string.Empty;
    public SaleReturnType ReturnType { get; set; } = SaleReturnType.Refund;
    public SaleReturnStatus Status { get; set; } = SaleReturnStatus.Completed;
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethod RefundMethod { get; set; }
    public string ReturnReason { get; set; } = string.Empty;
    public string? RefundReference { get; set; }
    public string? ExchangeReference { get; set; }
    public string? Notes { get; set; }
    public DateTime ProcessedOn { get; set; }

    public Sale? OriginalSale { get; set; }
    public Shift? Shift { get; set; }
    public ICollection<SaleReturnLine> Lines { get; set; } = [];
}
