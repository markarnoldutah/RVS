using System.Globalization;

namespace RVS.Domain.Branding;

/// <summary>
/// A dealer's intake header colour (<c>Spec A-16</c>, issue #470): parsing it, and choosing the
/// foreground that stays legible on it. The dealer picks any colour, so the foreground cannot be
/// fixed: it is whichever of white and the brand's dark ground contrasts more, by the WCAG
/// relative-luminance formula. On any colour that choice clears 3:1, the WCAG minimum for the
/// icons and graphics a header bar carries.
/// </summary>
public static class HeaderColor
{
    /// <summary>Foreground on a dark header colour.</summary>
    public const string LightForeground = "#FFFFFF";

    /// <summary>Foreground on a light header colour — the brand's dark ground, as used on dark-mode fills.</summary>
    public const string DarkForeground = "#1B2A3C";

    /// <summary>
    /// Returns <paramref name="value"/> as upper-case <c>#RRGGBB</c>, or <c>null</c> when it is
    /// blank or not a six-digit hex colour.
    /// </summary>
    /// <param name="value">The colour to parse.</param>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#' || !trimmed.AsSpan(1).ToString().All(char.IsAsciiHexDigit))
        {
            return null;
        }

        return trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// <c>true</c> when white contrasts more with <paramref name="background"/> than
    /// <see cref="DarkForeground"/> does.
    /// </summary>
    /// <param name="background">A <c>#RRGGBB</c> colour.</param>
    /// <exception cref="ArgumentException"><paramref name="background"/> is not <c>#RRGGBB</c>.</exception>
    public static bool PrefersLightForeground(string background) =>
        ContrastRatio(background, LightForeground) >= ContrastRatio(background, DarkForeground);

    /// <summary>The foreground to draw on <paramref name="background"/>.</summary>
    /// <param name="background">A <c>#RRGGBB</c> colour.</param>
    /// <exception cref="ArgumentException"><paramref name="background"/> is not <c>#RRGGBB</c>.</exception>
    public static string ForegroundFor(string background) =>
        PrefersLightForeground(background) ? LightForeground : DarkForeground;

    /// <summary>The WCAG 2 contrast ratio between two <c>#RRGGBB</c> colours, from 1 to 21.</summary>
    /// <param name="first">A <c>#RRGGBB</c> colour.</param>
    /// <param name="second">A <c>#RRGGBB</c> colour.</param>
    /// <exception cref="ArgumentException">Either colour is not <c>#RRGGBB</c>.</exception>
    public static double ContrastRatio(string first, string second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double RelativeLuminance(string color)
    {
        var hex = Normalize(color)
            ?? throw new ArgumentException($"'{color}' is not a #RRGGBB colour.", nameof(color));

        return (0.2126 * Channel(hex, 1)) + (0.7152 * Channel(hex, 3)) + (0.0722 * Channel(hex, 5));
    }

    private static double Channel(string hex, int offset)
    {
        var srgb = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return srgb <= 0.04045 ? srgb / 12.92 : Math.Pow((srgb + 0.055) / 1.055, 2.4);
    }
}
