using FluentAssertions;
using RVS.Blazor.Manager.Services;
using RVS.Blazor.Manager.Shared;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// Manager's per-mode colour choices resolve to MudBlazor palette variables, never to hex
/// literals (issue #728). The palette already holds the right value for Light and Dark, so a
/// hard-coded override can only drift from it — the pre-rebrand GitHub greys (<c>#9198A1</c>,
/// <c>#010409</c>) did, and <c>#9198A1</c> failed AA as chip text on the light board.
/// High contrast keeps its yellow text, reached through the high-contrast palette's primary.
/// </summary>
public class ModeColorsTests
{
    private const string Primary = "var(--mud-palette-primary)";
    private const string TextPrimary = "var(--mud-palette-text-primary)";
    private const string TextSecondary = "var(--mud-palette-text-secondary)";
    private const string LinesDefault = "var(--mud-palette-lines-default)";

    [Theory]
    [InlineData(ThemeMode.Light, TextPrimary)]
    [InlineData(ThemeMode.Dark, TextPrimary)]
    [InlineData(ThemeMode.HighContrast, Primary)]
    public void Text_ShouldResolveToThePaletteForTheMode(ThemeMode mode, string expected)
    {
        ModeColors.Text(mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(ThemeMode.Light, TextSecondary)]
    [InlineData(ThemeMode.Dark, TextSecondary)]
    [InlineData(ThemeMode.HighContrast, Primary)]
    public void SecondaryText_ShouldResolveToThePaletteForTheMode(ThemeMode mode, string expected)
    {
        ModeColors.SecondaryText(mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(ThemeMode.Light, LinesDefault)]
    [InlineData(ThemeMode.Dark, LinesDefault)]
    [InlineData(ThemeMode.HighContrast, Primary)]
    public void Line_ShouldResolveToThePaletteForTheMode(ThemeMode mode, string expected)
    {
        ModeColors.Line(mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(ThemeMode.Light, Primary)]
    [InlineData(ThemeMode.Dark, TextPrimary)]
    [InlineData(ThemeMode.HighContrast, Primary)]
    public void Action_ShouldBeRustExceptInDarkModeWhereItIsText(ThemeMode mode, string expected)
    {
        ModeColors.Action(mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    [InlineData(ThemeMode.HighContrast)]
    public void EveryColour_ShouldBeAPaletteVariable(ThemeMode mode)
    {
        foreach (var colour in new[]
                 {
                     ModeColors.Text(mode), ModeColors.SecondaryText(mode),
                     ModeColors.Line(mode), ModeColors.Action(mode)
                 })
        {
            colour.Should().StartWith("var(--mud-palette-");
        }
    }
}
