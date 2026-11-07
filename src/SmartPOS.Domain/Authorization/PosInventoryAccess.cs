using System.Security.Claims;

namespace SmartPOS.Domain.Authorization;

public static class PosInventoryAccess
{
    public static bool CanAdjustStock(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true &&
        (user.IsInRole(DefaultRolePermissions.SuperAdmin) || user.IsInRole(DefaultRolePermissions.BusinessOwner)) &&
        user.HasClaim(Permissions.ClaimType, Permissions.Catalog.InventoryManage);
}
