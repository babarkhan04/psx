using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>
/// Controls which stores can see/sell a product in POS.
/// Master product data remains tenant-level, while this row makes the product visible for a specific store.
/// </summary>
public class ProductStoreAssignment : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public bool IsVisibleOnPos { get; set; } = true;
    public DateTime AssignedOn { get; set; }
    public string? AssignedByUserId { get; set; }

    public Product? Product { get; set; }
    public Store? Store { get; set; }
}
