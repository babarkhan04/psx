using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class Product : AuditableEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Primary scannable barcode. Additional barcodes live in <see cref="Barcodes"/>.</summary>
    public string? Barcode { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? TaxCategoryId { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SellPrice { get; set; }
    public decimal ReorderLevel { get; set; }

    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPosFavorite { get; set; }
    public int PosFavoriteSortOrder { get; set; }

    public Category? Category { get; set; }
    public Brand? Brand { get; set; }
    public UnitOfMeasure? Unit { get; set; }
    public TaxCategory? TaxCategory { get; set; }

    public ICollection<ProductBarcode> Barcodes { get; set; } = [];
    public ICollection<ProductPrice> StorePrices { get; set; } = [];
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<ProductStoreAssignment> StoreAssignments { get; set; } = [];
}
