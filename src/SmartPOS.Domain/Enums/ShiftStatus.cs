namespace SmartPOS.Domain.Enums;

public enum ShiftStatus
{
    Open = 1,
    Closed = 2
}

public enum CashMovementType
{
    OpeningFloat = 1,
    CashIn = 2,
    CashOut = 3,
    Sale = 4,
    Refund = 5,
    Adjustment = 6,
    NoSaleDrawer = 7
}

public enum SaleReturnType
{
    Refund = 1,
    Exchange = 2
}

public enum SaleReturnStatus
{
    Completed = 1,
    Voided = 2
}
