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
    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    [InlineData(ThemeMode.HighContrast)]
    public void ColumnStyle_AnyMode_ResetsTextToPageTextColor(ThemeMode mode)
    {
        var style = FlyoutStyles.ColumnStyle(mode);

        style.Should().Contain("color: var(--mud-palette-text-primary);");
        style.Should().NotContain("drawer-text");
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    [InlineData(ThemeMode.HighContrast)]
    public void ColumnStyle_AnyMode_KeepsFullHeightAndBackground(ThemeMode mode)
    {
        var style = FlyoutStyles.ColumnStyle(mode);

        style.Should().Contain("height: 100vh;");
        style.Should().Contain($"background: {FlyoutStyles.ColumnBackground(mode)};");
    }
}
