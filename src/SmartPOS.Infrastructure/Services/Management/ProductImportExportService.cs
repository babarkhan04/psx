using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class ProductImportExportService(AppDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser) : IProductImportExportService
{
    private static readonly string[] Headers =
    [
        "SKU", "Name", "Description", "Barcode", "Category", "Brand", "Unit",
        "CostPrice", "SellPrice", "ReorderLevel", "TaxCategory", "IsActive"
    ];

    public async Task<byte[]> ExportAsync(CancellationToken ct = default)
    {
        var products = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Unit)
            .Include(p => p.TaxCategory)
            .OrderBy(p => p.Sku)
            .ToListAsync(ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Products");

        for (var i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var p in products)
        {
            sheet.Cell(row, 1).Value = p.Sku;
            sheet.Cell(row, 2).Value = p.Name;
            sheet.Cell(row, 3).Value = p.Description ?? string.Empty;
            sheet.Cell(row, 4).Value = p.Barcode ?? string.Empty;
            sheet.Cell(row, 5).Value = p.Category?.Name ?? string.Empty;
            sheet.Cell(row, 6).Value = p.Brand?.Name ?? string.Empty;
            sheet.Cell(row, 7).Value = p.Unit?.Name ?? string.Empty;
            sheet.Cell(row, 8).Value = p.CostPrice;
            sheet.Cell(row, 9).Value = p.SellPrice;
            sheet.Cell(row, 10).Value = p.ReorderLevel;
            sheet.Cell(row, 11).Value = p.TaxCategory?.Name ?? string.Empty;
            sheet.Cell(row, 12).Value = p.IsActive ? "Yes" : "No";
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<Result<ProductImportResult>> ImportAsync(Stream file, Guid? assignToStoreId = null, CancellationToken ct = default)
    {
        var errors = new List<string>();

        // Blazor BrowserFileStream does not support synchronous reads, while ClosedXML/ZIP
        // performs synchronous reads while opening the workbook. Copy the upload stream
        // into a seekable MemoryStream asynchronously first, then let ClosedXML read it.
        await using var uploadBuffer = new MemoryStream();
        await file.CopyToAsync(uploadBuffer, ct);
        uploadBuffer.Position = 0;

        using var workbook = new XLWorkbook(uploadBuffer);
        var sheet = workbook.Worksheets.First();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        var categories = await db.Categories.ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        var brands = await db.Brands.ToDictionaryAsync(b => b.Name, b => b.Id, StringComparer.OrdinalIgnoreCase, ct);
        var units = await db.Units.ToDictionaryAsync(u => u.Name, u => u.Id, StringComparer.OrdinalIgnoreCase, ct);
        var taxes = await db.TaxCategories.ToDictionaryAsync(t => t.Name, t => t.Id, StringComparer.OrdinalIgnoreCase, ct);
        var existingProducts = await db.Products.IgnoreQueryFilters()
            .Where(p => !p.IsDeleted)
            .ToDictionaryAsync(p => p.Sku, StringComparer.OrdinalIgnoreCase, ct);
        Store? assignToStore = null;
        if (assignToStoreId is not null)
        {
            assignToStore = await db.Stores.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == assignToStoreId && s.IsActive, ct);
            if (assignToStore is null)
            {
                return Result<ProductImportResult>.Failure("Selected store was not found or is inactive.", "STORE_NOT_FOUND");
            }
        }

        var importedBarcodeBySku = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var r = 2; r <= lastRow; r++)
        {
            var sku = sheet.Cell(r, 1).GetString().Trim();
            var barcode = NormalizeBarcode(sheet.Cell(r, 4).GetString());

            if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(barcode))
            {
                continue;
            }

            if (importedBarcodeBySku.TryGetValue(barcode, out var otherSku) && !string.Equals(otherSku, sku, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Row {r}: Barcode '{barcode}' is also used by SKU '{otherSku}' in this import file.");
            }
            else
            {
                importedBarcodeBySku[barcode] = sku;
            }
        }

        if (errors.Count > 0)
        {
            return Result<ProductImportResult>.Success(new ProductImportResult
            {
                Created = 0,
                Updated = 0,
                Skipped = Math.Max(0, lastRow - 1),
                Errors = errors
            });
        }

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var importedProducts = new List<Product>();

        for (var r = 2; r <= lastRow; r++)
        {
            var sku = sheet.Cell(r, 1).GetString().Trim();
            var name = sheet.Cell(r, 2).GetString().Trim();

            if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"Row {r}: SKU and Name are required.");
                skipped++;
                continue;
            }

            var barcode = NormalizeBarcode(sheet.Cell(r, 4).GetString());
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                var barcodeError = await ValidateImportedPrimaryBarcodeAsync(sku, barcode, ct);
                if (barcodeError is not null)
                {
                    errors.Add($"Row {r}: {barcodeError}");
                    skipped++;
                    continue;
                }
            }

            var categoryName = sheet.Cell(r, 5).GetString().Trim();
            var brandName = sheet.Cell(r, 6).GetString().Trim();
            var unitName = sheet.Cell(r, 7).GetString().Trim();
            var taxName = sheet.Cell(r, 11).GetString().Trim();

            Guid? categoryId = !string.IsNullOrWhiteSpace(categoryName) && categories.TryGetValue(categoryName, out var cid) ? cid : null;
            Guid? brandId = !string.IsNullOrWhiteSpace(brandName) && brands.TryGetValue(brandName, out var bid) ? bid : null;
            Guid? unitId = !string.IsNullOrWhiteSpace(unitName) && units.TryGetValue(unitName, out var uid) ? uid : null;
            Guid? taxId = !string.IsNullOrWhiteSpace(taxName) && taxes.TryGetValue(taxName, out var tid) ? tid : null;

            if (!string.IsNullOrWhiteSpace(categoryName) && categoryId is null)
            {
                errors.Add($"Row {r}: Category '{categoryName}' not found — create it in Catalog Setup first.");
                skipped++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(brandName) && brandId is null)
            {
                errors.Add($"Row {r}: Brand '{brandName}' not found — create it in Catalog Setup first.");
                skipped++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(unitName) && unitId is null)
            {
                errors.Add($"Row {r}: Unit '{unitName}' not found — create it in Catalog Setup first.");
                skipped++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(taxName) && taxId is null)
            {
                errors.Add($"Row {r}: Tax category '{taxName}' not found — create it in Catalog Setup first.");
                skipped++;
                continue;
            }

            var cost = ParseDecimal(sheet.Cell(r, 8));
            var sell = ParseDecimal(sheet.Cell(r, 9));
            var reorder = ParseDecimal(sheet.Cell(r, 10));
            var isActive = !sheet.Cell(r, 12).GetString().Equals("No", StringComparison.OrdinalIgnoreCase);

            if (existingProducts.TryGetValue(sku, out var product))
            {
                product.IsDeleted = false;
                product.Name = name;
                product.Description = sheet.Cell(r, 3).GetString().Trim();
                product.Barcode = barcode;
                product.CategoryId = categoryId;
                product.BrandId = brandId;
                product.UnitId = unitId;
                product.TaxCategoryId = taxId;
                product.CostPrice = cost;
                product.SellPrice = sell;
                product.ReorderLevel = reorder;
                product.IsActive = isActive;
                importedProducts.Add(product);
                updated++;
            }
            else
            {
                var entity = new Product
                {
                    Sku = sku,
                    Name = name,
                    Description = sheet.Cell(r, 3).GetString().Trim(),
                    Barcode = barcode,
                    CategoryId = categoryId,
                    BrandId = brandId,
                    UnitId = unitId,
                    TaxCategoryId = taxId,
                    CostPrice = cost,
                    SellPrice = sell,
                    ReorderLevel = reorder,
                    IsActive = isActive
                };
                db.Products.Add(entity);
                existingProducts[sku] = entity;
                importedProducts.Add(entity);
                created++;
            }
        }

        if (assignToStore is not null && importedProducts.Count > 0)
        {
            await AssignImportedProductsToStoreAsync(importedProducts, assignToStore, ct);
        }

        await db.SaveChangesAsync(ct);

        return Result<ProductImportResult>.Success(new ProductImportResult
        {
            Created = created,
            Updated = updated,
            Skipped = skipped,
            Errors = errors
        });
    }

    private async Task AssignImportedProductsToStoreAsync(IReadOnlyList<Product> products, Store store, CancellationToken ct)
    {
        var productIds = products.Select(p => p.Id).Distinct().ToList();
        var existing = await db.ProductStoreAssignments
            .IgnoreQueryFilters()
            .Where(a => a.StoreId == store.Id && productIds.Contains(a.ProductId))
            .ToListAsync(ct);

        foreach (var row in existing)
        {
            row.IsDeleted = false;
            row.IsVisibleOnPos = true;
            if (row.AssignedOn == default)
            {
                row.AssignedOn = clock.UtcNow;
            }
            row.AssignedByUserId = currentUser.UserId;
        }

        var existingIds = existing.Select(a => a.ProductId).ToHashSet();
        foreach (var product in products.Where(p => !existingIds.Contains(p.Id)))
        {
            db.ProductStoreAssignments.Add(new ProductStoreAssignment
            {
                TenantId = store.TenantId,
                StoreId = store.Id,
                ProductId = product.Id,
                IsVisibleOnPos = true,
                AssignedOn = clock.UtcNow,
                AssignedByUserId = currentUser.UserId
            });
        }
    }

    private async Task<string?> ValidateImportedPrimaryBarcodeAsync(string currentSku, string barcode, CancellationToken ct)
    {
        var productClash = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => !p.IsDeleted && p.Barcode == barcode && p.Sku != currentSku)
            .Select(p => new { p.Sku, p.Name })
            .FirstOrDefaultAsync(ct);

        if (productClash is not null)
        {
            return $"Barcode '{barcode}' already exists as the primary barcode of product '{productClash.Sku} - {productClash.Name}'.";
        }

        var additionalClash = await db.ProductBarcodes.IgnoreQueryFilters().AsNoTracking()
            .Where(b => !b.IsDeleted && b.Code == barcode)
            .Select(b => new { b.Code, ProductSku = b.Product != null ? b.Product.Sku : "Unknown", ProductName = b.Product != null ? b.Product.Name : "Unknown" })
            .FirstOrDefaultAsync(ct);

        if (additionalClash is not null)
        {
            return $"Barcode '{barcode}' already exists as an additional barcode of product '{additionalClash.ProductSku} - {additionalClash.ProductName}'.";
        }

        var variantClash = await db.ProductVariants.IgnoreQueryFilters().AsNoTracking()
            .Where(v => !v.IsDeleted && v.Barcode == barcode)
            .Select(v => new
            {
                v.Sku,
                v.Name,
                ProductSku = v.Product != null ? v.Product.Sku : "Unknown",
                ProductName = v.Product != null ? v.Product.Name : "Unknown"
            })
            .FirstOrDefaultAsync(ct);

        if (variantClash is not null)
        {
            return $"Barcode '{barcode}' already exists on variant '{variantClash.Sku} - {variantClash.Name}' of product '{variantClash.ProductSku} - {variantClash.ProductName}'.";
        }

        return null;
    }

    private static decimal ParseDecimal(IXLCell cell)
    {
        if (cell.TryGetValue(out decimal d))
        {
            return d;
        }

        if (cell.TryGetValue(out double dbl))
        {
            return (decimal)dbl;
        }

        return decimal.TryParse(cell.GetString(), out var parsed) ? parsed : 0m;
    }

    private static string? NormalizeBarcode(string? barcode) =>
        string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
}
