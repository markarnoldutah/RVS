using RVS.Blazor.Manager.Services;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// Theme-dependent styling shared by the right-hand detail flyouts — the service request
/// flyout and the location flyout (issue #724) — so the two cannot drift apart.
/// </summary>
public static class FlyoutStyles
{
    /// <summary>Background of the flyout column, including its sticky header and footer.</summary>
    public static string ColumnBackground(ThemeMode mode) => mode == ThemeMode.Dark
        ? "#181818"
        : "var(--mud-palette-background)";

    /// <summary>Hairline on the flyout's leading edge.</summary>
    public static string BorderColor(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark         => "rgba(255,255,255,0.12)",
        ThemeMode.HighContrast => "#FFFF00",
        _                      => "rgba(0,0,0,0.12)"
    };

    /// <summary><c>Style</c> for the <c>MudDrawer</c> itself.</summary>
    public static string DrawerStyle(ThemeMode mode) => $"border-left: 1px solid {BorderColor(mode)};";

    /// <summary><c>style</c> for the full-height column inside the drawer.</summary>
    public static string ColumnStyle(ThemeMode mode) => $"height: 100vh; background: {ColumnBackground(mode)};";

    /// <summary>Sticky title header.</summary>
    public static string HeaderStyle(ThemeMode mode) =>
        $"position: sticky; top: 0; z-index: 2; border-bottom: 1px solid var(--mud-palette-divider); background: {ColumnBackground(mode)};";

    /// <summary>Sticky action footer.</summary>
    public static string FooterStyle(ThemeMode mode) =>
        $"position: sticky; bottom: 0; z-index: 2; border-top: 1px solid var(--mud-palette-divider); background: {ColumnBackground(mode)};";
}
