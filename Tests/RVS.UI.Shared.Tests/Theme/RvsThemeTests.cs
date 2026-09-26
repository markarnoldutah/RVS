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
        RvsBrand.Accent.Should().Be("#A8431F");
        RvsBrand.AccentLogo.Should().Be("#C1502E");
        RvsBrand.AccentOnDark.Should().Be("#E8956D");
        RvsBrand.Paper.Should().Be("#F6F1E7");
        RvsBrand.InkDarkSurface.Should().Be("#1B2A3C");
    }

    [Fact]
    public void Brand_SemanticTokens_ShouldMatchTheWcagAuditedValues()
    {
        // Revised THEME-1: every pairing checked against WCAG 2.1 AA on cream and white.
        RvsBrand.Success.Should().Be("#36704E");
        RvsBrand.Warning.Should().Be("#92600F");
        RvsBrand.Error.Should().Be("#A3123F");
        RvsBrand.Info.Should().Be("#3B6E91");
        RvsBrand.SuccessDark.Should().Be("#6DBA88");
        RvsBrand.WarningDark.Should().Be("#E0A94E");
        RvsBrand.ErrorDark.Should().Be("#F08A8A");
        RvsBrand.InfoDark.Should().Be("#7FB2D3");
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void EveryTheme_ShouldNeverUseTheLogoRustInTheUi(string name, MudTheme theme)
    {
        // #C1502E is 4.19:1 on cream — fine for a logo, a fail for button and link text.
        theme.PaletteLight.Primary.Should().NotBeColor(RvsBrand.AccentLogo, name);
        theme.PaletteDark.Primary.Should().NotBeColor(RvsBrand.AccentLogo, name);
    }

    [Fact]
    public void ManagerTheme_Dark_EveryFilledColourShouldCarryDarkText()
    {
        // Every dark-mode fill is light, so MudBlazor's default white contrast text would vanish.
        var palette = ManagerTheme.Theme.PaletteDark;

        foreach (var (role, contrast) in new[]
                 {
                     ("primary", palette.PrimaryContrastText),
                     ("secondary", palette.SecondaryContrastText),
                     ("success", palette.SuccessContrastText),
                     ("warning", palette.WarningContrastText),
                     ("error", palette.ErrorContrastText),
                     ("info", palette.InfoContrastText)
                 })
        {
            contrast.Should().BeColor(RvsBrand.InkDarkSurface, role);
        }
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void EveryTheme_Light_SemanticFillsShouldCarryWhiteText(string name, MudTheme theme)
    {
        var palette = theme.PaletteLight;

        palette.PrimaryContrastText.Should().BeColor("#FFFFFF", name);
        palette.SuccessContrastText.Should().BeColor("#FFFFFF", name);
        palette.WarningContrastText.Should().BeColor("#FFFFFF", name);
        palette.ErrorContrastText.Should().BeColor("#FFFFFF", name);
        palette.InfoContrastText.Should().BeColor("#FFFFFF", name);
        palette.TextSecondary.Should().BeColor("rgba(32,52,74,0.72)", name);
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
        // Error is crimson, Primary rust: close in hue, so they must at least never be the same colour.
        // Hue is not the only signal either — every error state also carries an icon (THEME-1 §1).
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

    [Theory]
    [MemberData(nameof(IntakeThemes))]
    public void IntakeTheme_EverySemanticFill_ShouldMeetAaAgainstItsContrastText(string name, MudTheme theme)
    {
        // Intake notifications are solid fills (#761): the text and icon sit on the semantic
        // colour itself, so each fill/contrast-text pair must clear WCAG AA on its own.
        var palette = theme.PaletteLight;

        foreach (var (role, fill, text) in new[]
                 {
                     ("success", palette.Success, palette.SuccessContrastText),
                     ("warning", palette.Warning, palette.WarningContrastText),
                     ("error", palette.Error, palette.ErrorContrastText),
                     ("info", palette.Info, palette.InfoContrastText)
                 })
        {
            ContrastRatio(fill, text).Should().BeGreaterThanOrEqualTo(4.5, $"{name} {role}");
        }
    }

    [Fact]
    public void HighContrastThemes_SemanticFillsShouldBeBrightWithBlackText()
    {
        // On a black ground a solid notification must be a bright fill carrying black text;
        // MudBlazor's stock warning/info fills take white text and fail AA there.
        foreach (var (name, theme) in new[]
                 {
                     ("manager", ManagerTheme.HighContrast),
                     ("intake", IntakeTheme.HighContrast)
                 })
        {
            var palette = theme.PaletteLight;
            var stock = new PaletteLight();

            palette.Warning.Should().NotBeColor(stock.Warning.Value, name);
            palette.Info.Should().NotBeColor(stock.Info.Value, name);
            palette.SuccessContrastText.Should().BeColor("#000000", name);
            palette.WarningContrastText.Should().BeColor("#000000", name);
            palette.ErrorContrastText.Should().BeColor("#000000", name);
            palette.InfoContrastText.Should().BeColor("#000000", name);
        }
    }

    [Fact]
    public void ManagerTheme_Dark_SecondaryShouldBeTheAuditedLightDenim()
    {
        ManagerTheme.Theme.PaletteDark.Secondary.Should().BeColor("#8FA9C2");
    }

    public static TheoryData<string, MudTheme> IntakeThemes() => new()
    {
        { "intake", IntakeTheme.Theme },
        { "intake-high-contrast", IntakeTheme.HighContrast }
    };

    /// <summary>WCAG 2.1 contrast ratio between two opaque colours.</summary>
    private static double ContrastRatio(MudColor a, MudColor b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double RelativeLuminance(MudColor c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
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
