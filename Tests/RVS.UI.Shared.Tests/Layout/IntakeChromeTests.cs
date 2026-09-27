using FluentAssertions;
using RVS.Blazor.Intake.Layout;
using RVS.Domain.Branding;
using RVS.Domain.DTOs;
using RVS.Domain.Validation;
using RVS.UI.Shared.Theme;

namespace RVS.UI.Shared.Tests.Layout;

/// <summary>
/// Tests for <see cref="IntakeChrome"/> — whether the Intake app's header bar shows a dealer's
/// logo and colour or the RV Intake defaults (<c>Spec A-16</c>, issue #470).
/// </summary>
public class IntakeChromeTests
{
    private const string Slug = "acme-rv-slc";
    private const string LogoUrl = "https://cdn.acme.example/logo.png";

    private static IntakeConfigResponseDto Config(
        string? logoUrl = LogoUrl, string? headerColor = "#1A5E20", string? accentColor = null) => new()
    {
        LocationName = "Salt Lake City",
        LocationSlug = Slug,
        DealershipName = "Acme RV",
        Branding = new LocationBrandingDto { LogoUrl = logoUrl, HeaderColor = headerColor, AccentColor = accentColor },
    };

    [Fact]
    public void Resolve_WhenNoConfigIsLoaded_ShouldUseTheDefaults()
    {
        var chrome = IntakeChrome.Resolve(null, Slug, highContrast: false);

        chrome.Should().Be(IntakeChrome.Default);
        chrome.HasDealerLogo.Should().BeFalse();
        chrome.AppBarStyle.Should().BeNull();
        chrome.UseReversedMark.Should().BeTrue("the default bar is Ink, a dark surface");
    }

    [Theory]
    [InlineData(Slug)]
    [InlineData("ACME-RV-SLC")]
    [InlineData(Slug + "?src=qr&step=3")]
    [InlineData(Slug + "/")]
    [InlineData(Slug + "#top")]
    public void Resolve_OnTheLocationsIntakePage_ShouldShowTheDealersLogoAndColor(string path)
    {
        var chrome = IntakeChrome.Resolve(Config(), path, highContrast: false);

        chrome.DealerLogoUrl.Should().Be(LogoUrl);
        chrome.DealerName.Should().Be("Acme RV");
        chrome.AppBarBackground.Should().Be("#1A5E20");
        chrome.AppBarForeground.Should().Be(HeaderColor.LightForeground);
        chrome.AppBarStyle.Should().Be("background-color:#1A5E20;color:#FFFFFF;");
    }

    [Theory]
    [InlineData("")]                  // the RV Intake homepage
    [InlineData("privacy")]
    [InlineData("terms")]
    [InlineData("status/abc123")]
    [InlineData("acme-rv-slc-2")]     // a different location whose slug starts the same way
    public void Resolve_OffTheLocationsIntakePage_ShouldUseTheDefaults(string path)
    {
        // The policies and the homepage are RV Intake's, not the dealer's, even mid-session.
        IntakeChrome.Resolve(Config(), path, highContrast: false).Should().Be(IntakeChrome.Default);
    }

    [Fact]
    public void Resolve_WhenTheLocationSetsNoBranding_ShouldUseTheDefaults()
    {
        IntakeChrome.Resolve(Config(logoUrl: null, headerColor: null), Slug, highContrast: false)
            .Should().Be(IntakeChrome.Default);
    }

