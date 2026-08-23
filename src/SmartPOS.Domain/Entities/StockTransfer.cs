using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

public class StockTransfer : StoreScopedEntity
{
    public string TransferNumber { get; set; } = string.Empty;

    /// <summary>
    /// StoreId inherited from StoreScopedEntity is the source store.
    /// </summary>
    public Guid DestinationStoreId { get; set; }

    public StockTransferStatus Status { get; set; } = StockTransferStatus.Draft;

    public DateTime RequestedOn { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;

    public DateTime? ApprovedOn { get; set; }
    public string? ApprovedByUserId { get; set; }

    public DateTime? DispatchedOn { get; set; }
    public string? DispatchedByUserId { get; set; }

    public DateTime? ReceivedOn { get; set; }
    public string? ReceivedByUserId { get; set; }

    public DateTime? CancelledOn { get; set; }
    public string? CancelledByUserId { get; set; }

    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }

    public Store? SourceStore { get; set; }
    public Store? DestinationStore { get; set; }
    public ICollection<StockTransferLine> Lines { get; set; } = [];
    public ICollection<StockTransferStatusHistory> StatusHistory { get; set; } = [];
}
