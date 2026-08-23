using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Authorization;

namespace SmartPOS.Infrastructure.Identity;

/// <summary>
/// Enriches the signed-in user's identity with tenant, display name, and the
/// effective permission claims derived from their assigned roles. Baking these
/// into the auth cookie keeps tenant isolation and authorization working inside
/// Blazor circuits, where there is no live HttpContext to consult.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(AppClaimTypes.TenantId, user.TenantId.ToString()));
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            identity.AddClaim(new Claim(AppClaimTypes.DisplayName, user.DisplayName));
        }

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleName in await UserManager.GetRolesAsync(user))
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            foreach (var claim in await roleManager.GetClaimsAsync(role))
            {
                if (claim.Type == Permissions.ClaimType)
                {
                    permissions.Add(claim.Value);
                }
            }
        }

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim(Permissions.ClaimType, permission));
        }

        return identity;
    }
}
