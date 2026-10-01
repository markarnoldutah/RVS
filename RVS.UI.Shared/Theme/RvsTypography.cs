using MudBlazor;

namespace RVS.UI.Shared.Theme;

/// <summary>
/// Shared typography and layout scales built on <see cref="RvsBrand.FontFamily"/> (Spec THEME-1).
/// Each app starts from these and adjusts only where its audience differs.
/// </summary>
internal static class RvsTypography
{
    /// <summary>
    /// Bold headings, medium UI labels and buttons, regular body — the weight ladder the
    /// wordmark uses. <paramref name="baseFontSize"/> and <paramref name="buttonFontSize"/>
    /// let Intake step up a notch for phone-in-hand use.
    /// </summary>
    internal static Typography Build(
        string? baseFontSize = null,
        string? buttonFontSize = null,
        string buttonFontWeight = "500")
    {
        var typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = RvsBrand.FontFamily,
                FontWeight = "400"
            },
            H1 = new H1Typography { FontFamily = RvsBrand.FontFamily, FontWeight = "700" },
            H2 = new H2Typography { FontFamily = RvsBrand.FontFamily, FontWeight = "700" },
            H3 = new H3Typography { FontFamily = RvsBrand.FontFamily, FontWeight = "700" },
            H4 = new H4Typography { FontFamily = RvsBrand.FontFamily, FontWeight = "500" },
            H5 = new H5Typography { FontFamily = RvsBrand.FontFamily, FontWeight = "500" },
            H6 = new H6Typography { FontFamily = RvsBrand.FontFamily, FontWeight = "500" },
            Subtitle1 = new Subtitle1Typography { FontFamily = RvsBrand.FontFamily },
            Subtitle2 = new Subtitle2Typography { FontFamily = RvsBrand.FontFamily },
            Body1 = new Body1Typography { FontFamily = RvsBrand.FontFamily },
            Body2 = new Body2Typography { FontFamily = RvsBrand.FontFamily },
            Caption = new CaptionTypography { FontFamily = RvsBrand.FontFamily },
            Overline = new OverlineTypography { FontFamily = RvsBrand.FontFamily },
            Button = new ButtonTypography
            {
                FontFamily = RvsBrand.FontFamily,
                FontWeight = buttonFontWeight
            }
        };

        if (baseFontSize is not null)
        {
            typography.Default.FontSize = baseFontSize;
        }

        if (buttonFontSize is not null)
        {
            typography.Button.FontSize = buttonFontSize;
        }

        return typography;
    }
}
