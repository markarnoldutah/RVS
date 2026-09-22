using System.Globalization;
using System.Net;
using System.Text;
using RVS.UI.Shared.Theme;

namespace RVS.UI.Shared.Components;

/// <summary>
/// Builds the inline "RV Intake" wordmark (Concept D, Denim &amp; Rust — Spec THEME-1).
/// <para>
/// The mark is inlined into the host document rather than referenced as
/// <c>&lt;img src="…/brand/wordmark-horizontal.svg"&gt;</c> on purpose: the logo kit's SVGs
/// carry live <c>&lt;text&gt;</c>, and browsers refuse to load an external <c>@@font-face</c>
/// into an SVG used as an image — the wordmark would silently fall back to a system font.
/// Inlined, the self-hosted Space Grotesk applies and it renders in brand.
/// </para>
/// <para>
/// The standalone files in <c>wwwroot/brand/</c> stay the editable sources and are what to
/// hand to anyone outside the app; the geometry here mirrors them.
/// </para>
/// </summary>
public static class BrandWordmarkSvg
{
    // Geometry lifted from the logo kit's wordmark-horizontal.svg.
    private const string BadgeMarkup =
        """<rect x="14" y="14" width="112" height="112" rx="25" fill="{0}"/>""" +
        """<text x="41.5" y="86.5" font-family="{2}" font-weight="700" font-size="46" fill="{1}">RV</text>""";

    private const string FontStack = "Space Grotesk, Roboto, Helvetica, Arial, sans-serif";

    /// <summary>Renders the wordmark as a standalone SVG element.</summary>
    /// <param name="variant">Which lockup to draw.</param>
    /// <param name="reversed">True when the mark sits on a dark or Ink surface, e.g. an app bar.</param>
    /// <param name="height">CSS height; width follows the aspect ratio.</param>
    /// <param name="title">Accessible name, also the SVG <c>&lt;title&gt;</c>.</param>
    public static string Build(BrandWordmarkVariant variant, bool reversed, string height, string title)
    {
        var badgeFill = reversed ? RvsBrand.Paper : RvsBrand.Ink;
        var badgeText = reversed ? RvsBrand.Ink : RvsBrand.Paper;
        var accentFill = reversed ? RvsBrand.AccentOnDark : RvsBrand.Accent;
        var wordFill = reversed ? RvsBrand.Paper : RvsBrand.Ink;

        var safeTitle = WebUtility.HtmlEncode(title);
        var safeHeight = WebUtility.HtmlEncode(height);

        var viewBox = variant switch
        {
            BrandWordmarkVariant.Icon => "0 0 140 140",
            BrandWordmarkVariant.TextOnly => "142 0 289 140",
            _ => "0 0 431.2 140"
        };

        var svg = new StringBuilder(512);
        svg.Append(CultureInfo.InvariantCulture,
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="{viewBox}" height="{safeHeight}" role="img" aria-label="{safeTitle}" focusable="false">""");
        svg.Append(CultureInfo.InvariantCulture, $"<title>{safeTitle}</title>");

        if (variant is BrandWordmarkVariant.Horizontal or BrandWordmarkVariant.Icon)
        {
            svg.AppendFormat(CultureInfo.InvariantCulture, BadgeMarkup, badgeFill, badgeText, FontStack);
        }

        if (variant is BrandWordmarkVariant.Horizontal or BrandWordmarkVariant.TextOnly)
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"""<text font-family="{FontStack}" font-weight="700" font-size="58">""");
            svg.Append(CultureInfo.InvariantCulture, $"""<tspan x="156" y="90" fill="{accentFill}">RV</tspan>""");
            svg.Append(CultureInfo.InvariantCulture, $"""<tspan x="227.8" y="90" fill="{wordFill}"> Intake</tspan>""");
            svg.Append("</text>");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }
}
