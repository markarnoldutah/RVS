namespace RVS.UI.Shared.Components;

/// <summary>
/// Maps a <see cref="BrandWordmarkVariant"/> to the logo kit's SVG in <c>wwwroot/brand/</c>
/// (Spec THEME-1 §4).
/// <para>
/// The kit's lockups have their text converted to outlines, so they render identically as a
/// plain <c>&lt;img&gt;</c> whether or not Space Grotesk has loaded — the files themselves are
/// what the apps show, and there is no second copy of the geometry here to drift. The colours
/// are baked into each file: the light colourway uses the logo Rust <c>#C1502E</c>
/// (<see cref="Theme.RvsBrand.AccentLogo"/>), the reversed one Light Rust and Cream.
/// </para>
/// </summary>
public static class BrandMarkAssets
{
    private const string BrandFolder = "_content/RVS.UI.Shared/brand/";

    /// <summary>The app-relative path of the lockup's SVG.</summary>
    /// <param name="variant">Which lockup to draw.</param>
    /// <param name="reversed">True when the mark sits on a dark or Ink surface, e.g. an app bar.</param>
    public static string PathFor(BrandWordmarkVariant variant, bool reversed)
    {
        var name = variant switch
        {
            BrandWordmarkVariant.Stacked => "logo-stacked",
            BrandWordmarkVariant.Glyph => "glyph",
            BrandWordmarkVariant.Wordmark => "wordmark",
            _ => "logo-horizontal"
        };

        return $"{BrandFolder}{name}{(reversed ? "-reversed" : string.Empty)}.svg";
    }
}
