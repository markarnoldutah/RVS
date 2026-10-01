namespace RVS.UI.Shared.Components;

/// <summary>The lockups from the RV Intake "Service Tag" logo kit that <c>BrandWordmark</c> can draw.</summary>
public enum BrandWordmarkVariant
{
    /// <summary>Tag glyph beside the two-tone "RV Intake" — the primary lockup for headers.</summary>
    Horizontal,

    /// <summary>Tag glyph above "RV Intake", for square spaces and splash panels.</summary>
    Stacked,

    /// <summary>The bare tag mark with no background, for narrow chrome.</summary>
    Glyph,

    /// <summary>"RV Intake" alone, with no glyph, for narrow spaces and inline use.</summary>
    Wordmark
}
