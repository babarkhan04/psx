namespace SmartPOS.Domain.Enums;

public enum SaleStatus
{
    Completed = 1,
    Voided = 2,
    PartiallyReturned = 3,
    FullyReturned = 4
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    Other = 3,
    Credit = 4
}

public enum CustomerLedgerEntryType
{
    Opening = 1,
    Sale = 2,
    Payment = 3,
    Adjustment = 4,
    Refund = 5
}
