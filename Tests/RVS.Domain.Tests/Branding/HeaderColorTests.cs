using FluentAssertions;
using RVS.Domain.Branding;

namespace RVS.Domain.Tests.Branding;

/// <summary>
/// Tests for <see cref="HeaderColor"/> — parsing a dealer's header colour and choosing the
/// foreground that stays legible on it (<c>Spec A-16</c>, issue #470).
/// </summary>
public class HeaderColorTests
{
    [Theory]
    [InlineData("#2f4c6b", "#2F4C6B")]
    [InlineData("  #A8431F  ", "#A8431F")]
    public void Normalize_WhenSixDigitHex_ShouldReturnTrimmedUpperCase(string input, string expected)
    {
        HeaderColor.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#FFF")]
    [InlineData("green")]
    [InlineData("#2F4C6BFF")]
    public void Normalize_WhenBlankOrNotSixDigitHex_ShouldReturnNull(string? input)
    {
        HeaderColor.Normalize(input).Should().BeNull();
    }

    [Theory]
    [InlineData("#2F4C6B")]   // Denim — the product default
    [InlineData("#A8431F")]   // text-safe Rust
    [InlineData("#000000")]
    [InlineData("#1A5E20")]   // a dealer's dark green
    [InlineData("#B71C1C")]   // a dealer's red
    public void ForegroundFor_OnADarkColor_ShouldBeWhite(string background)
    {
        HeaderColor.ForegroundFor(background).Should().Be(HeaderColor.LightForeground);
        HeaderColor.PrefersLightForeground(background).Should().BeTrue();
    }

    [Theory]
    [InlineData("#FFFFFF")]
    [InlineData("#F6F1E7")]   // Cream
    [InlineData("#FFD54F")]   // a dealer's yellow
    [InlineData("#90CAF9")]   // a dealer's light blue
    public void ForegroundFor_OnALightColor_ShouldBeTheDarkGround(string background)
    {
        HeaderColor.ForegroundFor(background).Should().Be(HeaderColor.DarkForeground);
        HeaderColor.PrefersLightForeground(background).Should().BeFalse();
    }

    [Theory]
    [InlineData("#2F4C6B")]
    [InlineData("#FFD54F")]
    [InlineData("#777777")]   // mid grey — the worst case for either foreground
    [InlineData("#808080")]
    public void ForegroundFor_AnyColor_ShouldMeetTheThreeToOneNonTextContrastMinimum(string background)
    {
        var foreground = HeaderColor.ForegroundFor(background);

        HeaderColor.ContrastRatio(background, foreground).Should().BeGreaterThanOrEqualTo(3.0);
    }

    [Fact]
    public void ContrastRatio_BlackOnWhite_ShouldBeTwentyOne()
    {
        HeaderColor.ContrastRatio("#000000", "#FFFFFF").Should().BeApproximately(21.0, 0.01);
    }

    [Fact]
    public void ForegroundFor_WhenColorIsNotHex_ShouldThrowArgumentException()
    {
        var act = () => HeaderColor.ForegroundFor("green");

        act.Should().Throw<ArgumentException>();
    }

    // ── Darkening to a contrast floor (accent auto-darken, issue #470 follow-up) ──

    [Theory]
    [InlineData("#A8431F")]
    [InlineData("#000000")]
    public void DarkenToContrast_WhenTheColorAlreadyClearsTheFloor_ShouldReturnItUnchanged(string color)
    {
        HeaderColor.DarkenToContrast(color, "#FFFFFF", 4.5).Should().Be(color);
    }

    [Theory]
    [InlineData("#FFD54F")]
    [InlineData("#90CAF9")]
    [InlineData("#FF9800")]
    [InlineData("#E53935")]
    [InlineData("#FFFFFF")]
    public void DarkenToContrast_WhenTooLight_ShouldReturnTheLightestShadeThatClearsTheFloor(string color)
    {
        var darkened = HeaderColor.DarkenToContrast(color, "#FFFFFF", 4.5);

        var contrast = HeaderColor.ContrastRatio(darkened, "#FFFFFF");
        contrast.Should().BeGreaterThanOrEqualTo(4.5);
        contrast.Should().BeLessThan(4.8, "it should stop at the floor, not darken further than it has to");
    }

    [Theory]
    [InlineData("#FFD54F", 45)]    // amber
    [InlineData("#90CAF9", 207)]   // sky blue
    [InlineData("#FF9800", 36)]    // orange
    public void DarkenToContrast_ShouldKeepTheHue(string color, double hue)
    {
        var darkened = HeaderColor.DarkenToContrast(color, "#FFFFFF", 4.5);

        HeaderColor.Hue(darkened).Should().BeApproximately(hue, 3);
    }

    [Fact]
    public void DarkenToContrast_WhenColorIsNotHex_ShouldThrowArgumentException()
    {
        var act = () => HeaderColor.DarkenToContrast("yellow", "#FFFFFF", 4.5);

        act.Should().Throw<ArgumentException>();
    }
}
