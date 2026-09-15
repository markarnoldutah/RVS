namespace RVS.Domain.DTOs;

/// <summary>
/// Platform-admin edit of a tenant's commercial details (issue #563). Null fields are left
/// unchanged; a blank <see cref="BillingEmail"/> or <see cref="Notes"/> clears it.
/// <see cref="Status"/> is the commercial state and never touches the access gate (Spec P-4).
/// </summary>
public sealed record TenantUpdateRequestDto
{
    /// <summary><c>Pilot</c>, <c>Active</c> or <c>Churned</c>.</summary>
    public string? Status { get; init; }

    /// <summary><c>mobile</c> or <c>location</c>.</summary>
    public string? Plan { get; init; }

    public string? BillingEmail { get; init; }

    public string? Notes { get; init; }
}
