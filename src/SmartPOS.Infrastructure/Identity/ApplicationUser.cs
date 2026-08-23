using Microsoft.AspNetCore.Identity;

namespace SmartPOS.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PinHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public DateTime? LastLoginOn { get; set; }
}

public static class AppRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string BusinessOwner = "Business Owner";
    public const string StoreManager = "Store Manager";
    public const string Cashier = "Cashier";
}
