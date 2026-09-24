using MudBlazor;

namespace RVS.UI.Shared.Theme;

/// <summary>
/// The Denim &amp; Rust theme for <c>RVS.Blazor.Manager</c> (Spec THEME-1).
/// <para>
/// Manager is an authenticated, dense, hours-at-a-time tool — advisors live in the SR queue
/// and Service Board all day. Brand shows up in the app bar, the accent colour and the
/// typography, not in large cream blocks: it should read as a well-made B2B ops console.
/// </para>
/// </summary>
public static class ManagerTheme
{
    /// <summary>The brand theme. <c>MudThemeProvider.IsDarkMode</c> selects between the two palettes.</summary>
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = RvsBrand.Accent,
            PrimaryContrastText = "#FFFFFF",
            Secondary = RvsBrand.Ink,
            SecondaryContrastText = RvsBrand.Paper,
            AppbarBackground = RvsBrand.Ink,
            AppbarText = RvsBrand.Paper,
            DrawerBackground = RvsBrand.Ink,
            DrawerText = RvsBrand.DrawerTextOnInk,
            DrawerIcon = RvsBrand.DrawerTextOnInk,
            Background = RvsBrand.PaperNeutral,
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
        },
        PaletteDark = new PaletteDark
        {
            // Every dark-mode fill is light, so every *ContrastText is the dark ground —
            // MudBlazor's default white would put white text on light fills.
            Primary = RvsBrand.AccentOnDark,
            PrimaryContrastText = RvsBrand.InkDarkSurface,
            Secondary = RvsBrand.InkOnDark,
            SecondaryContrastText = RvsBrand.InkDarkSurface,
            AppbarBackground = RvsBrand.InkDarkSurface,
            AppbarText = RvsBrand.Paper,
            DrawerBackground = RvsBrand.InkDarkSurface,
            DrawerText = "rgba(240,236,225,0.85)",
            DrawerIcon = "rgba(240,236,225,0.85)",
            Background = RvsBrand.InkDarkSurface,
            Surface = RvsBrand.InkDarkElevated,
            TextPrimary = RvsBrand.TextOnInk,
            TextSecondary = RvsBrand.TextSecondaryOnInk,
            Divider = "rgba(240,236,225,0.20)",
            ActionDefault = RvsBrand.TextOnInk,
            ActionDisabled = "rgba(240,236,225,0.35)",
            Success = RvsBrand.SuccessDark,
            SuccessContrastText = RvsBrand.InkDarkSurface,
            Warning = RvsBrand.WarningDark,
            WarningContrastText = RvsBrand.InkDarkSurface,
            Error = RvsBrand.ErrorDark,
            ErrorContrastText = RvsBrand.InkDarkSurface,
            Info = RvsBrand.InfoDark,
            InfoContrastText = RvsBrand.InkDarkSurface
        },
        Typography = RvsTypography.Build(),
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "260px"
        }
    };

    /// <summary>
    /// The accessibility theme. Deliberately <em>not</em> brand-coloured: black ground with
    /// yellow and cyan carries far more contrast than Denim and Rust can, and a user who has
    /// asked for high contrast has asked for legibility over identity.
    /// </summary>
    public static readonly MudTheme HighContrast = new()
    {
        PaletteLight = RvsHighContrastPalette.Create(withDrawer: true),
        Typography = RvsTypography.Build(),
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "260px"
        }
    };
}
