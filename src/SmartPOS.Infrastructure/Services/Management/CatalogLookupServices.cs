using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class CategoryService(AppDbContext db, DbContextGate gate) : ICategoryService
{
    public Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var counts = await db.Products.AsNoTracking().Where(p => p.CategoryId != null)
                .GroupBy(p => p.CategoryId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

            var list = await db.Categories.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new CategoryDto { Id = x.Id, Name = x.Name, Description = x.Description, IsActive = x.IsActive })
                .ToListAsync(ct);

            foreach (var dto in list)
                dto.ProductCount = counts.GetValueOrDefault(dto.Id);

            return (IReadOnlyList<CategoryDto>)list;
        }, ct);

    public Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
            (IReadOnlyList<LookupDto>)await db.Categories.AsNoTracking()
                .Where(x => x.IsActive).OrderBy(x => x.Name)
                .Select(x => new LookupDto { Id = x.Id, Name = x.Name }).ToListAsync(ct),
        ct);

    public Task<Result<Guid>> CreateAsync(CategoryDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var name = NormalizeName(dto.Name);
            if (string.IsNullOrWhiteSpace(name))
                return Result<Guid>.Failure("Category name is required.", "VALIDATION");

            var existing = await db.Categories.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower(), ct);

            if (existing is not null)
            {
                if (!existing.IsDeleted)
                    return Result<Guid>.Failure($"A category named '{name}' already exists.", "DUPLICATE");

                existing.Name = name;
                existing.Description = dto.Description?.Trim();
                existing.IsActive = dto.IsActive;
                existing.IsDeleted = false;
                await db.SaveChangesAsync(ct);
                return Result<Guid>.Success(existing.Id);
            }

            var entity = new Category { Name = name, Description = dto.Description?.Trim(), IsActive = dto.IsActive };
            db.Categories.Add(entity);
            try
            {
                await db.SaveChangesAsync(ct);
                return Result<Guid>.Success(entity.Id);
            }
            catch (DbUpdateException)
            {
                return Result<Guid>.Failure($"A category named '{name}' already exists.", "DUPLICATE");
            }
        }, ct);

    public Task<Result> UpdateAsync(CategoryDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
            if (entity is null)
                return Result.Failure("Category not found.", "NOT_FOUND");

            var name = NormalizeName(dto.Name);
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure("Category name is required.", "VALIDATION");

            var duplicate = await db.Categories.IgnoreQueryFilters()
                .AnyAsync(x => x.Id != dto.Id && x.Name.ToLower() == name.ToLower(), ct);
            if (duplicate)
                return Result.Failure($"A category named '{name}' already exists.", "DUPLICATE");

            entity.Name = name;
            entity.Description = dto.Description?.Trim();
            entity.IsActive = dto.IsActive;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
                return Result.Failure("Category not found.", "NOT_FOUND");

            db.Categories.Remove(entity);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    private static string NormalizeName(string? value) => (value ?? string.Empty).Trim();
}

public sealed class BrandService(AppDbContext db, DbContextGate gate) : IBrandService
{
    public Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var counts = await db.Products.AsNoTracking().Where(p => p.BrandId != null)
                .GroupBy(p => p.BrandId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

            var list = await db.Brands.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new BrandDto { Id = x.Id, Name = x.Name, Description = x.Description, IsActive = x.IsActive })
                .ToListAsync(ct);

            foreach (var dto in list)
                dto.ProductCount = counts.GetValueOrDefault(dto.Id);

