using FluentAssertions;
using RVS.UI.Shared.Components;
using RVS.UI.Shared.Theme;

namespace RVS.UI.Shared.Tests.Components;

/// <summary>
/// The wordmark is inlined rather than loaded as an image so the self-hosted Space Grotesk
/// applies to its live text (Spec THEME-1, issue #702). These tests hold that inline copy to
/// the logo kit's geometry and colourways — including the revised lockup, where the badge
/// already reads "RV" so the text beside it says only "Intake".
/// </summary>
public class BrandWordmarkSvgTests
{
    [Fact]
    public void Build_Horizontal_ShouldNotRepeatRvBesideTheBadge()
    {
        var svg = BrandWordmarkSvg.Build(BrandWordmarkVariant.Horizontal, reversed: false, height: "32px", title: "RV Intake");

        svg.Should().Contain(">Intake</tspan>");
        svg.Should().NotContain(">RV</tspan>");
    }

    [Fact]
    public void Build_HorizontalOnLightSurface_ShouldDrawAnInkBadgeAndInkWord()
    {
        var svg = BrandWordmarkSvg.Build(BrandWordmarkVariant.Horizontal, reversed: false, height: "32px", title: "RV Intake");

        svg.Should().Contain($"fill=\"{RvsBrand.Ink}\"");
        svg.Should().Contain($"fill=\"{RvsBrand.Paper}\"");
        svg.Should().NotContain(RvsBrand.Accent);
        svg.Should().NotContain(RvsBrand.AccentOnDark);
    }

    [Fact]
    public void Build_HorizontalReversed_ShouldSwapTheBadgeToCream()
    {
        var svg = BrandWordmarkSvg.Build(BrandWordmarkVariant.Horizontal, reversed: true, height: "32px", title: "RV Intake");

        svg.Should().Contain($"<rect x=\"14\" y=\"14\" width=\"112\" height=\"112\" rx=\"25\" fill=\"{RvsBrand.Paper}\"");
        svg.Should().Contain($"fill=\"{RvsBrand.Ink}\">RV</text>");
        svg.Should().Contain($"fill=\"{RvsBrand.Paper}\">Intake</tspan>");
    }

    [Fact]
    public void Build_TextOnly_ShouldKeepTheTwoToneRvIntake()
    {
        var svg = BrandWordmarkSvg.Build(BrandWordmarkVariant.TextOnly, reversed: false, height: "32px", title: "RV Intake");

        svg.Should().Contain($"fill=\"{RvsBrand.Accent}\">RV</tspan>");
        svg.Should().Contain($"fill=\"{RvsBrand.Ink}\"> Intake</tspan>");
        svg.Should().NotContain("<rect");
    }

    [Fact]
    public void Build_TextOnlyReversed_ShouldUseTheOnDarkAccent()
    {
        var svg = BrandWordmarkSvg.Build(BrandWordmarkVariant.TextOnly, reversed: true, height: "32px", title: "RV Intake");

        svg.Should().Contain($"fill=\"{RvsBrand.AccentOnDark}\">RV</tspan>");
        svg.Should().Contain($"fill=\"{RvsBrand.Paper}\"> Intake</tspan>");
        svg.Should().NotContain(RvsBrand.Accent);
    }

    [Theory]
    [InlineData(BrandWordmarkVariant.Horizontal, "0 0 340.7 140")]
    [InlineData(BrandWordmarkVariant.Icon, "0 0 140 140")]
    [InlineData(BrandWordmarkVariant.TextOnly, "0 0 289.2 98")]
    public void Build_ShouldUseTheKitsViewBoxForTheVariant(BrandWordmarkVariant variant, string expectedViewBox)
    {
        var svg = BrandWordmarkSvg.Build(variant, reversed: false, height: "32px", title: "RV Intake");

        svg.Should().Contain($"viewBox=\"{expectedViewBox}\"");
    }

    [Fact]
    public void Build_ShouldNameTheBrandTypefaceWithFallbacks()
    {
        var svg = BrandWordmarkSvg.Build(BrandWordmarkVariant.Horizontal, reversed: true, height: "32px", title: "RV Intake");

        svg.Should().Contain("Space Grotesk");
        svg.Should().Contain("sans-serif");
        svg.Should().Contain("font-weight=\"700\"");
    }

    [Theory]
    [InlineData(BrandWordmarkVariant.Horizontal, true, true)]
    [InlineData(BrandWordmarkVariant.Icon, true, false)]
    [InlineData(BrandWordmarkVariant.TextOnly, false, true)]
    public void Build_ShouldIncludeOnlyThePartsTheVariantCallsFor(
        BrandWordmarkVariant variant, bool expectBadge, bool expectWord)
    {
        var svg = BrandWordmarkSvg.Build(variant, reversed: false, height: "32px", title: "RV Intake");

        svg.Contains("<rect", StringComparison.Ordinal).Should().Be(expectBadge);
        svg.Contains("Intake</tspan>", StringComparison.Ordinal).Should().Be(expectWord);
    }

    [Fact]
    public void Build_ShouldCarryAnAccessibleNameAndBeHiddenFromTheAccessibilityTreeTwice()
    {
        var svg = BrandWordmarkSvg.Build(BrandWordmarkVariant.Horizontal, reversed: false, height: "40px", title: "RV Intake Manager home");

        svg.Should().Contain("role=\"img\"");
        svg.Should().Contain("aria-label=\"RV Intake Manager home\"");
        svg.Should().Contain("<title>RV Intake Manager home</title>");
        svg.Should().Contain("height=\"40px\"");
    }

    [Fact]
    public void Build_ShouldEscapeATitleThatWouldOtherwiseBreakOutOfTheMarkup()
    {
        var svg = BrandWordmarkSvg.Build(BrandWordmarkVariant.Icon, reversed: false, height: "32px", title: "Ben & Jay's \"RV\" <shop>");

        svg.Should().NotContain("<shop>");
        svg.Should().Contain("&amp;");
        svg.Should().Contain("&lt;shop&gt;");
    }
}
