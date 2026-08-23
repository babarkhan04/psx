using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Identity;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class UserAdminService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    ITenantContext tenantContext,
    IDateTimeProvider clock) : IUserAdminService
{
    public async Task<IReadOnlyList<AppUserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = tenantContext.TenantId;
        var users = await userManager.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        var assignments = await db.UserStores.AsNoTracking().ToListAsync(ct);
        var byUser = assignments.GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.StoreId).ToList());

        var result = new List<AppUserDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(new AppUserDto
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                IsActive = user.IsActive,
                LastLoginOn = user.LastLoginOn,
                Roles = (await userManager.GetRolesAsync(user)).ToList(),
                StoreIds = byUser.TryGetValue(user.Id, out var ids) ? ids : []
            });
        }

        return result;
    }

    public async Task<UserEditModel?> GetAsync(string id, CancellationToken ct = default)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(
            u => u.Id == id && u.TenantId == tenantContext.TenantId, ct);
        if (user is null)
        {
            return null;
        }

        var assignments = await db.UserStores.AsNoTracking()
            .Where(a => a.UserId == id).ToListAsync(ct);

        return new UserEditModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            IsActive = user.IsActive,
            Roles = (await userManager.GetRolesAsync(user)).ToList(),
            StoreIds = assignments.Select(a => a.StoreId).ToList(),
            DefaultStoreId = assignments.FirstOrDefault(a => a.IsDefault)?.StoreId
        };
    }

    public async Task<Result<string>> CreateAsync(UserEditModel model, CancellationToken ct = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result<string>.Failure("No active company context.", "NO_TENANT");
        }

        if (string.IsNullOrWhiteSpace(model.Password))
        {
            return Result<string>.Failure("A password is required for new users.", "PASSWORD_REQUIRED");
        }

        var email = model.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Result<string>.Failure($"A user with email '{email}' already exists.", "DUPLICATE_EMAIL");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = model.DisplayName.Trim(),
            TenantId = tenantContext.TenantId.Value,
            IsActive = model.IsActive,
            CreatedOn = clock.UtcNow
        };

        var created = await userManager.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            return Result<string>.Failure(Describe(created), "CREATE_FAILED");
        }

        if (model.Roles.Count > 0)
        {
            await userManager.AddToRolesAsync(user, model.Roles);
        }

        await SyncStoresAsync(user.Id, model, ct);
        return Result<string>.Success(user.Id);
    }

    public async Task<Result> UpdateAsync(UserEditModel model, CancellationToken ct = default)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(
            u => u.Id == model.Id && u.TenantId == tenantContext.TenantId, ct);
        if (user is null)
        {
            return Result.Failure("User not found.", "NOT_FOUND");
        }

        user.DisplayName = model.DisplayName.Trim();
        user.IsActive = model.IsActive;

        var email = model.Email.Trim();
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            if (await userManager.FindByEmailAsync(email) is { } existing && existing.Id != user.Id)
            {
                return Result.Failure($"A user with email '{email}' already exists.", "DUPLICATE_EMAIL");
            }

            await userManager.SetEmailAsync(user, email);
            await userManager.SetUserNameAsync(user, email);
        }

        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return Result.Failure(Describe(updated), "UPDATE_FAILED");
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var toAdd = model.Roles.Except(currentRoles).ToList();
        var toRemove = currentRoles.Except(model.Roles).ToList();
        if (toRemove.Count > 0)
        {
            await userManager.RemoveFromRolesAsync(user, toRemove);
        }

        if (toAdd.Count > 0)
        {
            await userManager.AddToRolesAsync(user, toAdd);
        }

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, model.Password);
            if (!reset.Succeeded)
            {
                return Result.Failure(Describe(reset), "PASSWORD_FAILED");
            }
        }

        await SyncStoresAsync(user.Id, model, ct);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(string id, bool isActive, CancellationToken ct = default)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(
            u => u.Id == id && u.TenantId == tenantContext.TenantId, ct);
        if (user is null)
        {
            return Result.Failure("User not found.", "NOT_FOUND");
        }

        user.IsActive = isActive;
        await userManager.UpdateAsync(user);
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(string id, string newPassword, CancellationToken ct = default)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(
            u => u.Id == id && u.TenantId == tenantContext.TenantId, ct);
        if (user is null)
        {
            return Result.Failure("User not found.", "NOT_FOUND");
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, newPassword);
        return reset.Succeeded
            ? Result.Success()
            : Result.Failure(Describe(reset), "PASSWORD_FAILED");
    }

    private async Task SyncStoresAsync(string userId, UserEditModel model, CancellationToken ct)
    {
        var existing = await db.UserStores.Where(a => a.UserId == userId).ToListAsync(ct);

        foreach (var assignment in existing.Where(a => !model.StoreIds.Contains(a.StoreId)))
        {
            db.UserStores.Remove(assignment);
        }

        foreach (var storeId in model.StoreIds)
        {
            var assignment = existing.FirstOrDefault(a => a.StoreId == storeId);
            if (assignment is null)
            {
                assignment = new UserStore { UserId = userId, StoreId = storeId };
                db.UserStores.Add(assignment);
            }

            assignment.IsDefault = model.DefaultStoreId == storeId;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
