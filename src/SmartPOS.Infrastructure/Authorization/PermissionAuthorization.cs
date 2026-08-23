using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SmartPOS.Domain.Authorization;

namespace SmartPOS.Infrastructure.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var hasPermission = context.User.HasClaim(Permissions.ClaimType, requirement.Permission);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Treats any policy name that matches a known permission key as a permission
/// policy, so screens can simply declare <c>[Authorize(Policy = Permissions...)]</c>
/// without each policy being registered by hand.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("setup.", StringComparison.Ordinal)
            || policyName.StartsWith("sales.", StringComparison.Ordinal)
            || policyName.StartsWith("catalog.", StringComparison.Ordinal)
            || policyName.StartsWith("relationships.", StringComparison.Ordinal)
            || policyName.StartsWith("operations.", StringComparison.Ordinal)
            || policyName.StartsWith("insights.", StringComparison.Ordinal))
        {
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
