using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class ReceiptReprintLog : StoreScopedEntity
{
    public Guid SaleId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public string ReprintedByUserId { get; set; } = string.Empty;
    public string ReprintedByName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime ReprintedOn { get; set; }

    public Sale? Sale { get; set; }
}
