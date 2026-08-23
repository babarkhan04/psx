using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Authorization;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class RoleAdminService(
    RoleManager<IdentityRole> roleManager,
    UserManager<Identity.ApplicationUser> userManager) : IRoleAdminService
{
    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var roles = await roleManager.Roles.OrderBy(r => r.Name).ToListAsync(ct);
        var result = new List<RoleDto>(roles.Count);

        foreach (var role in roles)
        {
            var claims = await roleManager.GetClaimsAsync(role);
            var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);

            result.Add(new RoleDto
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                IsBuiltIn = DefaultRolePermissions.AllRoles.Contains(role.Name),
                UserCount = usersInRole.Count,
                Permissions = claims.Where(c => c.Type == Permissions.ClaimType)
                    .Select(c => c.Value).ToList()
            });
        }

        return result;
    }

    public async Task<Result<string>> CreateAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<string>.Failure("Role name is required.", "INVALID_NAME");
        }

        if (await roleManager.RoleExistsAsync(name))
        {
            return Result<string>.Failure($"A role named '{name}' already exists.", "DUPLICATE_NAME");
        }

        var role = new IdentityRole(name);
        var created = await roleManager.CreateAsync(role);
        return created.Succeeded
            ? Result<string>.Success(role.Id)
            : Result<string>.Failure(string.Join(" ", created.Errors.Select(e => e.Description)), "CREATE_FAILED");
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken ct = default)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null)
        {
            return Result.Failure("Role not found.", "NOT_FOUND");
        }

        if (DefaultRolePermissions.AllRoles.Contains(role.Name))
        {
            return Result.Failure("Built-in roles cannot be deleted.", "BUILTIN");
        }

        var deleted = await roleManager.DeleteAsync(role);
        return deleted.Succeeded
            ? Result.Success()
            : Result.Failure(string.Join(" ", deleted.Errors.Select(e => e.Description)), "DELETE_FAILED");
    }

    public async Task<IReadOnlyList<PermissionGroupDto>> GetPermissionMatrixAsync(string roleId, CancellationToken ct = default)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        var granted = role is null
            ? []
            : (await roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

        return Permissions.All
            .GroupBy(p => p.Group)
            .Select(g => new PermissionGroupDto
            {
                Group = g.Key,
                Items = g.Select(p => new PermissionOptionDto
                {
                    Key = p.Key,
                    Description = p.Description,
                    Granted = granted.Contains(p.Key)
                }).ToList()
            })
            .ToList();
    }

    public async Task<Result> SetPermissionsAsync(string roleId, IEnumerable<string> permissionKeys, CancellationToken ct = default)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            return Result.Failure("Role not found.", "NOT_FOUND");
        }

        var desired = permissionKeys
            .Where(k => Permissions.All.Any(p => p.Key == k))
            .ToHashSet(StringComparer.Ordinal);

        var current = (await roleManager.GetClaimsAsync(role))
            .Where(c => c.Type == Permissions.ClaimType)
            .ToList();

        foreach (var claim in current.Where(c => !desired.Contains(c.Value)))
        {
            await roleManager.RemoveClaimAsync(role, claim);
        }

        var currentKeys = current.Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var key in desired.Where(k => !currentKeys.Contains(k)))
        {
            await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, key));
        }

        return Result.Success();
    }
}
