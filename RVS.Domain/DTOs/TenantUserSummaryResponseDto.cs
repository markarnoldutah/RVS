namespace RVS.Domain.DTOs;

/// <summary>
/// One Manager-app user of a tenant, as the platform-admin user list shows it (Spec P-9, issue #647).
/// </summary>
public sealed record TenantUserSummaryResponseDto
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }

    /// <summary>Every Auth0 role the user holds. Normally exactly one <c>dealer:*</c> role.</summary>
    public List<string> Roles { get; init; } = [];

    /// <summary><c>app_metadata.locationIds</c>; empty for tenant-wide roles.</summary>
    public List<string> LocationIds { get; init; } = [];

    /// <summary><c>false</c> when the user is blocked in Auth0.</summary>
    public bool LoginsEnabled { get; init; }

    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
}
