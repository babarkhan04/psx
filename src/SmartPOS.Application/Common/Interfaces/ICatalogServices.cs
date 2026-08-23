using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(CategoryDto dto, CancellationToken ct = default);
    Task<Result> UpdateAsync(CategoryDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IBrandService
{
    Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(BrandDto dto, CancellationToken ct = default);
    Task<Result> UpdateAsync(BrandDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IUnitService
{
    Task<IReadOnlyList<UnitDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(UnitDto dto, CancellationToken ct = default);
    Task<Result> UpdateAsync(UnitDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITaxCategoryService
{
    Task<IReadOnlyList<TaxCategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(TaxCategoryDto dto, CancellationToken ct = default);
    Task<Result> UpdateAsync(TaxCategoryDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IProductService
{
    Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(string? search = null, Guid? storeId = null, bool assignedOnly = false, CancellationToken ct = default);
    Task<ProductDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(ProductDto dto, CancellationToken ct = default);
    Task<Result> UpdateAsync(ProductDto dto, CancellationToken ct = default);
    Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result> AssignProductsToStoreAsync(IReadOnlyList<Guid> productIds, Guid storeId, bool isVisible, CancellationToken ct = default);
}

public interface IProductImportExportService
{
    Task<byte[]> ExportAsync(CancellationToken ct = default);
    Task<Result<ProductImportResult>> ImportAsync(Stream file, Guid? assignToStoreId = null, CancellationToken ct = default);
}
