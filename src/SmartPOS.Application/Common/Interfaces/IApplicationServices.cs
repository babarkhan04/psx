using System.Security.Claims;

namespace SmartPOS.Application.Common.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

/// <summary>
/// Provides the current <see cref="ClaimsPrincipal"/> in a way that works both
/// during HTTP requests (via HttpContext) and inside Blazor Server circuits
/// (where there is no active HttpContext) once the circuit has been primed.
/// </summary>
public interface IUserContextAccessor
{
    ClaimsPrincipal? Principal { get; }
    void SetPrincipal(ClaimsPrincipal principal);
}

public static class AppClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string DisplayName = "display_name";
}

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
}

public interface ITenantContext
{
    Guid? TenantId { get; }
    void SetTenant(Guid tenantId);
}

public interface IStoreContext
{
    Guid? StoreId { get; }
    IReadOnlyList<Guid> AllowedStoreIds { get; }
    void SetStore(Guid storeId);
    void ClearStore();
}


public interface IStoreAccessService
{
    Task<IReadOnlyList<Guid>> GetAllowedStoreIdsAsync(CancellationToken ct = default);
    Task<Guid?> GetDefaultStoreIdAsync(CancellationToken ct = default);
    Task<bool> CanAccessStoreAsync(Guid storeId, CancellationToken ct = default);
    Task EnsureCanAccessStoreAsync(Guid storeId, CancellationToken ct = default);
}

public interface ICorrelationContext
{
    string CorrelationId { get; }
}

public interface IErrorLogService
{
    Task LogAsync(
        Exception exception,
        string source,
        string? errorCode = null,
        int? statusCode = null,
        CancellationToken cancellationToken = default);
}
