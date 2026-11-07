using System.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class ProductService(AppDbContext db, IStoreAccessService storeAccess, ITenantContext tenantContext, DbContextGate gate) : IProductService
{
    public Task<Result<Guid>> CreateAsync(ProductDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => CreateCoreAsync(dto, ct), ct);
    public Task<Result> UpdateAsync(ProductDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => UpdateCoreAsync(dto, ct), ct);
    public Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => SetActiveCoreAsync(id, isActive, ct), ct);
    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => DeleteCoreAsync(id, ct), ct);
    public Task<Result> AssignProductsToStoreAsync(IReadOnlyList<Guid> productIds, Guid storeId, bool isVisible, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => AssignProductsToStoreCoreAsync(productIds, storeId, isVisible, ct), ct);

    public async Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(string? search = null, Guid? storeId = null, bool assignedOnly = false, CancellationToken ct = default)
    {
        if ((await storeAccess.GetAllowedStoreIdsAsync(ct)).Count == 0)
            throw new UnauthorizedAccessException("No accessible store is available.");
        if (storeId is { } requestedStore)
            await storeAccess.EnsureCanAccessStoreAsync(requestedStore, ct);
        var query = db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Unit)
            .Include(p => p.TaxCategory)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") ||
                EF.Functions.Like(p.Sku, $"%{term}%") ||
                (p.Barcode != null && EF.Functions.Like(p.Barcode, $"%{term}%")) ||
                p.Barcodes.Any(b => EF.Functions.Like(b.Code, $"%{term}%")) ||
                p.Variants.Any(v => v.Barcode != null && EF.Functions.Like(v.Barcode, $"%{term}%")));
        }

        if (storeId is not null && assignedOnly)
        {
            query = query.Where(p => p.StoreAssignments.Any(a => a.StoreId == storeId && a.IsVisibleOnPos));
        }

        return await query.OrderBy(p => p.Name)
            .Select(p => new ProductListItemDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                Barcode = p.Barcode,
                CategoryName = p.Category != null ? p.Category.Name : null,
                BrandName = p.Brand != null ? p.Brand.Name : null,
                UnitAbbreviation = p.Unit != null ? p.Unit.Abbreviation : null,
                CostPrice = p.CostPrice,
                SellPrice = p.SellPrice,
                ReorderLevel = p.ReorderLevel,
                ImageUrl = p.ImageUrl,
                TaxRate = p.TaxCategory != null ? p.TaxCategory.Rate : (decimal?)null,
                IsActive = p.IsActive,
                VariantCount = p.Variants.Count(v => !v.IsDeleted),
                IsAssignedToSelectedStore = storeId != null && p.StoreAssignments.Any(a => a.StoreId == storeId && a.IsVisibleOnPos)
            })
            .ToListAsync(ct);
    }

    public async Task<ProductDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Barcodes)
            .Include(p => p.StorePrices)
            .Include(p => p.StoreAssignments)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product is null)
        {
            return null;
        }

        var allowed = (await storeAccess.GetAllowedStoreIdsAsync(ct)).ToHashSet();
        var result = MapToDto(product);
        result.StorePrices = result.StorePrices.Where(price => allowed.Contains(price.StoreId)).ToList();
        result.StoreAssignments = result.StoreAssignments.Where(assignment => allowed.Contains(assignment.StoreId)).ToList();
        return result;
    }

    private async Task<Result<Guid>> CreateCoreAsync(ProductDto dto, CancellationToken ct)
    {
        var validation = await ValidateAsync(dto, null, ct);
        if (validation is not null)
            return Result<Guid>.Failure(validation, "VALIDATION");
        var tenantId = tenantContext.TenantId!.Value;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var sku = dto.Sku.Trim();
            if (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenantId && p.Sku == sku, ct))
                return Result<Guid>.Failure($"SKU '{sku}' already exists, including archived products.", "DUPLICATE_SKU");
            var product = new Product { TenantId = tenantId };
            Apply(product, dto);
            SyncBarcodes(product, dto);
            db.Products.Add(product);
            await SyncStorePricesAsync(product.Id, tenantId, dto.StorePrices, ct);
            await SyncStoreAssignmentsAsync(product.Id, tenantId, dto.StoreAssignments, ct);
            await SyncVariantsAsync(product.Id, tenantId, dto, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<Guid>.Success(product.Id);
        });
    }

    private async Task<Result> UpdateCoreAsync(ProductDto dto, CancellationToken ct)
    {
        var initialValidation = await ValidateAsync(dto, dto.Id, ct);
        if (initialValidation is not null)
            return Result.Failure(initialValidation, "VALIDATION");
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            db.ChangeTracker.Clear();

            var product = await db.Products
                .Include(p => p.Barcodes)
                .FirstOrDefaultAsync(p => p.Id == dto.Id, ct);

            if (product is null)
            {
                return Result.Failure("Product not found.", "NOT_FOUND");
            }

            var sku = dto.Sku.Trim();
            if (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenantContext.TenantId && p.Sku == sku && p.Id != dto.Id, ct))
            {
                return Result.Failure($"A product with SKU '{sku}' already exists.", "DUPLICATE_SKU");
            }

            var validationError = await ValidateAsync(dto, dto.Id, ct);
            if (validationError is not null)
            {
                return Result.Failure(validationError, "VALIDATION");
            }

            Apply(product, dto);
            SyncBarcodes(product, dto);
            await SyncStorePricesAsync(product.Id, product.TenantId, dto.StorePrices, ct);
            await SyncStoreAssignmentsAsync(product.Id, product.TenantId, dto.StoreAssignments, ct);
            await SyncVariantsAsync(product.Id, product.TenantId, dto, ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }

    private async Task<Result> SetActiveCoreAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return Result.Failure("Product not found.", "NOT_FOUND");
        }

        product.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> DeleteCoreAsync(Guid id, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return Result.Failure("Product not found.", "NOT_FOUND");
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> AssignProductsToStoreCoreAsync(IReadOnlyList<Guid> productIds, Guid storeId, bool isVisible, CancellationToken ct = default)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        if (productIds.Count == 0)
        {
            return Result.Failure("No products selected for store assignment.", "NO_PRODUCTS");
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId && s.IsActive, ct);
        if (store is null)
        {
            return Result.Failure("Store not found or inactive.", "STORE_NOT_FOUND");
        }

        var tenantId = store.TenantId;
        var ids = productIds.Distinct().ToList();
        if (await db.Products.CountAsync(product => ids.Contains(product.Id), ct) != ids.Count)
            return Result.Failure("One or more products do not belong to the active company.", "INVALID_PRODUCT");
        var existing = await db.ProductStoreAssignments
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.StoreId == storeId && ids.Contains(a.ProductId))
            .ToListAsync(ct);

        foreach (var row in existing)
        {
            row.IsDeleted = false;
            row.IsVisibleOnPos = isVisible;
            if (isVisible && row.AssignedOn == default)
            {
                row.AssignedOn = DateTime.UtcNow;
            }
        }

        var existingIds = existing.Select(a => a.ProductId).ToHashSet();
        foreach (var productId in ids.Where(id => !existingIds.Contains(id)))
        {
            db.ProductStoreAssignments.Add(new ProductStoreAssignment
            {
                TenantId = tenantId,
                ProductId = productId,
                StoreId = storeId,
                IsVisibleOnPos = isVisible,
                AssignedOn = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }


    private static ProductDto MapToDto(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Description = product.Description,
        Barcode = product.Barcode,
        CategoryId = product.CategoryId,
        BrandId = product.BrandId,
        UnitId = product.UnitId,
        TaxCategoryId = product.TaxCategoryId,
        CostPrice = product.CostPrice,
        SellPrice = product.SellPrice,
        ReorderLevel = product.ReorderLevel,
        ImageUrl = product.ImageUrl,
        IsActive = product.IsActive,
        AdditionalBarcodes = product.Barcodes
            .Where(b => !b.IsDeleted)
            .Select(b => b.Code)
            .ToList(),
        StorePrices = product.StorePrices
            .Where(sp => !sp.IsDeleted)
            .Select(sp => new ProductStorePriceDto { StoreId = sp.StoreId, Price = sp.Price })
            .ToList(),
        StoreAssignments = product.StoreAssignments
            .Where(sa => !sa.IsDeleted)
            .Select(sa => new ProductStoreAssignmentDto { StoreId = sa.StoreId, IsVisibleOnPos = sa.IsVisibleOnPos })
            .ToList(),
        Variants = product.Variants
            .Where(v => !v.IsDeleted)
            .Select(v => new ProductVariantDto
            {
                Id = v.Id,
                Sku = v.Sku,
                Name = v.Name,
                Barcode = v.Barcode,
                CostPrice = v.CostPrice,
                SellPrice = v.SellPrice,
                IsActive = v.IsActive
            })
            .ToList()
    };

    private static void Apply(Product product, ProductDto dto)
    {
        product.Sku = dto.Sku.Trim();
        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.Barcode = NormalizeBarcode(dto.Barcode);
        product.CategoryId = dto.CategoryId;
        product.BrandId = dto.BrandId;
        product.UnitId = dto.UnitId;
        product.TaxCategoryId = dto.TaxCategoryId;
        product.CostPrice = dto.CostPrice;
        product.SellPrice = dto.SellPrice;
        product.ReorderLevel = dto.ReorderLevel;
        product.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim();
        product.IsActive = dto.IsActive;
    }

    private void SyncBarcodes(Product product, ProductDto dto)
    {
        var desired = dto.AdditionalBarcodes
            .Select(NormalizeBarcode)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()!;

        foreach (var existing in product.Barcodes.Where(b => !desired.Contains(b.Code, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            db.ProductBarcodes.Remove(existing);
        }

        foreach (var code in desired.Where(c => !product.Barcodes.Any(b => string.Equals(b.Code, c, StringComparison.OrdinalIgnoreCase))))
        {
            product.Barcodes.Add(new ProductBarcode { Code = code });
        }
    }

    /// <summary>
    /// Upserts store price overrides via direct DB access (including soft-deleted rows)
    /// so Blazor circuit reuse and unique-index collisions cannot cause concurrency errors.
    /// </summary>
    private async Task SyncStorePricesAsync(
        Guid productId,
        Guid tenantId,
        IEnumerable<ProductStorePriceDto> storePrices,
        CancellationToken ct)
    {
        var desired = storePrices
            .Where(sp => sp.Price.HasValue)
            .ToDictionary(sp => sp.StoreId, sp => sp.Price!.Value);

        var allowed = await storeAccess.GetAllowedStoreIdsAsync(ct);
        var existing = await db.ProductPrices
            .IgnoreQueryFilters()
            .Where(pp => pp.TenantId == tenantId && pp.ProductId == productId && allowed.Contains(pp.StoreId))
            .ToListAsync(ct);

        foreach (var row in existing)
        {
            if (desired.TryGetValue(row.StoreId, out var price))
            {
                row.IsDeleted = false;
                row.Price = price;
            }
            else if (!row.IsDeleted)
            {
                row.IsDeleted = true;
            }
        }

        foreach (var (storeId, price) in desired)
        {
            if (existing.Any(e => e.StoreId == storeId))
            {
                continue;
            }

            db.ProductPrices.Add(new ProductPrice
            {
                ProductId = productId,
                TenantId = tenantId,
                StoreId = storeId,
                Price = price
            });
        }
    }

    private async Task SyncStoreAssignmentsAsync(
        Guid productId,
        Guid tenantId,
        IEnumerable<ProductStoreAssignmentDto> assignments,
        CancellationToken ct)
    {
        var desired = assignments
            .GroupBy(a => a.StoreId)
            .ToDictionary(g => g.Key, g => g.Last().IsVisibleOnPos);

        var allowed = await storeAccess.GetAllowedStoreIdsAsync(ct);
        var existing = await db.ProductStoreAssignments
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.ProductId == productId && allowed.Contains(a.StoreId))
            .ToListAsync(ct);

        foreach (var row in existing)
        {
            if (desired.TryGetValue(row.StoreId, out var isVisible))
            {
                row.IsDeleted = false;
                row.IsVisibleOnPos = isVisible;
            }
            else if (!row.IsDeleted)
            {
                row.IsVisibleOnPos = false;
            }
        }

        foreach (var (storeId, isVisible) in desired)
        {
            if (existing.Any(e => e.StoreId == storeId))
            {
                continue;
            }

            db.ProductStoreAssignments.Add(new ProductStoreAssignment
            {
                ProductId = productId,
                TenantId = tenantId,
                StoreId = storeId,
                IsVisibleOnPos = isVisible,
                AssignedOn = DateTime.UtcNow
            });
        }
    }

    private async Task SyncVariantsAsync(Guid productId, Guid tenantId, ProductDto dto, CancellationToken ct)
    {
        var desired = dto.Variants
            .Where(v => !string.IsNullOrWhiteSpace(v.Sku) && !string.IsNullOrWhiteSpace(v.Name))
            .ToList();

        var existing = await db.ProductVariants
            .IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId && v.ProductId == productId)
            .ToListAsync(ct);

        var keepIds = desired.Where(v => v.Id != Guid.Empty).Select(v => v.Id).ToHashSet();

        foreach (var row in existing.Where(v => !keepIds.Contains(v.Id)))
        {
            row.IsDeleted = true;
        }

        foreach (var variant in desired)
        {
            var row = variant.Id != Guid.Empty
                ? existing.FirstOrDefault(v => v.Id == variant.Id)
                : null;

            if (row is null)
            {
                db.ProductVariants.Add(new ProductVariant
                {
                    ProductId = productId,
                    TenantId = tenantId,
                    Sku = variant.Sku.Trim(),
                    Name = variant.Name.Trim(),
                    Barcode = NormalizeBarcode(variant.Barcode),
                    CostPrice = variant.CostPrice,
                    SellPrice = variant.SellPrice,
                    IsActive = variant.IsActive
                });
            }
            else
            {
                row.IsDeleted = false;
                row.Sku = variant.Sku.Trim();
                row.Name = variant.Name.Trim();
                row.Barcode = NormalizeBarcode(variant.Barcode);
                row.CostPrice = variant.CostPrice;
                row.SellPrice = variant.SellPrice;
                row.IsActive = variant.IsActive;
            }
        }
    }

    private async Task<string?> ValidateAsync(ProductDto dto, Guid? productId, CancellationToken ct)
    {
        if (tenantContext.TenantId is null || (await storeAccess.GetAllowedStoreIdsAsync(ct)).Count == 0)
            return "No accessible company or store is available.";
        dto.StorePrices ??= [];
        dto.StoreAssignments ??= [];
        dto.Variants ??= [];
        dto.AdditionalBarcodes ??= [];
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true))
            return errors.First().ErrorMessage;
        var allowed = (await storeAccess.GetAllowedStoreIdsAsync(ct)).ToHashSet();
        if (dto.StorePrices.Any(price => !allowed.Contains(price.StoreId)) ||
            dto.StoreAssignments.Any(assignment => !allowed.Contains(assignment.StoreId)))
            return "You cannot change prices or availability for an unassigned store.";
        if (dto.StorePrices.GroupBy(price => price.StoreId).Any(group => group.Count() > 1) ||
            dto.StorePrices.Any(price => price.Price is < 0 or > 9999999m))
            return "Store prices must be unique per store and between zero and 9,999,999.";
        if (dto.CategoryId is { } categoryId && !await db.Categories.AnyAsync(x => x.Id == categoryId, ct) ||
            dto.BrandId is { } brandId && !await db.Brands.AnyAsync(x => x.Id == brandId, ct) ||
            dto.UnitId is { } unitId && !await db.Units.AnyAsync(x => x.Id == unitId, ct) ||
            dto.TaxCategoryId is { } taxId && !await db.TaxCategories.AnyAsync(x => x.Id == taxId, ct))
            return "A selected catalog value does not belong to the active company.";
        foreach (var variant in dto.Variants)
        {
            if (variant is null || !Validator.TryValidateObject(variant, new ValidationContext(variant), errors, true) ||
                variant.CostPrice is < 0 or > 9999999m || variant.SellPrice is < 0 or > 9999999m)
                return "Enter a valid SKU, name, and non-negative prices for every variant.";
        }

        var barcodeError = await ValidateBarcodesAsync(dto, productId, ct);
        if (barcodeError is not null)
        {
            return barcodeError;
        }

        var variantSkus = dto.Variants
            .Where(v => !string.IsNullOrWhiteSpace(v.Sku))
            .Select(v => v.Sku.Trim())
            .ToList();

        if (variantSkus.Count != variantSkus.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return "Duplicate variant SKU within this product.";
        }

        foreach (var sku in variantSkus)
        {
            if (await db.ProductVariants.IgnoreQueryFilters()
                    .AnyAsync(v => v.TenantId == tenantContext.TenantId && v.Sku == sku && v.ProductId != productId, ct))
            {
                return $"Variant SKU '{sku}' is already used by another product.";
            }
        }

        return null;
    }

    private async Task<string?> ValidateBarcodesAsync(ProductDto dto, Guid? productId, CancellationToken ct)
    {
        var codes = new List<string>();

        if (!string.IsNullOrWhiteSpace(dto.Barcode))
        {
            codes.Add(NormalizeBarcode(dto.Barcode)!);
        }

        codes.AddRange(dto.AdditionalBarcodes
            .Select(NormalizeBarcode)
            .Where(b => !string.IsNullOrWhiteSpace(b))!);

        codes.AddRange(dto.Variants
            .Select(v => NormalizeBarcode(v.Barcode))
            .Where(b => !string.IsNullOrWhiteSpace(b))!);

        codes = codes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (codes.Count == 0)
        {
            return null;
        }

        var rawCodes = new List<string>();
        if (!string.IsNullOrWhiteSpace(dto.Barcode)) rawCodes.Add(dto.Barcode.Trim());
        rawCodes.AddRange(dto.AdditionalBarcodes.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)));
        rawCodes.AddRange(dto.Variants.Select(x => x.Barcode?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))!);

        var duplicates = rawCodes.GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            return $"Duplicate barcode(s) in this product: {string.Join(", ", duplicates)}.";
        }

        // Product primary barcodes must not clash with another product.
        var productClash = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == tenantContext.TenantId && p.Id != productId && !p.IsDeleted && p.Barcode != null && codes.Contains(p.Barcode))
            .Select(p => new { p.Sku, p.Name, Code = p.Barcode! })
            .FirstOrDefaultAsync(ct);

        if (productClash is not null)
        {
            return $"Barcode '{productClash.Code}' is already used as the primary barcode for product '{productClash.Sku} - {productClash.Name}'.";
        }

        // Additional barcode clashes must also be detected.
        var additionalClash = await db.ProductBarcodes.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.TenantId == tenantContext.TenantId && b.ProductId != productId && !b.IsDeleted && codes.Contains(b.Code))
            .Select(b => new { b.Code, ProductSku = b.Product != null ? b.Product.Sku : "Unknown", ProductName = b.Product != null ? b.Product.Name : "Unknown" })
            .FirstOrDefaultAsync(ct);

        if (additionalClash is not null)
        {
            return $"Barcode '{additionalClash.Code}' is already used as an additional barcode for product '{additionalClash.ProductSku} - {additionalClash.ProductName}'.";
        }

        // Variant barcode clashes must be detected too, otherwise scanning can return the wrong item.
        var variantClash = await db.ProductVariants.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.TenantId == tenantContext.TenantId && v.ProductId != productId && !v.IsDeleted && v.Barcode != null && codes.Contains(v.Barcode))
            .Select(v => new
            {
                Code = v.Barcode!,
                VariantSku = v.Sku,
                VariantName = v.Name,
                ProductSku = v.Product != null ? v.Product.Sku : "Unknown",
                ProductName = v.Product != null ? v.Product.Name : "Unknown"
            })
            .FirstOrDefaultAsync(ct);

        if (variantClash is not null)
        {
            return $"Barcode '{variantClash.Code}' is already used by variant '{variantClash.VariantSku} - {variantClash.VariantName}' of product '{variantClash.ProductSku} - {variantClash.ProductName}'.";
        }

        return null;
    }

    private static string? NormalizeBarcode(string? barcode) =>
        string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
}
