using FluentAssertions;
using RVS.Domain.Links;
using RVS.Domain.Security;

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

    // ── Invite token pass-through (Spec A-14, issue #663) ─────────────────

    [Fact]
    public void ShortLink_WithInvite_ShouldCarryTheTokenAfterTheChannel()
    {
        var token = InviteToken.Generate();

        IntakeLinkBuilder.ShortLink(RedirectBase, Slug, "advisor", token)
            .Should().Be($"{RedirectBase}/{Slug}?src=advisor&inv={token}");
    }

    [Fact]
    public void ShortLink_WithInviteButNoQueryChannel_ShouldStartTheQueryStringWithTheToken()
    {
        var token = InviteToken.Generate();

        IntakeLinkBuilder.ShortLink(RedirectBase, Slug, null, token)
            .Should().Be($"{RedirectBase}/{Slug}?inv={token}");
    }

    [Fact]
    public void ShortLink_WithMalformedInvite_ShouldThrowArgumentException()
    {
        // Composition is ours: a malformed token here is a bug, not customer input.
        var act = () => IntakeLinkBuilder.ShortLink(RedirectBase, Slug, "advisor", "not a token");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IntakeUrl_WithInvite_ShouldCarryTheTokenThrough()
    {
        var token = InviteToken.Generate();

        IntakeLinkBuilder.IntakeUrl(IntakeBase, Slug, "advisor", token)
            .Should().Be($"{IntakeBase}/{Slug}?src=advisor&inv={token}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<script>")]
    [InlineData("tooshort")]
    public void IntakeUrl_WithMissingOrMalformedInvite_ShouldDropItAndStillBuildTheUrl(string? invite)
    {
        // The redirect never fails (Spec A-13, A-14): a mangled inv lands on a blank form.
        IntakeLinkBuilder.IntakeUrl(IntakeBase, Slug, "advisor", invite)
            .Should().Be($"{IntakeBase}/{Slug}?src=advisor");
    }
}