    [Fact]
    public void Resolve_WhenOnlyALogoIsSet_ShouldKeepTheDefaultBar()
    {
        var chrome = IntakeChrome.Resolve(Config(headerColor: null), Slug, highContrast: false);

        chrome.DealerLogoUrl.Should().Be(LogoUrl);
        chrome.AppBarStyle.Should().BeNull();
        chrome.UseReversedMark.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WhenOnlyAColorIsSet_ShouldKeepTheRvIntakeMarkOnTheDealersColor()
    {
        var chrome = IntakeChrome.Resolve(Config(logoUrl: null), Slug, highContrast: false);

        chrome.HasDealerLogo.Should().BeFalse();
        chrome.AppBarBackground.Should().Be("#1A5E20");
    }

    [Fact]
    public void Resolve_OnALightDealerColor_ShouldUseTheDarkForegroundAndTheUnreversedMark()
    {
        var chrome = IntakeChrome.Resolve(Config(logoUrl: null, headerColor: "#ffd54f"), Slug, highContrast: false);

        chrome.AppBarBackground.Should().Be("#FFD54F");
        chrome.AppBarForeground.Should().Be(HeaderColor.DarkForeground);
        chrome.UseReversedMark.Should().BeFalse();
    }

    [Fact]
    public void Resolve_InHighContrastMode_ShouldIgnoreTheDealersColorButKeepTheLogo()
    {
        // High contrast is deliberately not brand-coloured, the dealer's brand included.
        var chrome = IntakeChrome.Resolve(Config(), Slug, highContrast: true);

        chrome.AppBarStyle.Should().BeNull();
        chrome.DealerLogoUrl.Should().Be(LogoUrl);
    }

    [Theory]
    [InlineData("http://cdn.acme.example/logo.png")]
    [InlineData("javascript:alert(1)")]
    public void Resolve_WhenTheLogoIsNotHttps_ShouldIgnoreIt(string logoUrl)
    {
        IntakeChrome.Resolve(Config(logoUrl: logoUrl), Slug, highContrast: false).HasDealerLogo.Should().BeFalse();
    }

    [Fact]
    public void Resolve_WhenTheColorIsNotHex_ShouldIgnoreIt()
    {
        IntakeChrome.Resolve(Config(headerColor: "green"), Slug, highContrast: false).AppBarStyle.Should().BeNull();
    }

    [Fact]
    public void Resolve_WhenTheDealershipNameIsBlank_ShouldNameTheLogoAfterTheLocation()
    {
        var config = Config() with { DealershipName = "" };

        IntakeChrome.Resolve(config, Slug, highContrast: false).DealerName.Should().Be("Salt Lake City");
    }

    // ── The white plate: only where the dealer did not choose the colour ──

    [Fact]
    public void LogoOnPlate_WhenTheDealerSetsAColor_ShouldBeFalse()
    {
        // The dealer chose the logo and the colour together; a plate would hide a white logo
        // drawn for a dark header and read as a sticker on their own colour.
        IntakeChrome.Resolve(Config(), Slug, highContrast: false).LogoOnPlate.Should().BeFalse();
    }

    [Fact]
    public void LogoOnPlate_WhenTheBarKeepsTheDefaultInk_ShouldBeTrue()
    {
        // A dark logo on Ink the dealer never picked would be unreadable.
        IntakeChrome.Resolve(Config(headerColor: null), Slug, highContrast: false).LogoOnPlate.Should().BeTrue();
    }

    [Fact]
    public void LogoOnPlate_InHighContrastMode_ShouldBeTrue()
    {
        // The dealer's colour is ignored and the bar is black.
        IntakeChrome.Resolve(Config(), Slug, highContrast: true).LogoOnPlate.Should().BeTrue();
    }

    [Fact]
    public void LogoOnPlate_WithNoDealerLogo_ShouldBeFalse()
    {
        IntakeChrome.Resolve(Config(logoUrl: null), Slug, highContrast: false).LogoOnPlate.Should().BeFalse();
    }

    // ── Step 0 hero: same surface rules as the bar ─────────────────────────

    [Fact]
    public void ForLanding_ShouldResolveAsTheLocationsOwnPage()
    {
        var chrome = IntakeChrome.ForLanding(Config(), highContrast: false);

        chrome.DealerLogoUrl.Should().Be(LogoUrl);
        chrome.AppBarBackground.Should().Be("#1A5E20");
        chrome.LogoOnPlate.Should().BeFalse();
    }

    [Fact]
    public void ForLanding_WhenNoConfig_ShouldUseTheDefaults()
    {
        IntakeChrome.ForLanding(null, highContrast: false).Should().Be(IntakeChrome.Default);
    }

    // ── Accent: replaces Rust for buttons, links, focus (issue #470 follow-up) ──

    [Fact]
    public void Resolve_OnTheLocationsIntakePage_ShouldCarryTheDealersAccent()
    {
        IntakeChrome.Resolve(Config(accentColor: "#0d47a1"), Slug, highContrast: false)
            .AccentColor.Should().Be("#0D47A1");
    }

    [Fact]
    public void Resolve_WhenOnlyAnAccentIsSet_ShouldNotBeTheDefaults()
    {
        var chrome = IntakeChrome.Resolve(Config(logoUrl: null, headerColor: null, accentColor: "#0D47A1"), Slug, highContrast: false);

        chrome.Should().NotBe(IntakeChrome.Default);
        chrome.AccentColor.Should().Be("#0D47A1");
        chrome.AppBarStyle.Should().BeNull();
    }

    [Fact]
    public void Resolve_OffTheLocationsIntakePage_ShouldNotCarryTheAccent()
    {
        IntakeChrome.Resolve(Config(accentColor: "#0D47A1"), "privacy", highContrast: false)
            .AccentColor.Should().BeNull();
    }

    [Fact]
    public void Resolve_InHighContrastMode_ShouldIgnoreTheAccent()
    {
        IntakeChrome.Resolve(Config(accentColor: "#0D47A1"), Slug, highContrast: true)
            .AccentColor.Should().BeNull();
    }

    [Fact]
    public void Resolve_WhenTheAccentIsTooLightForText_ShouldCarryADarkerShadeOfIt()
    {
        var accent = IntakeChrome.Resolve(Config(accentColor: "#FFD54F"), Slug, highContrast: false).AccentColor;

        accent.Should().Be(LocationBrandingValidator.EffectiveAccent("#FFD54F"));
        HeaderColor.ContrastRatio(accent!, "#FFFFFF").Should().BeGreaterThanOrEqualTo(4.5);
    }

    [Fact]
    public void Resolve_WhenTheAccentIsNotHex_ShouldIgnoreIt()
    {
        IntakeChrome.Resolve(Config(accentColor: "blue"), Slug, highContrast: false)
            .AccentColor.Should().BeNull();
    }

    [Fact]
    public void ThemeOr_WithAnAccent_ShouldUseTheAccentTheme()
    {
        var chrome = IntakeChrome.Resolve(Config(accentColor: "#0D47A1"), Slug, highContrast: false);

        chrome.ThemeOr(IntakeTheme.Theme).Should().BeSameAs(IntakeTheme.WithAccent("#0D47A1"));
    }

    [Fact]
    public void ThemeOr_WithoutAnAccent_ShouldUseTheModesTheme()
    {
        IntakeChrome.Default.ThemeOr(IntakeTheme.HighContrast).Should().BeSameAs(IntakeTheme.HighContrast);
    }

    // ── Landing-step logo ────────────────────────────────────────────────

    [Fact]
    public void DealerLogoUrlOf_WhenTheLocationHasAnHttpsLogo_ShouldReturnIt()
    {
        IntakeChrome.DealerLogoUrlOf(Config()).Should().Be(LogoUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("http://cdn.acme.example/logo.png")]
    public void DealerLogoUrlOf_WhenThereIsNoUsableLogo_ShouldReturnNull(string? logoUrl)
    {
        IntakeChrome.DealerLogoUrlOf(Config(logoUrl: logoUrl)).Should().BeNull();
        IntakeChrome.DealerLogoUrlOf(null).Should().BeNull();
    }
}
