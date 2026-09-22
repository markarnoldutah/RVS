namespace RVS.UI.Shared.Theme;

/// <summary>
/// The "RV Intake" brand tokens — Concept D wordmark, Denim &amp; Rust colourway (Spec THEME-1).
/// <para>
/// This is the one authoritative home for brand colour in the front end. Both Blazor apps
/// build their themes from here rather than declaring hex values of their own; the logo kit's
/// SVGs carry the same values, and <c>wwwroot/css/design-tokens.css</c> mirrors them for the
/// handful of components that style themselves with plain CSS.
/// </para>
/// <para>
/// Semantic colours (<see cref="Success"/>, <see cref="Warning"/>, <see cref="Error"/>,
/// <see cref="Info"/>) are deliberately <em>not</em> derived from the brand pair. In particular
/// <see cref="Error"/> stays a true red so it never has to be told apart from the Rust
/// <see cref="Accent"/> by hue alone.
/// </para>
/// </summary>
public static class RvsBrand
{
    /// <summary>Denim — structure: app bar, drawer, nav, headings, body text.</summary>
    public const string Ink = "#2F4C6B";

    /// <summary>Rust — action: primary buttons, links, focus states, active nav.</summary>
    public const string Accent = "#C1502E";

    /// <summary>Rust as it sits on a dark or Ink surface; full-saturation Rust loses contrast there.</summary>
    public const string AccentOnDark = "#E8956D";

    /// <summary>Cream — page and card background in light mode.</summary>
    public const string Paper = "#F6F1E7";

    /// <summary>A step deeper than <see cref="Ink"/> — the dark-mode page background.</summary>
    public const string InkDarkSurface = "#1B2A3C";

    /// <summary>One step lighter than <see cref="InkDarkSurface"/> — dark-mode cards and panels.</summary>
    public const string InkDarkElevated = "#243B54";

    /// <summary>Barely tinted paper — a console page background that is warm without reading as cream.</summary>
    public const string PaperNeutral = "#FAF8F3";

    /// <summary>Ink at reading weight, for body copy on light surfaces.</summary>
    public const string TextOnPaper = "#20344A";

    /// <summary>Cream at reading weight, for body copy on Ink surfaces.</summary>
    public const string TextOnInk = "#F0ECE1";

    // Semantic colours — independent of the brand pair on purpose.
    public const string Success = "#3F7D58";
    public const string Warning = "#C98A2C";
    public const string Error = "#B3261E";
    public const string Info = "#3B6E91";

    public const string SuccessDark = "#5FA97A";
    public const string WarningDark = "#E0A94E";
    public const string ErrorDark = "#E5766A";
    public const string InfoDark = "#6FA3C4";

    /// <summary>
    /// Space Grotesk, self-hosted from <c>_content/RVS.UI.Shared/fonts/fonts.css</c> so the
    /// PWA still renders in brand on a bad connection in a service bay. The fallbacks matter:
    /// they are what shows during the <c>font-display: swap</c> window.
    /// </summary>
    public static readonly string[] FontFamily =
        ["Space Grotesk", "Roboto", "Helvetica", "Arial", "sans-serif"];
}
