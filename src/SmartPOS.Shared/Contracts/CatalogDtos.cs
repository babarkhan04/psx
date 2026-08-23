using System.ComponentModel.DataAnnotations;

namespace SmartPOS.Shared.Contracts;

public sealed class LookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CategoryDto
{
    public Guid Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int ProductCount { get; set; }
}

public sealed class BrandDto
{
    public Guid Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int ProductCount { get; set; }
}

public sealed class UnitDto
{
    public Guid Id { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Abbreviation { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int ProductCount { get; set; }
}

public sealed class TaxCategoryDto
{
    public Guid Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100)]
    public decimal Rate { get; set; }

    public bool IsActive { get; set; } = true;
    public int ProductCount { get; set; }
}

public sealed class ProductListItemDto
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? CategoryName { get; set; }
    public string? BrandName { get; set; }
    public string? UnitAbbreviation { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellPrice { get; set; }
    public decimal? TaxRate { get; set; }
    public bool IsActive { get; set; }
    public int VariantCount { get; set; }
    public bool IsAssignedToSelectedStore { get; set; }
}

public sealed class ProductVariantDto
{
    public Guid Id { get; set; }

    [Required, StringLength(60)]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(60)]
    public string? Barcode { get; set; }

    public decimal? CostPrice { get; set; }
    public decimal? SellPrice { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductImportResult
{
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public IList<string> Errors { get; init; } = [];
}

public sealed class ProductStorePriceDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal? Price { get; set; }
}

public sealed class ProductStoreAssignmentDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public bool IsVisibleOnPos { get; set; }
}

public sealed class ProductDto
{
    public Guid Id { get; set; }

    [Required, StringLength(60)]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(250)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(60)]
    public string? Barcode { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? TaxCategoryId { get; set; }

    [Range(0, 9999999)]
    public decimal CostPrice { get; set; }

    [Range(0, 9999999)]
    public decimal SellPrice { get; set; }

    [Range(0, 9999999)]
    public decimal ReorderLevel { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public IList<string> AdditionalBarcodes { get; set; } = [];
    public IList<ProductStorePriceDto> StorePrices { get; set; } = [];
    public IList<ProductStoreAssignmentDto> StoreAssignments { get; set; } = [];
    public IList<ProductVariantDto> Variants { get; set; } = [];
}
