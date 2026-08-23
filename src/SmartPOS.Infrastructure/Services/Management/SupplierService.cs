using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class SupplierService(AppDbContext db, DbContextGate gate) : ISupplierService
{
    public Task<IReadOnlyList<SupplierListItemDto>> GetAllAsync(string? search = null, bool includeInactive = true, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var query = db.Suppliers.AsNoTracking();
            if (!includeInactive)
            {
                query = query.Where(s => s.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var like = $"%{search.Trim()}%";
                query = query.Where(s =>
                    EF.Functions.Like(s.Name, like) ||
                    (s.Phone != null && EF.Functions.Like(s.Phone, like)) ||
                    (s.City != null && EF.Functions.Like(s.City, like)));
            }

            return (IReadOnlyList<SupplierListItemDto>)await query
                .OrderBy(s => s.Name)
                .Select(s => new SupplierListItemDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Phone = s.Phone,
                    City = s.City,
                    TaxNumber = s.TaxNumber,
                    IsActive = s.IsActive,
                    PurchaseCount = s.Purchases.Count(p => p.Status == Domain.Enums.PurchaseStatus.Received)
                })
                .ToListAsync(ct);
        }, ct);

    public Task<SupplierDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return s is null ? null : new SupplierDto
            {
                Id = s.Id,
                Name = s.Name,
                Phone = s.Phone,
                Email = s.Email,
                AddressLine = s.AddressLine,
                City = s.City,
                TaxNumber = s.TaxNumber,
                Notes = s.Notes,
                IsActive = s.IsActive
            };
        }, ct);

    public Task<IReadOnlyList<SupplierLookupDto>> GetLookupAsync(string? term = null, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var query = db.Suppliers.AsNoTracking().Where(s => s.IsActive);
            if (!string.IsNullOrWhiteSpace(term))
            {
                var like = $"%{term.Trim()}%";
                query = query.Where(s => EF.Functions.Like(s.Name, like) || (s.Phone != null && EF.Functions.Like(s.Phone, like)));
            }

            return (IReadOnlyList<SupplierLookupDto>)await query
                .OrderBy(s => s.Name)
                .Take(50)
                .Select(s => new SupplierLookupDto { Id = s.Id, Name = s.Name, Phone = s.Phone })
                .ToListAsync(ct);
        }, ct);

    public Task<Result<Guid>> CreateAsync(SupplierDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Result<Guid>.Failure("Supplier name is required.", "VALIDATION");
            }

            var supplier = new Supplier
            {
                Name = dto.Name.Trim(),
                Phone = dto.Phone?.Trim(),
                Email = dto.Email?.Trim(),
                AddressLine = dto.AddressLine?.Trim(),
                City = dto.City?.Trim(),
                TaxNumber = dto.TaxNumber?.Trim(),
                Notes = dto.Notes?.Trim(),
                IsActive = dto.IsActive
            };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync(ct);
            return Result<Guid>.Success(supplier.Id);
        }, ct);

    public Task<Result> UpdateAsync(SupplierDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var supplier = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
            if (supplier is null)
            {
                return Result.Failure("Supplier not found.", "NOT_FOUND");
            }

            supplier.Name = dto.Name.Trim();
            supplier.Phone = dto.Phone?.Trim();
            supplier.Email = dto.Email?.Trim();
            supplier.AddressLine = dto.AddressLine?.Trim();
            supplier.City = dto.City?.Trim();
            supplier.TaxNumber = dto.TaxNumber?.Trim();
            supplier.Notes = dto.Notes?.Trim();
            supplier.IsActive = dto.IsActive;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var supplier = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (supplier is null)
            {
                return Result.Failure("Supplier not found.", "NOT_FOUND");
            }

            supplier.IsActive = isActive;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var supplier = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (supplier is null)
            {
                return Result.Failure("Supplier not found.", "NOT_FOUND");
            }

            if (await db.Purchases.AnyAsync(p => p.SupplierId == id, ct))
            {
                return Result.Failure("Cannot delete a supplier with purchase history.", "HAS_PURCHASES");
            }

            db.Suppliers.Remove(supplier);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
}