            return (IReadOnlyList<BrandDto>)list;
        }, ct);

    public Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
            (IReadOnlyList<LookupDto>)await db.Brands.AsNoTracking()
                .Where(x => x.IsActive).OrderBy(x => x.Name)
                .Select(x => new LookupDto { Id = x.Id, Name = x.Name }).ToListAsync(ct),
        ct);

    public Task<Result<Guid>> CreateAsync(BrandDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var name = NormalizeName(dto.Name);
            if (string.IsNullOrWhiteSpace(name))
                return Result<Guid>.Failure("Brand name is required.", "VALIDATION");

            var existing = await db.Brands.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower(), ct);

            if (existing is not null)
            {
                if (!existing.IsDeleted)
                    return Result<Guid>.Failure($"A brand named '{name}' already exists.", "DUPLICATE");

                existing.Name = name;
                existing.Description = dto.Description?.Trim();
                existing.IsActive = dto.IsActive;
                existing.IsDeleted = false;
                await db.SaveChangesAsync(ct);
                return Result<Guid>.Success(existing.Id);
            }

            var entity = new Brand { Name = name, Description = dto.Description?.Trim(), IsActive = dto.IsActive };
            db.Brands.Add(entity);
            try
            {
                await db.SaveChangesAsync(ct);
                return Result<Guid>.Success(entity.Id);
            }
            catch (DbUpdateException)
            {
                return Result<Guid>.Failure($"A brand named '{name}' already exists.", "DUPLICATE");
            }
        }, ct);

    public Task<Result> UpdateAsync(BrandDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var entity = await db.Brands.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
            if (entity is null)
                return Result.Failure("Brand not found.", "NOT_FOUND");

            var name = NormalizeName(dto.Name);
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure("Brand name is required.", "VALIDATION");

            var duplicate = await db.Brands.IgnoreQueryFilters()
                .AnyAsync(x => x.Id != dto.Id && x.Name.ToLower() == name.ToLower(), ct);
            if (duplicate)
                return Result.Failure($"A brand named '{name}' already exists.", "DUPLICATE");

            entity.Name = name;
            entity.Description = dto.Description?.Trim();
            entity.IsActive = dto.IsActive;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var entity = await db.Brands.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
                return Result.Failure("Brand not found.", "NOT_FOUND");

            db.Brands.Remove(entity);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    private static string NormalizeName(string? value) => (value ?? string.Empty).Trim();
}

public sealed class UnitService(AppDbContext db, DbContextGate gate) : IUnitService
{
    public Task<IReadOnlyList<UnitDto>> GetAllAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var counts = await db.Products.AsNoTracking().Where(p => p.UnitId != null)
                .GroupBy(p => p.UnitId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

            var list = await db.Units.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new UnitDto { Id = x.Id, Name = x.Name, Abbreviation = x.Abbreviation, IsActive = x.IsActive })
                .ToListAsync(ct);

            foreach (var dto in list)
                dto.ProductCount = counts.GetValueOrDefault(dto.Id);

            return (IReadOnlyList<UnitDto>)list;
        }, ct);

    public Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
            (IReadOnlyList<LookupDto>)await db.Units.AsNoTracking()
                .Where(x => x.IsActive).OrderBy(x => x.Name)
                .Select(x => new LookupDto { Id = x.Id, Name = x.Name + " (" + x.Abbreviation + ")" }).ToListAsync(ct),
        ct);

    public Task<Result<Guid>> CreateAsync(UnitDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var name = NormalizeName(dto.Name);
            var abbreviation = NormalizeName(dto.Abbreviation);
            if (string.IsNullOrWhiteSpace(name))
                return Result<Guid>.Failure("Unit name is required.", "VALIDATION");
            if (string.IsNullOrWhiteSpace(abbreviation))
                return Result<Guid>.Failure("Unit abbreviation is required.", "VALIDATION");

            var existing = await db.Units.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower(), ct);

            if (existing is not null)
            {
                if (!existing.IsDeleted)
                    return Result<Guid>.Failure($"A unit named '{name}' already exists.", "DUPLICATE");

                existing.Name = name;
                existing.Abbreviation = abbreviation;
                existing.IsActive = dto.IsActive;
                existing.IsDeleted = false;
                await db.SaveChangesAsync(ct);
                return Result<Guid>.Success(existing.Id);
            }

            var entity = new UnitOfMeasure { Name = name, Abbreviation = abbreviation, IsActive = dto.IsActive };
            db.Units.Add(entity);
            try
            {
                await db.SaveChangesAsync(ct);
                return Result<Guid>.Success(entity.Id);
            }
            catch (DbUpdateException)
            {
                return Result<Guid>.Failure($"A unit named '{name}' already exists.", "DUPLICATE");
            }
        }, ct);

    public Task<Result> UpdateAsync(UnitDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var entity = await db.Units.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
            if (entity is null)
                return Result.Failure("Unit not found.", "NOT_FOUND");

            var name = NormalizeName(dto.Name);
            var abbreviation = NormalizeName(dto.Abbreviation);
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure("Unit name is required.", "VALIDATION");
            if (string.IsNullOrWhiteSpace(abbreviation))
                return Result.Failure("Unit abbreviation is required.", "VALIDATION");

            var duplicate = await db.Units.IgnoreQueryFilters()
                .AnyAsync(x => x.Id != dto.Id && x.Name.ToLower() == name.ToLower(), ct);
            if (duplicate)
                return Result.Failure($"A unit named '{name}' already exists.", "DUPLICATE");

            entity.Name = name;
            entity.Abbreviation = abbreviation;
            entity.IsActive = dto.IsActive;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var entity = await db.Units.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
                return Result.Failure("Unit not found.", "NOT_FOUND");

            db.Units.Remove(entity);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    private static string NormalizeName(string? value) => (value ?? string.Empty).Trim();
}

