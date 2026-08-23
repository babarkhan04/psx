using System.ComponentModel.DataAnnotations;

namespace SmartPOS.Shared.Contracts;

public sealed class CompanyDto
{
    public Guid Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? LegalName { get; set; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? TaxNumber { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class StoreDto
{
    public Guid Id { get; set; }

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? Region { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? TimeZoneId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class RegisterDto
{
    public Guid Id { get; set; }

    [Required]
    public Guid StoreId { get; set; }

    public string? StoreName { get; set; }

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Location { get; set; }

    [StringLength(100)]
    public string? PrinterName { get; set; }

    public bool HasCashDrawer { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public sealed class AppUserDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginOn { get; set; }
    public IList<string> Roles { get; set; } = [];
    public IList<Guid> StoreIds { get; set; } = [];
}

public sealed class UserEditModel
{
    public string? Id { get; set; }

    [Required, StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;
    public IList<string> Roles { get; set; } = [];
    public IList<Guid> StoreIds { get; set; } = [];
    public Guid? DefaultStoreId { get; set; }

    public bool IsNew => string.IsNullOrEmpty(Id);
}

public sealed class RoleDto
{
    public string Id { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string Name { get; set; } = string.Empty;

    public bool IsBuiltIn { get; set; }
    public int UserCount { get; set; }
    public IList<string> Permissions { get; set; } = [];
}

public sealed class PermissionGroupDto
{
    public string Group { get; set; } = string.Empty;
    public IList<PermissionOptionDto> Items { get; set; } = [];
}

public sealed class PermissionOptionDto
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Granted { get; set; }
}
