using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class CashDenominationCount : StoreScopedEntity
{
    public Guid ShiftId { get; set; }
    public decimal DenominationValue { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }

    public Shift? Shift { get; set; }
}
