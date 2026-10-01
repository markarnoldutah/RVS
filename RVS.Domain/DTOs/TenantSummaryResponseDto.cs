namespace RVS.Domain.DTOs;

/// <summary>
/// One row of the platform-admin tenant list (issue #563): commercial details, the access gate,
/// and the tenant's locations with their public intake URLs.
/// </summary>
public sealed record TenantSummaryResponseDto
{
    public string TenantId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? BillingEmail { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Plan { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public bool LoginsEnabled { get; init; }
    public string? DisabledReason { get; init; }
    public DateTimeOffset? DisabledAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public List<TenantLocationSummaryDto> Locations { get; init; } = [];

    /// <summary>The tenant's master list of service capabilities (issue #757), for rendering the
    /// per-location capability checkboxes in the admin tool.</summary>
    public List<TenantCapabilityDto> AvailableCapabilities { get; init; } = [];
}

/// <summary>A location as shown in the platform-admin tenant list.</summary>
public sealed record TenantLocationSummaryDto
{
    public string LocationId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;

    /// <summary>Public intake link to hand out, e.g. <c>https://go.rvintake.com/{slug}</c> — the
    /// channel-tagging redirect (<c>Spec A-13</c>), not the intake app directly.</summary>
    public string IntakeUrl { get; init; } = string.Empty;

    /// <summary>Capability codes enabled for this location (issue #757).</summary>
    public List<string> EnabledCapabilities { get; init; } = [];
}
