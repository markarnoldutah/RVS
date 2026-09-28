using FluentAssertions;
using RVS.Blazor.Manager.Services;
using RVS.Blazor.Manager.Shared;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// The detail flyouts are <c>MudDrawer</c>s, and a drawer sets its text colour to
/// <c>--mud-palette-drawer-text</c> — near-white, for the Ink nav drawer. The flyout column
/// paints its own page-coloured background, so it has to reset the text colour to match,
/// or every label without an explicit colour renders white on cream (issue #742).
/// </summary>
public class FlyoutStylesTests
{
    [Fact]
    public void ColumnStyle_ResetsTextToPageTextColor()
    {
        var style = FlyoutStyles.ColumnStyle;

        style.Should().Contain("color: var(--mud-palette-text-primary);");
        style.Should().NotContain("drawer-text");
    }

    [Fact]
    public void ColumnStyle_KeepsFullHeightAndThePageBackground()
    {
        var style = FlyoutStyles.ColumnStyle;

        style.Should().Contain("height: 100vh;");
        style.Should().Contain("background: var(--mud-palette-background);");
    }

    [Fact]
    public void HeaderAndFooter_ShareTheColumnBackground()
    {
        // Dark mode used to paint the column #181818 — a neutral grey against the Denim page.
        FlyoutStyles.HeaderStyle.Should().Contain("background: var(--mud-palette-background);");
        FlyoutStyles.FooterStyle.Should().Contain("background: var(--mud-palette-background);");
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    [InlineData(ThemeMode.HighContrast)]
    public void DrawerStyle_EdgeFollowsTheModesLineColour(ThemeMode mode)
    {
        FlyoutStyles.DrawerStyle(mode).Should().Be($"border-left: 1px solid {ModeColors.Line(mode)};");
    }
}
