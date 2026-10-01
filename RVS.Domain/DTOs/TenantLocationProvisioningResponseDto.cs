namespace RVS.Domain.DTOs;

/// <summary>
/// A location added through the platform-admin tool (Spec P-5, issue #563).
/// </summary>
public sealed record TenantLocationProvisioningResponseDto
{
    public string TenantId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;

    /// <summary>Public intake link to hand out, e.g. <c>https://go.rvintake.com/{slug}</c> — the
    /// channel-tagging redirect (<c>Spec A-13</c>), not the intake app directly.</summary>
    public string IntakeUrl { get; init; } = string.Empty;
}
