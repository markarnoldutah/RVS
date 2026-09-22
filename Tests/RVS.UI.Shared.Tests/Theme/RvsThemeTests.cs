using FluentAssertions;
using FluentAssertions.Primitives;
using MudBlazor;
using MudBlazor.Utilities;
using RVS.UI.Shared.Theme;

namespace RVS.UI.Shared.Tests.Theme;

/// <summary>
/// Guards the Denim &amp; Rust brand palette (Spec THEME-1, issue #702).
/// The palette is the one place both Blazor apps agree on brand colour; if it
/// drifts, nothing else in the codebase notices.
/// </summary>
public class RvsThemeTests
{
    [Fact]
    public void Brand_Tokens_ShouldMatchTheSpecifiedHexValues()
    {
        RvsBrand.Ink.Should().Be("#2F4C6B");
        RvsBrand.Accent.Should().Be("#C1502E");
        RvsBrand.AccentOnDark.Should().Be("#E8956D");
        RvsBrand.Paper.Should().Be("#F6F1E7");
        RvsBrand.InkDarkSurface.Should().Be("#1B2A3C");
    }

    [Fact]
    public void Brand_FontFamily_ShouldLeadWithSpaceGroteskAndKeepFallbacks()
    {
        RvsBrand.FontFamily.Should().StartWith(["Space Grotesk"]);
        RvsBrand.FontFamily.Should().Contain("sans-serif");
    }

    [Fact]
    public void ManagerTheme_Light_ShouldUseRustActionOnInkChrome()
    {
        var palette = ManagerTheme.Theme.PaletteLight;

        palette.Primary.Should().BeColor(RvsBrand.Accent);
        palette.Secondary.Should().BeColor(RvsBrand.Ink);
        palette.AppbarBackground.Should().BeColor(RvsBrand.Ink);
        palette.AppbarText.Should().BeColor(RvsBrand.Paper);
        palette.DrawerBackground.Should().BeColor(RvsBrand.Ink);
    }

    [Fact]
    public void ManagerTheme_Light_BackgroundShouldBeBarelyTintedPaperNotFullCream()
    {
        // An 8-hour-shift console: cream everywhere reads as a landing page.
        var palette = ManagerTheme.Theme.PaletteLight;

        palette.Background.Should().BeColor("#FAF8F3");
        palette.Background.Should().NotBeColor(RvsBrand.Paper);
        palette.Surface.Should().BeColor("#FFFFFF");
    }

    [Fact]
    public void ManagerTheme_Dark_ShouldUseTheOnDarkAccentOverInkSurfaces()
    {
        var palette = ManagerTheme.Theme.PaletteDark;

        palette.Primary.Should().BeColor(RvsBrand.AccentOnDark);
        palette.Background.Should().BeColor(RvsBrand.InkDarkSurface);
        palette.Surface.Should().BeColor("#243B54");
        palette.AppbarBackground.Should().BeColor(RvsBrand.InkDarkSurface);
    }

    [Fact]
    public void ManagerTheme_Dark_PrimaryShouldNotBeFullSaturationRust()
    {
        // Full Rust loses contrast against a dark background at small sizes.
        ManagerTheme.Theme.PaletteDark.Primary.Should().NotBeColor(RvsBrand.Accent);
    }

    [Fact]
    public void IntakeTheme_ShouldUseFullCreamBackgroundWithWhiteCards()
    {
        var palette = IntakeTheme.Theme.PaletteLight;

        palette.Background.Should().BeColor(RvsBrand.Paper);
        palette.Surface.Should().BeColor("#FFFFFF");
        palette.Primary.Should().BeColor(RvsBrand.Accent);
        palette.AppbarBackground.Should().BeColor(RvsBrand.Ink);
    }

