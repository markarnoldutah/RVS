using MudBlazor;

namespace RVS.UI.Shared.Theme;

/// <summary>
/// The shared high-contrast palette both apps offer as an accessibility mode.
/// <para>
/// This one is not brand-coloured, and that is the point: Denim and Rust cannot reach the
/// contrast a black ground with yellow and cyan does, and someone who turns high contrast on
/// has asked for legibility ahead of identity. Only the typeface follows the brand.
/// </para>
/// </summary>
internal static class RvsHighContrastPalette
{
    internal static PaletteLight Create(bool withDrawer)
    {
        var palette = new PaletteLight
        {
            Background = "#000000",
            BackgroundGray = "#1A1A1A",
            Surface = "#000000",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#FFFFFF",
            Primary = "#FFFF00",
            PrimaryContrastText = "#000000",
            Secondary = "#00FFFF",
            SecondaryContrastText = "#000000",
            Error = "#FF4D4D",
            ErrorContrastText = "#000000",
            Success = "#00FF00",
            SuccessContrastText = "#000000",
            Divider = "#FFFFFF",
            ActionDefault = "#FFFFFF",
            ActionDisabled = "#666666",
            AppbarBackground = "#000000",
            AppbarText = "#FFFFFF"
        };

        if (withDrawer)
        {
            palette.DrawerBackground = "#000000";
            palette.DrawerText = "#FFFFFF";
            palette.DrawerIcon = "#FFFFFF";
        }

        return palette;
    }
}
