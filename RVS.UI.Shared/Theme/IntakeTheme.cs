using MudBlazor;

namespace RVS.UI.Shared.Theme;

/// <summary>
/// The Denim &amp; Rust theme for <c>RVS.Blazor.Intake</c> (Spec THEME-1).
/// <para>
/// Intake is anonymous and mobile-first, filled out once by a stressed customer standing next
/// to a broken RV — not a tool anyone lives in. Brand shows up more here, not less: full cream
/// ground, white cards, rounder corners, a notch more type. There is no dark-mode toggle; the
/// high-contrast mode below is an accessibility choice, not a second look.
/// </para>
/// </summary>
public static class IntakeTheme
{
    private static readonly PaletteLight BrandPalette = new()
    {
        Primary = RvsBrand.Accent,
        PrimaryContrastText = "#FFFFFF",
        Secondary = RvsBrand.Ink,
        SecondaryContrastText = RvsBrand.Paper,
        AppbarBackground = RvsBrand.Ink,
        AppbarText = RvsBrand.Paper,
        Background = RvsBrand.Paper,
        Surface = "#FFFFFF",
        TextPrimary = RvsBrand.TextOnPaper,
        TextSecondary = RvsBrand.TextSecondaryOnPaper,
        Success = RvsBrand.Success,
        SuccessContrastText = "#FFFFFF",
        Warning = RvsBrand.Warning,
        WarningContrastText = "#FFFFFF",
        Error = RvsBrand.Error,
        ErrorContrastText = "#FFFFFF",
        Info = RvsBrand.Info,
        InfoContrastText = "#FFFFFF"
    };

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = BrandPalette,

        // Intake exposes no dark-mode toggle. The dark palette is the light one restated
        // rather than left unset: if anything ever resolves it — an OS preference, a future
        // MudThemeProvider default — the customer still sees the brand, not MudBlazor's
        // stock dark grey clashing with a cream-and-rust form.
        PaletteDark = new PaletteDark
        {
            Primary = BrandPalette.Primary,
            PrimaryContrastText = BrandPalette.PrimaryContrastText,
            Secondary = BrandPalette.Secondary,
            SecondaryContrastText = BrandPalette.SecondaryContrastText,
            AppbarBackground = BrandPalette.AppbarBackground,
            AppbarText = BrandPalette.AppbarText,
            Background = BrandPalette.Background,
            Surface = BrandPalette.Surface,
            TextPrimary = BrandPalette.TextPrimary,
            TextSecondary = BrandPalette.TextSecondary,
            Success = BrandPalette.Success,
            SuccessContrastText = BrandPalette.SuccessContrastText,
            Warning = BrandPalette.Warning,
            WarningContrastText = BrandPalette.WarningContrastText,
            Error = BrandPalette.Error,
            ErrorContrastText = BrandPalette.ErrorContrastText,
            Info = BrandPalette.Info,
            InfoContrastText = BrandPalette.InfoContrastText
        },

        // One notch up from MudBlazor's default: this is filled out on a phone, standing up.
        Typography = RvsTypography.Build(
            baseFontSize: "1rem",
            buttonFontSize: "1rem",
            buttonFontWeight: "700"),

        LayoutProperties = new LayoutProperties
        {
            // Rounder than Manager — softer, less "console".
            DefaultBorderRadius = "14px"
        }
    };

    /// <summary>The accessibility theme. See <see cref="RvsHighContrastPalette"/> for why it is not brand-coloured.</summary>
    public static readonly MudTheme HighContrast = new()
    {
        PaletteLight = RvsHighContrastPalette.Create(withDrawer: false),
        Typography = RvsTypography.Build(baseFontSize: "1rem", buttonFontSize: "1rem", buttonFontWeight: "700"),
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px"
        }
    };
}
