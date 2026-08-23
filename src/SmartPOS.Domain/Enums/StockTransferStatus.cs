namespace SmartPOS.Domain.Enums;

public enum StockTransferStatus
{
    Draft = 1,
    Requested = 2,
    Approved = 3,
    Dispatched = 4,
    PartiallyReceived = 5,
    Received = 6,
    Cancelled = 7,
    Rejected = 8
}
