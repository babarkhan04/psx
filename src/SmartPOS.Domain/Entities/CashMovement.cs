using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

public class CashMovement : StoreScopedEntity
{
    public Guid ShiftId { get; set; }
    public CashMovementType MovementType { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredOn { get; set; }
    public Guid? SaleId { get; set; }
    public Guid? SaleReturnId { get; set; }

    public Shift? Shift { get; set; }
}
