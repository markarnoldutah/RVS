using RVS.Blazor.Manager.Services;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// Theme-dependent styling shared by the right-hand detail flyouts — the service request
/// flyout and the location flyout (issue #724) — so the two cannot drift apart.
/// </summary>
public static class FlyoutStyles
{
    /// <summary>Background of the flyout column, including its sticky header and footer.</summary>
    private const string ColumnBackground = "var(--mud-palette-background)";

    /// <summary><c>Style</c> for the <c>MudDrawer</c> itself: a hairline on its leading edge.</summary>
    public static string DrawerStyle(ThemeMode mode) => $"border-left: 1px solid {ModeColors.Line(mode)};";

    /// <summary>
    /// <c>style</c> for the full-height column inside the drawer. <c>MudDrawer</c> sets its
    /// text to <c>--mud-palette-drawer-text</c> — near-white, for the Ink nav drawer — but this
    /// column is page-coloured, so it resets the text to the page's, or every label without
    /// an explicit colour reads white on cream (issue #742).
    /// </summary>
    public const string ColumnStyle =
        $"height: 100vh; background: {ColumnBackground}; color: var(--mud-palette-text-primary);";

    /// <summary>Sticky title header.</summary>
    public const string HeaderStyle =
        $"position: sticky; top: 0; z-index: 2; border-bottom: 1px solid var(--mud-palette-divider); background: {ColumnBackground};";

    /// <summary>Sticky action footer.</summary>
    public const string FooterStyle =
        $"position: sticky; bottom: 0; z-index: 2; border-top: 1px solid var(--mud-palette-divider); background: {ColumnBackground};";
}
