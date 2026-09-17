using FluentAssertions;
using RVS.Domain.Links;

namespace RVS.Domain.Tests.Links;

/// <summary>
/// Tests for <see cref="IntakeLinkBuilder"/> — the single place customer-facing intake links
/// are composed (<c>Spec A-13</c>, issue #599).
/// </summary>
public class IntakeLinkBuilderTests
{
    private const string RedirectBase = "https://go.rvintake.com";
    private const string IntakeBase = "https://rvintake.com";
    private const string Slug = "nova-hurricane";

    // ── ShortLink ────────────────────────────────────────────────────────

    [Fact]
    public void ShortLink_ShouldPointAtTheRedirectHostAndCarryTheChannel()
    {
        IntakeLinkBuilder.ShortLink(RedirectBase, Slug, "qr")
            .Should().Be($"{RedirectBase}/{Slug}?src=qr");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("print")]
    public void ShortLink_WhenPrint_ShouldOmitTheQueryStringEntirely(string? source)
    {
        // Printed material cannot carry a query string — that absence is what defines the
        // print channel — and a bare URL is what stays typeable off a business card.
        IntakeLinkBuilder.ShortLink(RedirectBase, Slug, source)
            .Should().Be($"{RedirectBase}/{Slug}");
    }

    [Fact]
    public void ShortLink_ShouldTrimATrailingSlashOnTheBaseUrl()
    {
        IntakeLinkBuilder.ShortLink($"{RedirectBase}/", Slug, "qr")
            .Should().Be($"{RedirectBase}/{Slug}?src=qr");
    }

    [Fact]
    public void ShortLink_ShouldLowercaseAndTrimTheSlug()
    {
        IntakeLinkBuilder.ShortLink(RedirectBase, "  NOVA-Hurricane ", "qr")
            .Should().Be($"{RedirectBase}/{Slug}?src=qr");
    }

    [Fact]
    public void ShortLink_WhenSourceMalformed_ShouldFallBackToTheOtherChannel()
    {
        IntakeLinkBuilder.ShortLink(RedirectBase, Slug, "<script>")
            .Should().Be($"{RedirectBase}/{Slug}?src=other");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ShortLink_WhenSlugBlank_ShouldThrowArgumentException(string? slug)
    {
        var act = () => IntakeLinkBuilder.ShortLink(RedirectBase, slug!, "qr");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ShortLink_WhenBaseUrlBlank_ShouldThrowArgumentException(string? baseUrl)
    {
        var act = () => IntakeLinkBuilder.ShortLink(baseUrl!, Slug, "qr");

        act.Should().Throw<ArgumentException>();
    }

    // ── IntakeUrl ────────────────────────────────────────────────────────

    [Fact]
    public void IntakeUrl_ShouldAlwaysSpellOutTheChannelIncludingPrint()
    {
        // The intake app forwards src back on submission; omitting it would be
        // indistinguishable from a link that never passed the redirect.
        IntakeLinkBuilder.IntakeUrl(IntakeBase, Slug, null)
            .Should().Be($"{IntakeBase}/{Slug}?src=print");
    }

    [Fact]
    public void IntakeUrl_ShouldCarryAnUnknownChannelThrough()
    {
        IntakeLinkBuilder.IntakeUrl(IntakeBase, Slug, "nfc")
            .Should().Be($"{IntakeBase}/{Slug}?src=nfc");
    }
}
