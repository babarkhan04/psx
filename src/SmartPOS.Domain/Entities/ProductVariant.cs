using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>
/// A sellable variant of a base product (e.g. size, color, pack size).
/// </summary>
public class ProductVariant : AuditableEntity
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SellPrice { get; set; }
    public bool IsActive { get; set; } = true;

    public Product? Product { get; set; }
}
