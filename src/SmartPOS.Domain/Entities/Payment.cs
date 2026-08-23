using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

public class Payment : AuditableEntity
{
    public Guid SaleId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public DateTime ReceivedOn { get; set; }

    public Sale? Sale { get; set; }
}
