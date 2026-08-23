using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>Optional store-specific selling price that overrides the product default.</summary>
public class ProductPrice : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public decimal Price { get; set; }

    public Product? Product { get; set; }
    public Store? Store { get; set; }
}
