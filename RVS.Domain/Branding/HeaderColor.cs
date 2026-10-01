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

    /// <summary>
    /// Returns <paramref name="color"/> unchanged when it already reaches <paramref name="minimum"/>
    /// contrast against <paramref name="background"/>; otherwise the lightest darker shade of it —
    /// same hue and saturation, lower lightness — that does. Used for a dealer accent too light to
    /// be link text on the white intake page (issue #470 follow-up): the customer sees the
    /// dealer's colour, darkened only as far as legibility needs.
    /// </summary>
    /// <param name="color">A <c>#RRGGBB</c> colour.</param>
    /// <param name="background">The <c>#RRGGBB</c> colour it sits on. Must be light enough that black clears <paramref name="minimum"/> on it.</param>
    /// <param name="minimum">The contrast floor, e.g. 4.5.</param>
    /// <exception cref="ArgumentException">Either colour is not <c>#RRGGBB</c>.</exception>
    public static string DarkenToContrast(string color, string background, double minimum)
    {
        var hex = Normalize(color)
            ?? throw new ArgumentException($"'{color}' is not a #RRGGBB colour.", nameof(color));

        if (ContrastRatio(hex, background) >= minimum)
        {
            return hex;
        }

        var (hue, saturation, lightness) = ToHsl(hex);

        // Contrast rises as lightness falls, so binary-search the lightest shade that passes.
        double low = 0, high = lightness;
        for (var i = 0; i < 24; i++)
        {
            var mid = (low + high) / 2;
            if (ContrastRatio(FromHsl(hue, saturation, mid), background) >= minimum)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        // Rounding to whole channels can land a hair under the floor; step down until it clears.
        var result = FromHsl(hue, saturation, low);
        while (ContrastRatio(result, background) < minimum && low > 0)
        {
            low = Math.Max(0, low - 0.002);
            result = FromHsl(hue, saturation, low);
        }

        return result;
    }

    /// <summary>The hue of a <c>#RRGGBB</c> colour, in degrees from 0 to 360.</summary>
    /// <param name="color">A <c>#RRGGBB</c> colour.</param>
    /// <exception cref="ArgumentException"><paramref name="color"/> is not <c>#RRGGBB</c>.</exception>
    public static double Hue(string color) =>
        ToHsl(Normalize(color) ?? throw new ArgumentException($"'{color}' is not a #RRGGBB colour.", nameof(color))).Hue;

    private static (double Hue, double Saturation, double Lightness) ToHsl(string hex)
    {
        var r = Byte(hex, 1) / 255.0;
        var g = Byte(hex, 3) / 255.0;
        var b = Byte(hex, 5) / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        var delta = max - min;

        if (delta == 0)
        {
            return (0, 0, lightness);
        }

        var saturation = delta / (1 - Math.Abs((2 * lightness) - 1));
        double hue;
        if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / delta) + 4);
        }

        return (hue < 0 ? hue + 360 : hue, saturation, lightness);
    }

    private static string FromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var x = chroma * (1 - Math.Abs(((hue / 60) % 2) - 1));
        var m = lightness - (chroma / 2);

        var (r, g, b) = hue switch
        {
            < 60 => (chroma, x, 0.0),
            < 120 => (x, chroma, 0.0),
            < 180 => (0.0, chroma, x),
            < 240 => (0.0, x, chroma),
            < 300 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x)
        };

        return string.Create(CultureInfo.InvariantCulture, $"#{ToByte(r + m):X2}{ToByte(g + m):X2}{ToByte(b + m):X2}");
    }

    private static int ToByte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 1) * 255);

    private static int Byte(string hex, int offset) =>
        int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

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
