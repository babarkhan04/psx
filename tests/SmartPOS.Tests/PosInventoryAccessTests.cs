using System.Security.Claims;
using SmartPOS.Domain.Authorization;

namespace SmartPOS.Tests;

public sealed class PosInventoryAccessTests
{
    [Theory]
    [InlineData(DefaultRolePermissions.SuperAdmin, true, true)]
    [InlineData(DefaultRolePermissions.BusinessOwner, true, true)]
    [InlineData(DefaultRolePermissions.StoreManager, true, false)]
    [InlineData(DefaultRolePermissions.Cashier, true, false)]
    [InlineData("Administrator", true, false)]
    [InlineData("Custom role", true, false)]
    [InlineData(DefaultRolePermissions.SuperAdmin, false, false)]
    [InlineData(DefaultRolePermissions.BusinessOwner, false, false)]
    public void Pos_stock_shortcut_requires_an_administrator_role_and_inventory_permission(string role, bool canManage, bool expected)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (canManage) claims.Add(new Claim(Permissions.ClaimType, Permissions.Catalog.InventoryManage));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        Assert.Equal(expected, PosInventoryAccess.CanAdjustStock(user));
    }

    [Fact]
    public void Unauthenticated_claims_do_not_grant_the_stock_shortcut()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, DefaultRolePermissions.SuperAdmin),
            new Claim(Permissions.ClaimType, Permissions.Catalog.InventoryManage)
        }));
        Assert.False(PosInventoryAccess.CanAdjustStock(user));
    }

    [Fact]
    public void An_administrator_with_multiple_roles_can_use_the_shortcut()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, DefaultRolePermissions.Cashier),
            new Claim(ClaimTypes.Role, DefaultRolePermissions.BusinessOwner),
            new Claim(Permissions.ClaimType, Permissions.Catalog.InventoryManage)
        }, "test"));
        Assert.True(PosInventoryAccess.CanAdjustStock(user));
    }
}