    [Fact]
    public void IntakeTheme_DarkPaletteShouldMirrorLight_SoNoStockDarkPaletteLeaksThrough()
    {
        // Intake exposes no dark-mode toggle; if MudBlazor ever resolves the dark
        // palette anyway, it must still be the brand, not MudBlazor's stock dark.
        var light = IntakeTheme.Theme.PaletteLight;
        var dark = IntakeTheme.Theme.PaletteDark;

        dark.Primary.Should().BeColor(light.Primary.Value);
        dark.Background.Should().BeColor(light.Background.Value);
        dark.Surface.Should().BeColor(light.Surface.Value);
        dark.AppbarBackground.Should().BeColor(light.AppbarBackground.Value);
    }

    [Fact]
    public void IntakeTheme_ShouldBeRounderAndLargerThanManager()
    {
        // Filled out once, on a phone, standing next to a broken RV.
        IntakeTheme.Theme.LayoutProperties.DefaultBorderRadius.Should().Be("14px");
        ManagerTheme.Theme.LayoutProperties.DefaultBorderRadius.Should().Be("10px");
        IntakeTheme.Theme.Typography.Default.FontSize.Should().Be("1rem");
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void EveryTheme_ShouldRenderInSpaceGrotesk(string name, MudTheme theme)
    {
        theme.Typography.Default.FontFamily.Should().NotBeNull(name);
        theme.Typography.Default.FontFamily![0].Should().Be("Space Grotesk", name);
        theme.Typography.Button.FontFamily![0].Should().Be("Space Grotesk", name);
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void EveryTheme_ShouldKeepErrorVisuallyDistinctFromTheRustPrimary(string name, MudTheme theme)
    {
        // Rust and true red must never be told apart by hue alone.
        theme.PaletteLight.Error.Should().NotBeColor(theme.PaletteLight.Primary.Value, name);
        theme.PaletteDark.Error.Should().NotBeColor(theme.PaletteDark.Primary.Value, name);
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void EveryTheme_ShouldLeaveTertiaryAtTheMudBlazorDefault(string name, MudTheme theme)
    {
        // A deliberately two-colour brand: no third accent was invented here.
        var stock = new PaletteLight();
        theme.PaletteLight.Tertiary.Should().BeColor(stock.Tertiary.Value, name);
    }

    [Fact]
    public void HighContrastThemes_ShouldStayMaximumContrast_NotBrandColoured()
    {
        // Accessibility outranks brand: high contrast keeps black/yellow/cyan.
        foreach (var (name, theme) in new[]
                 {
                     ("manager", ManagerTheme.HighContrast),
                     ("intake", IntakeTheme.HighContrast)
                 })
        {
            theme.PaletteLight.Background.Should().BeColor("#000000", name);
            theme.PaletteLight.TextPrimary.Should().BeColor("#FFFFFF", name);
            theme.PaletteLight.Primary.Should().BeColor("#FFFF00", name);
            theme.PaletteLight.Primary.Should().NotBeColor(RvsBrand.Accent, name);
        }
    }

    public static TheoryData<string, MudTheme> AllThemes() => new()
    {
        { "manager", ManagerTheme.Theme },
        { "intake", IntakeTheme.Theme }
    };
}

/// <summary>
/// MudBlazor normalises every <see cref="MudColor"/> to lowercase eight-digit hex
/// (<c>#2f4c6bff</c>), so a raw string comparison against a brand token always fails on case
/// and the alpha pair. Compare parsed colours instead.
/// </summary>
internal static class MudColorAssertions
{
    internal static void BeColor(this ObjectAssertions assertions, string expectedHex, string because = "", params object[] becauseArgs)
    {
        Normalize(assertions.Subject).Should().Be(new MudColor(expectedHex).Value, because, becauseArgs);
    }

    internal static void NotBeColor(this ObjectAssertions assertions, string expectedHex, string because = "", params object[] becauseArgs)
    {
        Normalize(assertions.Subject).Should().NotBe(new MudColor(expectedHex).Value, because, becauseArgs);
    }

    private static string Normalize(object? subject) => ((MudColor)subject!).Value;
}
