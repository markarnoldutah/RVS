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
}
