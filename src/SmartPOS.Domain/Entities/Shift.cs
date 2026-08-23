using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

public class Shift : StoreScopedEntity
{
    public Guid RegisterId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public string OpenedByUserId { get; set; } = string.Empty;
    public string? ClosedByUserId { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public decimal OpeningFloat { get; set; }
    public DateTime OpenedOn { get; set; }
    public DateTime? ClosedOn { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Variance { get; set; }
    public string? OpeningNotes { get; set; }
    public string? ClosingNotes { get; set; }

    public Register? Register { get; set; }
    public ICollection<CashMovement> Movements { get; set; } = [];
    public ICollection<CashDenominationCount> CashDenominationCounts { get; set; } = [];
    public ICollection<Sale> Sales { get; set; } = [];
}
