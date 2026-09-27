using RVS.Domain.Branding;
using RVS.Domain.Entities;

namespace RVS.Domain.Validation;

/// <summary>
/// Validates a location's <see cref="LocationBrandingEmbedded"/> (<c>Spec A-16</c>, issue #470):
/// an optional absolute https logo URL and an optional <c>#RRGGBB</c> header colour.
/// </summary>
public static class LocationBrandingValidator
{
    /// <summary>Longest logo URL accepted.</summary>
    public const int MaxLogoUrlLength = 2048;

    /// <summary>
    /// Validates <paramref name="branding"/> and returns the first problem found, or
    /// <see cref="ValidationResult.Success"/> when every rule passes.
    /// </summary>
    /// <param name="branding">The branding to validate. Must not be null.</param>
    public static ValidationResult Validate(LocationBrandingEmbedded branding)
    {
        ArgumentNullException.ThrowIfNull(branding);

        var logoResult = ValidateLogoUrl(branding.LogoUrl);
        return logoResult.IsValid ? ValidateHeaderColor(branding.HeaderColor) : logoResult;
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

    /// <summary><c>true</c> when <paramref name="url"/> is an absolute https URL.</summary>
    /// <param name="url">The URL to check.</param>
    public static bool IsHttpsUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(uri.Host);
}
