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
            DrawerText = "rgba(246,241,231,0.85)",
            DrawerIcon = "rgba(246,241,231,0.85)",
            Background = RvsBrand.PaperNeutral,
            Surface = "#FFFFFF",
            TextPrimary = RvsBrand.TextOnPaper,
            TextSecondary = "rgba(32,52,74,0.68)",
            Success = RvsBrand.Success,
            Warning = RvsBrand.Warning,
            Error = RvsBrand.Error,
            Info = RvsBrand.Info
        },
        PaletteDark = new PaletteDark
        {
            // Full-saturation Rust loses contrast on a dark background at small sizes.
            Primary = RvsBrand.AccentOnDark,
            PrimaryContrastText = RvsBrand.InkDarkSurface,
            Secondary = "#8FA9C2",
            AppbarBackground = RvsBrand.InkDarkSurface,
            AppbarText = RvsBrand.Paper,
            DrawerBackground = RvsBrand.InkDarkSurface,
            DrawerText = "rgba(240,236,225,0.85)",
            DrawerIcon = "rgba(240,236,225,0.85)",
            Background = RvsBrand.InkDarkSurface,
            Surface = RvsBrand.InkDarkElevated,
            TextPrimary = RvsBrand.TextOnInk,
            TextSecondary = "rgba(240,236,225,0.70)",
            Divider = "rgba(240,236,225,0.20)",
            ActionDefault = RvsBrand.TextOnInk,
            ActionDisabled = "rgba(240,236,225,0.35)",
            Success = RvsBrand.SuccessDark,
            Warning = RvsBrand.WarningDark,
            Error = RvsBrand.ErrorDark,
            Info = RvsBrand.InfoDark
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
