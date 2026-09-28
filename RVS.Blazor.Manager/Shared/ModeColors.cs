using RVS.Blazor.Manager.Services;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// Per-mode colours for the places that style themselves inline (issue #728). Every value is a
/// MudBlazor palette variable, so Light and Dark take whatever <c>ManagerTheme</c> audited rather
/// than a hex literal that can drift from it. High contrast is the exception the palette cannot
/// express on its own: text there is yellow, which is the high-contrast palette's primary.
/// </summary>
public static class ModeColors
{
    private const string Primary = "var(--mud-palette-primary)";
    private const string TextPrimary = "var(--mud-palette-text-primary)";

    /// <summary>Body and value text.</summary>
    public static string Text(ThemeMode mode) =>
        mode == ThemeMode.HighContrast ? Primary : TextPrimary;

    /// <summary>Labels, captions and chip text.</summary>
    public static string SecondaryText(ThemeMode mode) =>
        mode == ThemeMode.HighContrast ? Primary : "var(--mud-palette-text-secondary)";

    /// <summary>Card outlines and hairlines.</summary>
    public static string Line(ThemeMode mode) =>
        mode == ThemeMode.HighContrast ? Primary : "var(--mud-palette-lines-default)";

    /// <summary>
    /// Text-coloured actions — an icon button or a menu heading. Rust in light mode; in dark mode
    /// plain text, so the on-dark Rust stays reserved for the filled buttons.
    /// </summary>
    public static string Action(ThemeMode mode) =>
        mode == ThemeMode.Dark ? TextPrimary : Primary;
}
