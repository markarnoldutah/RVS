using FluentAssertions;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="LocationBrandingValidator"/> — the per-location logo and header colour
/// a dealer shows on its intake form and packet (<c>Spec A-16</c>, issue #470).
/// </summary>
public class LocationBrandingValidatorTests
{
    [Fact]
    public void Validate_WhenBrandingIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => LocationBrandingValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_WhenBrandingIsEmpty_ShouldSucceed()
    {
        LocationBrandingValidator.Validate(new LocationBrandingEmbedded()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenLogoAndColorAreValid_ShouldSucceed()
    {
        var branding = new LocationBrandingEmbedded
        {
            LogoUrl = "https://cdn.dealer.example/logo.png",
            HeaderColor = "#1A5E20",
        };

        LocationBrandingValidator.Validate(branding).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenLogoUrlIsInvalid_ShouldFail()
    {
        var branding = new LocationBrandingEmbedded { LogoUrl = "http://cdn.dealer.example/logo.png" };

        LocationBrandingValidator.Validate(branding).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenHeaderColorIsInvalid_ShouldFail()
    {
        var branding = new LocationBrandingEmbedded { HeaderColor = "green" };

        LocationBrandingValidator.Validate(branding).IsValid.Should().BeFalse();
    }

    // ── Logo URL ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://cdn.dealer.example/logo.png")]
    [InlineData("https://dealer.example/assets/brand/logo.jpg?v=2")]
    public void ValidateLogoUrl_BlankOrAbsoluteHttpsUrl_ShouldSucceed(string? url)
    {
        LocationBrandingValidator.ValidateLogoUrl(url).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://cdn.dealer.example/logo.png")]   // mixed content on the https intake page
    [InlineData("ftp://cdn.dealer.example/logo.png")]
    [InlineData("/brand/logo.png")]
    [InlineData("not a url")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("javascript:alert(1)")]
    public void ValidateLogoUrl_WhenNotAnAbsoluteHttpsUrl_ShouldFailWithHttpsMessage(string url)
    {
        var result = LocationBrandingValidator.ValidateLogoUrl(url);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("https");
    }

    [Fact]
    public void ValidateLogoUrl_WhenLongerThan2048Characters_ShouldFail()
    {
        var url = "https://cdn.dealer.example/" + new string('a', 2048);

        LocationBrandingValidator.ValidateLogoUrl(url).IsValid.Should().BeFalse();
    }

    // ── Header colour ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("#2F4C6B")]
    [InlineData("#a8431f")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void ValidateHeaderColor_BlankOrSixDigitHex_ShouldSucceed(string? color)
    {
        LocationBrandingValidator.ValidateHeaderColor(color).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("2F4C6B")]      // no leading #
    [InlineData("#FFF")]        // shorthand
    [InlineData("#2F4C6BFF")]   // alpha
    [InlineData("#GGGGGG")]
    [InlineData("rgb(0,0,0)")]
    [InlineData("denim")]
    public void ValidateHeaderColor_WhenNotSixDigitHex_ShouldFail(string color)
    {
        var result = LocationBrandingValidator.ValidateHeaderColor(color);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("#RRGGBB");
    }

    // ── Accent colour: replaces Rust for buttons, links, focus (issue #470 follow-up) ──

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("#A8431F")]   // text-safe Rust itself — 6.02:1
    [InlineData("#1A5E20")]   // a dealer's dark green
    [InlineData("#0D47A1")]   // a dealer's navy
    [InlineData("#767676")]   // mid grey, just over 4.5:1
    [InlineData("#C1502E")]   // logo Rust: fails AA on cream, but clears it on the white page
    public void ValidateAccentColor_BlankOrTextSafeOnWhite_ShouldSucceed(string? color)
    {
        LocationBrandingValidator.ValidateAccentColor(color).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("#FFD54F")]   // yellow
    [InlineData("#90CAF9")]   // light blue
    [InlineData("#FF9800")]   // bright orange
    [InlineData("#777777")]   // mid grey, just under 4.5:1
    public void ValidateAccentColor_WhenTooLightForTextOnWhite_ShouldFailAndSayWhy(string color)
    {
        var result = LocationBrandingValidator.ValidateAccentColor(color);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("too light").And.Contain("4.5:1").And.Contain("darker");
    }

    [Theory]
    [InlineData("A8431F")]
    [InlineData("#FFF")]
    [InlineData("rust")]
    public void ValidateAccentColor_WhenNotSixDigitHex_ShouldFail(string color)
    {
        var result = LocationBrandingValidator.ValidateAccentColor(color);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("#RRGGBB");
    }

    [Fact]
    public void Validate_WhenAccentColorIsTooLight_ShouldFail()
    {
        var branding = new LocationBrandingEmbedded { AccentColor = "#FFD54F" };

        LocationBrandingValidator.Validate(branding).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("#A8431F", true)]
    [InlineData("#FFD54F", false)]
    [InlineData(null, false)]
    [InlineData("green", false)]
    public void IsTextSafeAccent_ShouldHoldOnlyForHexThatClearsFourPointFiveOnWhite(string? color, bool expected)
    {
        LocationBrandingValidator.IsTextSafeAccent(color).Should().Be(expected);
    }
}
