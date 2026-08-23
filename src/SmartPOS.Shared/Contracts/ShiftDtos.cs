using System.ComponentModel.DataAnnotations;

namespace SmartPOS.Shared.Contracts;

public sealed class ShiftContextDto
{
    public Guid ShiftId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public Guid RegisterId { get; set; }
    public string RegisterName { get; set; } = string.Empty;
    public decimal OpeningFloat { get; set; }
    public decimal CurrentCashBalance { get; set; }
    public DateTime OpenedOn { get; set; }
}

public sealed class ShiftListItemDto
{
    public Guid Id { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public string RegisterName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string OpenedByName { get; set; } = string.Empty;
    public string? ClosedByName { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal OpeningFloat { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Variance { get; set; }
    public DateTime OpenedOn { get; set; }
    public DateTime? ClosedOn { get; set; }
}

public sealed class ShiftQueryDto
{
    public Guid? StoreId { get; set; }
    public Guid? RegisterId { get; set; }
    public bool OpenOnly { get; set; }
    public int Take { get; set; } = 50;
}

public sealed class OpenShiftDto
{
    [Required]
    public Guid RegisterId { get; set; }

    [Range(0, 99999999)]
    public decimal OpeningFloat { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class CashDenominationLineDto
{
    public decimal DenominationValue { get; set; }
    public int Quantity { get; set; }
    public decimal Amount => DenominationValue * Quantity;
    public int SortOrder { get; set; }
}

public sealed class CloseShiftDto
{
    [Required]
    public Guid ShiftId { get; set; }

    [Range(0, 99999999)]
    public decimal CountedCash { get; set; }

    public List<CashDenominationLineDto> Denominations { get; set; } = [];

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class RecordCashMovementDto
{
    [Required]
    public Guid ShiftId { get; set; }

    [Required]
    public string MovementType { get; set; } = "CashIn";

    [Range(0.01, 99999999)]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}


public sealed class NoSaleDrawerDto
{
    [Required]
    public Guid ShiftId { get; set; }

    [Required]
    [StringLength(300, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ShiftReportDto
{
    public Guid ShiftId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public string RegisterName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string OpenedByName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OpenedOn { get; set; }
    public DateTime? ClosedOn { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal CashSales { get; set; }
    public decimal CashRefunds { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public int NoSaleDrawerOpenCount { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Variance { get; set; }
    public int SaleCount { get; set; }
    public decimal CardSales { get; set; }
    public decimal CreditSales { get; set; }
    public decimal GrossSales { get; set; }
    public IReadOnlyList<CashDenominationLineDto> Denominations { get; set; } = [];
}

public sealed class RegisterOptionDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Display { get; set; } = string.Empty;
    public bool HasCashDrawer { get; set; }
    public bool HasOpenShift { get; set; }
}
