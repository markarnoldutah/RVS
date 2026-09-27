namespace RVS.Domain.DTOs;

/// <summary>
/// A location's customer-facing branding (<c>Spec A-16</c>, issue #470): the dealer logo on the
/// intake form and the packet, and the intake form's header and accent colours. Each field is optional and
/// falls back to the RV Intake default on its own.
/// </summary>
public sealed record LocationBrandingDto
{
    /// <summary>Absolute https URL of the dealer's logo, or <c>null</c> for the RV Intake mark.</summary>
    public string? LogoUrl { get; init; }

    /// <summary>Intake header-bar colour as <c>#RRGGBB</c>, or <c>null</c> for the brand's Denim.</summary>
    public string? HeaderColor { get; init; }

    /// <summary>
    /// Intake accent — buttons, links, focus — as <c>#RRGGBB</c>, or <c>null</c> for Rust. Must
    /// clear 4.5:1 against white.
    /// </summary>
    public string? AccentColor { get; init; }
}
