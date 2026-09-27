using RVS.Domain.Branding;
using RVS.Domain.Entities;

namespace RVS.Domain.Validation;

/// <summary>
/// Validates a location's <see cref="LocationBrandingEmbedded"/> (<c>Spec A-16</c>, issue #470):
/// an optional absolute https logo URL, an optional <c>#RRGGBB</c> header colour, and an optional
/// <c>#RRGGBB</c> accent colour (darkened for customers when too light — <see cref="EffectiveAccent"/>).
/// </summary>
public static class LocationBrandingValidator
{
    /// <summary>Longest logo URL accepted.</summary>
    public const int MaxLogoUrlLength = 2048;

    /// <summary>
    /// The contrast an accent is darkened to reach against white: WCAG AA for body text. The
    /// accent is link text and outlined-button text on the white page, not only a button fill.
    /// </summary>
    public const double MinAccentContrast = 4.5;

    private const string White = "#FFFFFF";

    /// <summary>
    /// Validates <paramref name="branding"/> and returns the first problem found, or
    /// <see cref="ValidationResult.Success"/> when every rule passes.
    /// </summary>
    /// <param name="branding">The branding to validate. Must not be null.</param>
    public static ValidationResult Validate(LocationBrandingEmbedded branding)
    {
        ArgumentNullException.ThrowIfNull(branding);

        var logoResult = ValidateLogoUrl(branding.LogoUrl);
        if (!logoResult.IsValid)
        {
            return logoResult;
        }

        var headerResult = ValidateHeaderColor(branding.HeaderColor);
        return headerResult.IsValid ? ValidateAccentColor(branding.AccentColor) : headerResult;
    }

    /// <summary>
    /// Validates the optional logo URL: blank is allowed; otherwise an absolute https URL of at
    /// most <see cref="MaxLogoUrlLength"/> characters. Not http: the intake form is served over
    /// https, where a browser blocks an http image as mixed content, and the packet email would
    /// load it in the clear. Exposed for the manager settings form.
    /// </summary>
    /// <param name="logoUrl">The URL to check.</param>
    public static ValidationResult ValidateLogoUrl(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return ValidationResult.Success;
        }

        if (logoUrl.Length > MaxLogoUrlLength)
        {
            return ValidationResult.Failure($"Logo URL must not exceed {MaxLogoUrlLength} characters.");
        }

        return IsHttpsUrl(logoUrl)
            ? ValidationResult.Success
            : ValidationResult.Failure("Logo URL must be an absolute https:// address.");
    }

    /// <summary>
    /// Validates the optional header colour: blank is allowed; otherwise <c>#RRGGBB</c>.
    /// Exposed for the manager settings form.
    /// </summary>
    /// <param name="headerColor">The colour to check.</param>
    public static ValidationResult ValidateHeaderColor(string? headerColor)
    {
        if (string.IsNullOrWhiteSpace(headerColor))
        {
            return ValidationResult.Success;
        }

        return HeaderColor.Normalize(headerColor) is not null
            ? ValidationResult.Success
            : ValidationResult.Failure("Header colour must be a hex colour in the form #RRGGBB.");
    }

    /// <summary>
    /// Validates the optional accent colour: blank is allowed; otherwise <c>#RRGGBB</c>. Any
    /// colour is accepted — one too light to be link text on white is darkened for customers by
    /// <see cref="EffectiveAccent"/>, not rejected. Exposed for the manager settings form.
    /// </summary>
    /// <param name="accentColor">The colour to check.</param>
    public static ValidationResult ValidateAccentColor(string? accentColor)
    {
        if (string.IsNullOrWhiteSpace(accentColor))
        {
            return ValidationResult.Success;
        }

        return HeaderColor.Normalize(accentColor) is not null
            ? ValidationResult.Success
            : ValidationResult.Failure("Accent colour must be a hex colour in the form #RRGGBB.");
    }

    /// <summary>
    /// The accent customers actually see: <paramref name="accentColor"/> itself when it reaches
    /// <see cref="MinAccentContrast"/> against white, otherwise the lightest darker shade of it that
    /// does (<see cref="HeaderColor.DarkenToContrast"/>). The dealer's colour is stored as entered
    /// and darkened here, at render, so the rule can change without touching stored data.
    /// <c>null</c> when the colour is blank or not <c>#RRGGBB</c>.
    /// </summary>
    /// <param name="accentColor">The dealer's accent as stored.</param>
    public static string? EffectiveAccent(string? accentColor)
    {
        var hex = HeaderColor.Normalize(accentColor);
        return hex is null ? null : HeaderColor.DarkenToContrast(hex, White, MinAccentContrast);
    }

    /// <summary><c>true</c> when <paramref name="url"/> is an absolute https URL.</summary>
    /// <param name="url">The URL to check.</param>
    public static bool IsHttpsUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(uri.Host);
}
