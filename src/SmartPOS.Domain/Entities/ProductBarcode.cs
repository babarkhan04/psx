using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class ProductBarcode : AuditableEntity
{
    public Guid ProductId { get; set; }
    public string Code { get; set; } = string.Empty;

    public Product? Product { get; set; }
}
