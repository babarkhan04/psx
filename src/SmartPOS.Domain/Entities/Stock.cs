using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>Current on-hand quantity for a product (or variant) at a store.</summary>
public class Stock : StoreScopedEntity
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal QuantityOnHand { get; set; }

    public Product? Product { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public Store? Store { get; set; }
}
