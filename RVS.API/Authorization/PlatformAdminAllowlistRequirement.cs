using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using RVS.API.Options;

namespace RVS.API.Authorization;

/// <summary>
/// The second check on the <c>PlatformAdmin</c> policy (Spec P-7, issue #563): the caller's
/// <c>sub</c> must be on <see cref="AdminOptions.AllowedUserIds"/>. The permission alone is not
/// enough — the Auth0 tenant is shared across environments and products, so a stray role
/// assignment must not open the provisioning tool.
/// </summary>
public sealed class PlatformAdminAllowlistRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Succeeds <see cref="PlatformAdminAllowlistRequirement"/> when the caller's user id is on the
/// allowlist. Reads the options at request time, so a Key Vault change applies on the next
/// configuration reload without a redeploy of code.
/// </summary>
public sealed class PlatformAdminAllowlistHandler : AuthorizationHandler<PlatformAdminAllowlistRequirement>
{
    private readonly IOptionsMonitor<AdminOptions> _options;

    public PlatformAdminAllowlistHandler(IOptionsMonitor<AdminOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminAllowlistRequirement requirement)
    {
        // JwtBearer maps "sub" to NameIdentifier; fall back to the raw claim if mapping is off.
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (!string.IsNullOrWhiteSpace(userId)
            && _options.CurrentValue.AllowedUserIds.Any(allowed =>
                !string.IsNullOrWhiteSpace(allowed)
                && string.Equals(allowed.Trim(), userId, StringComparison.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
