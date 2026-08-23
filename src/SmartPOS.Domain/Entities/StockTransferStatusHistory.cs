using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

public class StockTransferStatusHistory : AuditableEntity
{
    public Guid StockTransferId { get; set; }
    public StockTransferStatus? FromStatus { get; set; }
    public StockTransferStatus ToStatus { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedOn { get; set; }
    public string? Notes { get; set; }

    public StockTransfer? StockTransfer { get; set; }
}
