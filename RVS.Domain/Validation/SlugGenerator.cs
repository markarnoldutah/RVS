using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RVS.Domain.Validation;

/// <summary>
/// Generates URL-safe slugs from human-readable text.
/// Used to auto-assign a slug when creating a new location,
/// derived from the dealership (org) name and the location name
/// so the result is human-readable and uniform.
/// </summary>
/// <example>
/// <code>
/// var slug = SlugGenerator.Generate("Camping World", "Salt Lake City");
/// // slug == "camping-world-salt-lake-city"
/// </code>
/// </example>
public static partial class SlugGenerator
{
    [GeneratedRegex("[^a-z0-9-]")]
    private static partial Regex NonSlugChars();

    [GeneratedRegex("-{2,}")]
    private static partial Regex ConsecutiveHyphens();

    /// <summary>
    /// Generates a slug from one or more name segments (e.g., dealership name, location name).
    /// Strips diacritics, lowercases, replaces spaces/non-alphanumeric with hyphens,
    /// collapses consecutive hyphens, and trims leading/trailing hyphens.
    /// </summary>
    /// <param name="segments">Name segments to combine into a slug.</param>
    /// <returns>A URL-safe, lowercase, hyphen-separated slug.</returns>
    public static string Generate(params string?[] segments)
    {
        var combined = string.Join(" ", segments.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(combined))
        {
            return string.Empty;
        }

        var normalized = RemoveDiacritics(combined);
        var lowered = normalized.ToLowerInvariant();
        var replaced = lowered.Replace(' ', '-');
        var cleaned = NonSlugChars().Replace(replaced, "");
        var collapsed = ConsecutiveHyphens().Replace(cleaned, "-");

        return collapsed.Trim('-');
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
