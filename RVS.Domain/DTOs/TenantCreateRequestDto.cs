namespace RVS.Domain.DTOs;

/// <summary>
/// Platform-admin request to provision a tenant in one submission (Spec P-1, issue #563):
/// the <c>Tenant</c>, its <c>TenantConfig</c>, <c>Dealership</c>, first <c>Location</c> and slug
/// lookup, and the first Auth0 user. Re-submitting the same request is a safe retry (Spec P-6).
/// </summary>
public sealed record TenantCreateRequestDto
{
    /// <summary>Display name of the corporation; also the dealership name and the user's <c>orgName</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional tenant id. When omitted it is derived as <c>org_{snake_name}</c> from <see cref="Name"/>.</summary>
    public string? TenantId { get; init; }

    /// <summary>Where hand-sent invoices go. Optional.</summary>
    public string? BillingEmail { get; init; }

    /// <summary><c>mobile</c> or <c>location</c>.</summary>
    public string Plan { get; init; } = string.Empty;

    /// <summary>Free-text admin notes. Optional.</summary>
    public string? Notes { get; init; }

    /// <summary>Name of the first location.</summary>
    public string LocationName { get; init; } = string.Empty;

    /// <summary>Optional slug for the first location. When omitted it is generated from the dealership and location names.</summary>
    public string? LocationSlug { get; init; }

    /// <summary>Phone number for the first location. Optional.</summary>
    public string? LocationPhone { get; init; }

    /// <summary>First user's email; also the first location's packet recipient.</summary>
    public string OwnerEmail { get; init; } = string.Empty;

    /// <summary>First user's display name.</summary>
    public string OwnerDisplayName { get; init; } = string.Empty;

    /// <summary>First user's role. Location-scoped roles are scoped to the first location.</summary>
    public string OwnerRole { get; init; } = "dealer:owner";
}
