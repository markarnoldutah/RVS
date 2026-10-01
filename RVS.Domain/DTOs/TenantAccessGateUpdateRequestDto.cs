namespace RVS.Domain.DTOs;

/// <summary>
/// Platform-admin toggle of a tenant's access gate (Spec P-4, issue #563). Controls access only;
/// the commercial <c>Tenant.Status</c> is edited separately.
/// </summary>
public sealed record TenantAccessGateUpdateRequestDto
{
    public bool LoginsEnabled { get; init; }

    /// <summary>Why logins are disabled (e.g. <c>PastDue</c>). Required when <see cref="LoginsEnabled"/> is false.</summary>
    public string? Reason { get; init; }
}
