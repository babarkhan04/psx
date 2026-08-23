using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class StoreService(AppDbContext db, DbContextGate gate, IStoreAccessService storeAccess) : IStoreService
{
    public Task<IReadOnlyList<StoreDto>> GetAllAsync(bool includeInactive = true, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetAllCoreAsync(includeInactive, ct), ct);

    private async Task<IReadOnlyList<StoreDto>> GetAllCoreAsync(bool includeInactive, CancellationToken ct)
    {
        var allowedStoreIds = await storeAccess.GetAllowedStoreIdsAsync(ct);
        if (allowedStoreIds.Count == 0)
        {
            return [];
        }

        var query = db.Stores.AsNoTracking()
            .Where(s => allowedStoreIds.Contains(s.Id));

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query.OrderBy(s => s.Code)
            .Select(s => Map(s))
            .ToListAsync(ct);
    }

    public Task<StoreDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            if (!await storeAccess.CanAccessStoreAsync(id, ct))
            {
                return null;
            }

            var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
            return store is null ? null : Map(store);
        }, ct);

    public Task<Result<Guid>> CreateAsync(StoreDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var code = dto.Code.Trim();
            if (await db.Stores.AnyAsync(s => s.Code == code, ct))
                return Result<Guid>.Failure($"A store with code '{code}' already exists.", "DUPLICATE_CODE");

            var store = new Store();
            Apply(store, dto);
            db.Stores.Add(store);
            await db.SaveChangesAsync(ct);
            return Result<Guid>.Success(store.Id);
        }, ct);

    public Task<Result> UpdateAsync(StoreDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == dto.Id, ct);
            if (store is null)
                return Result.Failure("Store not found.", "NOT_FOUND");

            var code = dto.Code.Trim();
            if (await db.Stores.AnyAsync(s => s.Code == code && s.Id != dto.Id, ct))
                return Result.Failure($"A store with code '{code}' already exists.", "DUPLICATE_CODE");

            Apply(store, dto);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (store is null)
                return Result.Failure("Store not found.", "NOT_FOUND");

            store.IsActive = isActive;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (store is null)
                return Result.Failure("Store not found.", "NOT_FOUND");

            if (await db.Registers.AnyAsync(r => r.StoreId == id, ct))
                return Result.Failure("Cannot delete a store that still has registers.", "HAS_DEPENDENTS");

            db.Stores.Remove(store);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    private static void Apply(Store store, StoreDto dto)
    {
        store.Code = dto.Code.Trim();
        store.Name = dto.Name.Trim();
        store.AddressLine1 = dto.AddressLine1?.Trim();
        store.AddressLine2 = dto.AddressLine2?.Trim();
        store.City = dto.City?.Trim();
        store.Region = dto.Region?.Trim();
        store.PostalCode = dto.PostalCode?.Trim();
        store.Country = dto.Country?.Trim();
        store.Phone = dto.Phone?.Trim();
        store.Email = dto.Email?.Trim();
        store.TimeZoneId = dto.TimeZoneId?.Trim();
        store.IsActive = dto.IsActive;
    }

    private static StoreDto Map(Store s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Name = s.Name,
        AddressLine1 = s.AddressLine1,
        AddressLine2 = s.AddressLine2,
        City = s.City,
        Region = s.Region,
        PostalCode = s.PostalCode,
        Country = s.Country,
        Phone = s.Phone,
        Email = s.Email,
        TimeZoneId = s.TimeZoneId,
        IsActive = s.IsActive
    };
}
