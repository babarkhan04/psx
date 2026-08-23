using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>
/// Assigns an application user to a store they are allowed to operate in.
/// Enforces store-level isolation for non-administrative users.
/// </summary>
public class UserStore : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public Guid StoreId { get; set; }

    /// <summary>The store selected by default when the user signs in.</summary>
    public bool IsDefault { get; set; }

    public Store? Store { get; set; }
}
