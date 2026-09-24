namespace RVS.UI.Shared.Theme;

/// <summary>
/// The "RV Intake" brand tokens — "Service Tag" mark, Denim &amp; Rust colourway (Spec THEME-1).
/// <para>
/// This is the one authoritative home for brand colour in the front end. Both Blazor apps
/// build their themes from here rather than declaring hex values of their own; the logo kit's
/// SVGs carry the same values, and <c>wwwroot/css/design-tokens.css</c> mirrors them for the
/// handful of components that style themselves with plain CSS.
/// </para>
/// <para>
/// There are two Rusts. <see cref="AccentLogo"/> is the logo colour and appears only in the
/// logo files: at 4.19:1 on cream it fails WCAG AA as text. <see cref="Accent"/> is the darker,
/// text-safe Rust every button, link and focus ring uses. Every UI pairing here was audited to
/// AA (≥ 4.5:1) in the revised THEME-1.
/// </para>
/// <para>
/// Semantic colours (<see cref="Success"/>, <see cref="Warning"/>, <see cref="Error"/>,
/// <see cref="Info"/>) are deliberately <em>not</em> derived from the brand pair.
/// <see cref="Error"/> is shifted toward crimson to sit apart from the Rust accent, but hue is
/// not a reliable signal on its own — every error state also carries an icon.
/// </para>
/// </summary>
public static class RvsBrand
{
    /// <summary>Denim — structure: app bar, drawer, nav, headings, body text.</summary>
    public const string Ink = "#2F4C6B";

    /// <summary>
    /// Text-safe Rust — action: primary buttons, links, focus states, active nav. 5.35:1 on
    /// cream, 6.02:1 under white button text.
    /// </summary>
    public const string Accent = "#A8431F";

    /// <summary>
    /// The logo's Rust — the "RV" in the wordmark. <b>Logo and marketing only</b>: it fails AA
    /// as text on cream, so no theme or component may use it.
    /// </summary>
    public const string AccentLogo = "#C1502E";

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

    /// <summary>Ink at secondary weight; 4.99:1 on cream, 5.30:1 on white.</summary>
    public const string TextSecondaryOnPaper = "rgba(32,52,74,0.72)";

    /// <summary>Cream at secondary weight on the dark surface; 5.67:1.</summary>
    public const string TextSecondaryOnInk = "rgba(240,236,225,0.70)";

    /// <summary>Drawer text and icons on Ink; 6.21:1.</summary>
    public const string DrawerTextOnInk = "rgba(246,241,231,0.85)";

    /// <summary>Denim lifted for dark mode, where full Ink would vanish into the surface.</summary>
    public const string InkOnDark = "#8FA9C2";

    // Semantic colours — independent of the brand pair on purpose. Light-mode fills take white
    // text; dark-mode fills are all light and take InkDarkSurface text.
    public const string Success = "#36704E";
    public const string Warning = "#92600F";
    public const string Error = "#A3123F";
    public const string Info = "#3B6E91";

    public const string SuccessDark = "#6DBA88";
    public const string WarningDark = "#E0A94E";
    public const string ErrorDark = "#F08A8A";
    public const string InfoDark = "#7FB2D3";

    /// <summary>
    /// Space Grotesk, self-hosted from <c>_content/RVS.UI.Shared/fonts/fonts.css</c> so the
    /// PWA still renders in brand on a bad connection in a service bay. The fallbacks matter:
    /// they are what shows during the <c>font-display: swap</c> window.
    /// </summary>
    public static readonly string[] FontFamily =
        ["Space Grotesk", "Roboto", "Helvetica", "Arial", "sans-serif"];
}
