using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Authorization;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Identity;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class ManagerApprovalService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IStoreAccessService storeAccess,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDateTimeProvider clock) : IManagerApprovalService
{
    private static readonly string[] ManagerRoles =
    [
        DefaultRolePermissions.SuperAdmin,
        DefaultRolePermissions.BusinessOwner,
        DefaultRolePermissions.StoreManager
    ];

    public async Task<Result<ManagerApprovalResultDto>> ApproveAsync(ManagerApprovalRequestDto request, CancellationToken ct = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result<ManagerApprovalResultDto>.Failure("No active company context.", "NO_TENANT");
        }

        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId) ||
            !Enum.IsDefined(request.ApprovalType) || request.StoreId is null || request.Amount is null or < 0)
            return Result<ManagerApprovalResultDto>.Failure("A signed-in user, store, action, and valid amount are required.", "VALIDATION");
        await storeAccess.EnsureCanAccessStoreAsync(request.StoreId.Value, ct);
        var email = request.ManagerEmail?.Trim() ?? string.Empty;
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.ManagerPassword))
        {
            return Result<ManagerApprovalResultDto>.Failure("Manager email and password are required.", "VALIDATION");
        }

        if (reason.Length < 3)
        {
            return Result<ManagerApprovalResultDto>.Failure("Approval reason is required.", "VALIDATION");
        }

        var manager = await userManager.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == userManager.NormalizeEmail(email) && u.TenantId == tenantContext.TenantId.Value, ct);

        if (manager is null || !manager.IsActive)
        {
            return Result<ManagerApprovalResultDto>.Failure("Manager approval failed. Check manager account and password.", "APPROVAL_FAILED");
        }

        var passwordCheck = await signInManager.CheckPasswordSignInAsync(manager, request.ManagerPassword, lockoutOnFailure: true);
        if (!passwordCheck.Succeeded)
        {
            return Result<ManagerApprovalResultDto>.Failure("Manager approval failed. Check manager account and password.", "APPROVAL_FAILED");
        }

        var roles = await userManager.GetRolesAsync(manager);
        if (!roles.Any(role => ManagerRoles.Contains(role)))
        {
            return Result<ManagerApprovalResultDto>.Failure("This user is not allowed to approve manager actions.", "NOT_MANAGER");
        }

        if (request.StoreId is { } storeId && !roles.Contains(DefaultRolePermissions.SuperAdmin) && !roles.Contains(DefaultRolePermissions.BusinessOwner))
        {
            var assigned = await db.UserStores.AsNoTracking()
                .AnyAsync(x => x.UserId == manager.Id && x.StoreId == storeId, ct);
            if (!assigned)
            {
                return Result<ManagerApprovalResultDto>.Failure("This manager is not assigned to the selected store.", "STORE_NOT_ALLOWED");
            }
        }

        var now = clock.UtcNow;
        var log = new ManagerApprovalLog
        {
            TenantId = tenantContext.TenantId.Value,
            StoreId = request.StoreId,
            ApprovalType = request.ApprovalType,
            Status = ManagerApprovalStatus.Approved,
            RequestedByUserId = currentUser.UserId ?? string.Empty,
            RequestedByUserName = currentUser.UserName ?? currentUser.Email,
            ApprovedByUserId = manager.Id,
            ApprovedByUserName = manager.DisplayName ?? manager.Email ?? manager.UserName ?? "Manager",
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            Amount = request.Amount,
            Reason = reason,
            ApprovedOn = now,
            CreatedOn = now,
            CreatedBy = currentUser.UserId
        };

        db.Set<ManagerApprovalLog>().Add(log);
        await db.SaveChangesAsync(ct);

        return Result<ManagerApprovalResultDto>.Success(new ManagerApprovalResultDto
        {
            ApprovalId = log.Id,
            ApprovedByUserId = manager.Id,
            ApprovedByName = log.ApprovedByUserName,
            ApprovedOn = now
        });
    }

    public async Task<IReadOnlyList<ManagerApprovalLogDto>> GetRecentAsync(Guid? storeId = null, int take = 100, CancellationToken ct = default)
    {
        var allowed = await storeAccess.GetAllowedStoreIdsAsync(ct);
        if (storeId is { } requestedStore)
            await storeAccess.EnsureCanAccessStoreAsync(requestedStore, ct);
        var query = db.Set<ManagerApprovalLog>().AsNoTracking()
            .Where(log => log.StoreId != null && allowed.Contains(log.StoreId.Value));
        if (storeId is not null)
        {
            query = query.Where(x => x.StoreId == storeId);
        }

        return await query
            .OrderByDescending(x => x.ApprovedOn)
            .Take(take <= 0 ? 100 : take)
            .Select(x => new ManagerApprovalLogDto
            {
                Id = x.Id,
                ApprovedOn = x.ApprovedOn,
                ApprovalType = x.ApprovalType.ToString(),
                ApprovedByName = x.ApprovedByUserName,
                RequestedByUserName = x.RequestedByUserName,
                Reference = x.Reference,
                Amount = x.Amount,
                Reason = x.Reason
            })
            .ToListAsync(ct);
    }
}