public sealed class TaxCategoryService(AppDbContext db, DbContextGate gate) : ITaxCategoryService
{
    public Task<IReadOnlyList<TaxCategoryDto>> GetAllAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var counts = await db.Products.AsNoTracking().Where(p => p.TaxCategoryId != null)
                .GroupBy(p => p.TaxCategoryId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

            var list = await db.TaxCategories.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new TaxCategoryDto { Id = x.Id, Name = x.Name, Rate = x.Rate, IsActive = x.IsActive })
                .ToListAsync(ct);

            foreach (var dto in list)
                dto.ProductCount = counts.GetValueOrDefault(dto.Id);

            return (IReadOnlyList<TaxCategoryDto>)list;
        }, ct);

    public Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
            (IReadOnlyList<LookupDto>)await db.TaxCategories.AsNoTracking()
                .Where(x => x.IsActive).OrderBy(x => x.Name)
                .Select(x => new LookupDto { Id = x.Id, Name = x.Name }).ToListAsync(ct),
        ct);

    public Task<Result<Guid>> CreateAsync(TaxCategoryDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var name = NormalizeName(dto.Name);
            if (string.IsNullOrWhiteSpace(name))
                return Result<Guid>.Failure("Tax category name is required.", "VALIDATION");

            var existing = await db.TaxCategories.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower(), ct);

            if (existing is not null)
            {
                if (!existing.IsDeleted)
                    return Result<Guid>.Failure($"A tax category named '{name}' already exists.", "DUPLICATE");

                existing.Name = name;
                existing.Rate = dto.Rate;
                existing.IsActive = dto.IsActive;
                existing.IsDeleted = false;
                await db.SaveChangesAsync(ct);
                return Result<Guid>.Success(existing.Id);
            }

            var entity = new TaxCategory { Name = name, Rate = dto.Rate, IsActive = dto.IsActive };
            db.TaxCategories.Add(entity);
            try
            {
                await db.SaveChangesAsync(ct);
                return Result<Guid>.Success(entity.Id);
            }
            catch (DbUpdateException)
            {
                return Result<Guid>.Failure($"A tax category named '{name}' already exists.", "DUPLICATE");
            }
        }, ct);

    public Task<Result> UpdateAsync(TaxCategoryDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var entity = await db.TaxCategories.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
            if (entity is null)
                return Result.Failure("Tax category not found.", "NOT_FOUND");

            var name = NormalizeName(dto.Name);
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure("Tax category name is required.", "VALIDATION");

            var duplicate = await db.TaxCategories.IgnoreQueryFilters()
                .AnyAsync(x => x.Id != dto.Id && x.Name.ToLower() == name.ToLower(), ct);
            if (duplicate)
                return Result.Failure($"A tax category named '{name}' already exists.", "DUPLICATE");

            entity.Name = name;
            entity.Rate = dto.Rate;
            entity.IsActive = dto.IsActive;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var entity = await db.TaxCategories.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
                return Result.Failure("Tax category not found.", "NOT_FOUND");

            db.TaxCategories.Remove(entity);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    private static string NormalizeName(string? value) => (value ?? string.Empty).Trim();
}
