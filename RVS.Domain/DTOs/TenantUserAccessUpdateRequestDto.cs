namespace RVS.Domain.DTOs;

/// <summary>
/// Platform-admin request to disable or re-enable one user's logins (Spec P-11, issue #647).
/// The per-user counterpart of <see cref="TenantAccessGateUpdateRequestDto"/>.
/// </summary>
public sealed record TenantUserAccessUpdateRequestDto
{
    public bool LoginsEnabled { get; init; }
}
