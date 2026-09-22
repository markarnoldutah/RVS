namespace RVS.Domain.DTOs;

/// <summary>
/// Body of the 403 that <c>TenantAccessGateMiddleware</c> returns when a tenant's logins are
/// disabled. The manager app keys on <see cref="Code"/> to show one "access restricted" screen
/// instead of a raw 403 on every page (issue #625).
/// </summary>
public sealed record TenantAccessDeniedResponseDto
{
    /// <summary>The <see cref="Code"/> value for a disabled tenant.</summary>
    public const string TenantDisabledCode = "tenant_disabled";

    public string Message { get; init; } = default!;
    public string ErrorId { get; init; } = default!;
    public string Code { get; init; } = default!;

    /// <summary>The tenant's own message for its users, if the platform admin set one.</summary>
    public string? DisabledMessage { get; init; }

    /// <summary>Who the user should contact, if the platform admin set one.</summary>
    public string? SupportContactEmail { get; init; }
}
