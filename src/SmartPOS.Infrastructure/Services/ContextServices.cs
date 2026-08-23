using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartPOS.Application.Common.Interfaces;

namespace SmartPOS.Infrastructure.Services;

/// <summary>
/// Resolves the active principal from the HTTP request when available, falling
/// back to a principal explicitly set for the current Blazor circuit.
/// </summary>
public sealed class UserContextAccessor(IHttpContextAccessor httpContextAccessor) : IUserContextAccessor
{
    private ClaimsPrincipal? _circuitPrincipal;

    public ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User ?? _circuitPrincipal;

    public void SetPrincipal(ClaimsPrincipal principal) => _circuitPrincipal = principal;
}

public sealed class CurrentUserService(IUserContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.Principal;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => User?.Identity?.Name;
    public string? Email => User?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
}

public sealed class TenantContext(IUserContextAccessor accessor) : ITenantContext
{
    private Guid? _override;

    public Guid? TenantId
    {
        get
        {
            if (_override is not null)
            {
                return _override;
            }

            var claim = accessor.Principal?.FindFirstValue(AppClaimTypes.TenantId);
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public void SetTenant(Guid tenantId) => _override = tenantId;
}

public sealed class StoreContext : IStoreContext
{
    public const string SelectedStoreCookieName = "SmartPOS.SelectedStoreId";

    public StoreContext(IHttpContextAccessor httpContextAccessor)
    {
        var raw = httpContextAccessor.HttpContext?.Request.Cookies[SelectedStoreCookieName];
        if (Guid.TryParse(raw, out var storeId))
        {
            StoreId = storeId;
        }
    }

    public Guid? StoreId { get; private set; }
    public IReadOnlyList<Guid> AllowedStoreIds { get; private set; } = [];

    public void SetStore(Guid storeId) => StoreId = storeId;

    public void ClearStore() => StoreId = null;

    public void SetAllowedStores(IEnumerable<Guid> storeIds) =>
        AllowedStoreIds = storeIds.Distinct().ToList();
}

public sealed class CorrelationContext(IHttpContextAccessor httpContextAccessor) : ICorrelationContext
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public string CorrelationId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context?.Items[ItemKey] is string correlationId && !string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }

            return context?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        }
    }
}
