using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RVS.Domain.Validation;

/// <summary>
/// Generates URL-safe slugs that conform to <see cref="SlugValidator"/>'s contract
/// (<c>/^[a-z0-9-]+$/</c>, max 64 chars).
///
/// Strategy:
/// <list type="bullet">
///   <item>Strip diacritics (e.g., <c>café</c> → <c>cafe</c>).</item>
///   <item>Lowercase and replace any run of non-alphanumeric characters with a single hyphen.</item>
///   <item>Trim leading/trailing hyphens and cap to <see cref="MaxSlugLength"/>.</item>
/// </list>
/// </summary>
public static partial class SlugGenerator
{
    /// <summary>Hard maximum slug length (matches <see cref="SlugValidator"/>'s default).</summary>
    public const int MaxSlugLength = 64;

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRun();

    /// <summary>
    /// Slugifies a single string (e.g., a location name) into a URL-safe token.
    /// Returns an empty string when the input contains no alphanumeric characters.
    /// </summary>
    public static string Slugify(string? value, int maxLength = MaxSlugLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var folded = RemoveDiacritics(value).ToLowerInvariant();
        var collapsed = NonAlphanumericRun().Replace(folded, "-").Trim('-');

        if (collapsed.Length > maxLength)
        {
            collapsed = collapsed[..maxLength].TrimEnd('-');
        }

        return collapsed;
    }

    /// <summary>
    /// Combines an organization (dealership) slug with a location-name slug to produce a
    /// human-readable, hierarchical slug such as <c>camping-world-salt-lake</c>.
    /// Either part may be empty; if both produce content they are joined with a hyphen.
    /// If the org prefix is already a prefix of the location slug it is not duplicated.
    /// </summary>
    /// <param name="orgSlug">The dealership/org slug (already URL-safe). May be null/empty.</param>
    /// <param name="locationName">The human-readable location name. May be null/empty.</param>
    /// <param name="maxLength">Maximum slug length. Defaults to <see cref="MaxSlugLength"/>.</param>
    public static string ForLocation(string? orgSlug, string? locationName, int maxLength = MaxSlugLength)
    {
        var orgPart = Slugify(orgSlug, maxLength);
        var locPart = Slugify(locationName, maxLength);

        if (orgPart.Length == 0)
        {
            return locPart;
        }

        if (locPart.Length == 0)
        {
            return orgPart;
        }

        // Avoid "camping-world-camping-world-..." when the location name already starts with the org name.
        var combined = locPart.StartsWith(orgPart + "-", StringComparison.Ordinal)
            ? locPart
            : $"{orgPart}-{locPart}";

        if (combined.Length > maxLength)
        {
            combined = combined[..maxLength].TrimEnd('-');
        }

        return combined;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
