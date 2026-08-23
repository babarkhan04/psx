using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Authorization;
using SmartPOS.Infrastructure.Persistence;

namespace SmartPOS.Infrastructure.Services;

/// <summary>
/// Central store-access guard for multi-store security.
/// UI store dropdowns are not enough; services must also verify that the
/// current user is allowed to access the requested store.
/// </summary>
public sealed class StoreAccessService(
    AppDbContext db,
    ICurrentUserService currentUser,
    ITenantContext tenantContext) : IStoreAccessService
{
    private static readonly string[] AllStoreRoles =
    [
        DefaultRolePermissions.SuperAdmin,
        DefaultRolePermissions.BusinessOwner
    ];

    public async Task<IReadOnlyList<Guid>> GetAllowedStoreIdsAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || tenantContext.TenantId is not { } tenantId)
        {
            return [];
        }

        if (HasAllStoreAccess())
        {
            return await db.Stores.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId && s.IsActive)
                .OrderBy(s => s.Code)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return [];
        }

        return await db.UserStores.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(us => us.TenantId == tenantId && us.UserId == currentUser.UserId && us.Store != null && us.Store.IsActive)
            .OrderByDescending(us => us.IsDefault)
            .ThenBy(us => us.Store!.Code)
            .Select(us => us.StoreId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<Guid?> GetDefaultStoreIdAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || tenantContext.TenantId is not { } tenantId)
        {
            return null;
        }

        if (HasAllStoreAccess())
        {
            return await db.Stores.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId && s.IsActive)
                .OrderBy(s => s.Code)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return null;
        }

        return await db.UserStores.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(us => us.TenantId == tenantId && us.UserId == currentUser.UserId && us.Store != null && us.Store.IsActive)
            .OrderByDescending(us => us.IsDefault)
            .ThenBy(us => us.Store!.Code)
            .Select(us => (Guid?)us.StoreId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> CanAccessStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        if (storeId == Guid.Empty)
        {
            return false;
        }

        var allowed = await GetAllowedStoreIdsAsync(ct);
        return allowed.Contains(storeId);
    }

    public async Task EnsureCanAccessStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        if (!await CanAccessStoreAsync(storeId, ct))
        {
            throw new UnauthorizedAccessException("You are not allowed to access the selected store.");
        }
    }

    private bool HasAllStoreAccess() =>
        currentUser.Roles.Any(role => AllStoreRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
}
