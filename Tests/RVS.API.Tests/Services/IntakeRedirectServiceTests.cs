using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Options;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace RVS.API.Tests.Services;

/// <summary>
/// Tests for <see cref="IntakeRedirectService"/> — the <c>go.rvintake.com</c> short-link
/// resolver behind every distribution channel (<c>Spec A-13</c>, issue #599).
///
/// The governing rule throughout: the redirect is the customer's journey and the hit log is
/// telemetry. Nothing in the logging path — an unknown slug, a nonsense <c>src</c>, a storage
/// outage — is allowed to cost the customer their redirect.
/// </summary>
public class IntakeRedirectServiceTests
{
    private const string IntakeBaseUrl = "https://rvintake.com";
    private const string Slug = "nova-hurricane";
    private const string TenantId = "ten_nova_rv";
    private const string LocationId = "loc_hurricane";
    private const string BrowserUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";

    private readonly Mock<ISlugLookupRepository> _slugLookupRepoMock = new();
    private readonly Mock<IIntakeRedirectHitRepository> _hitRepoMock = new();
    private readonly IntakeRedirectService _sut;

    public IntakeRedirectServiceTests()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync(Slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup
            {
                Slug = Slug,
                TenantId = TenantId,
                LocationId = LocationId,
                DealershipName = "Nova RV",
                LocationName = "Hurricane"
            });

        _sut = new IntakeRedirectService(
            _slugLookupRepoMock.Object,
            _hitRepoMock.Object,
            MsOptions.Create(new IntakeUrlOptions { BaseUrl = IntakeBaseUrl }),
            Mock.Of<ILogger<IntakeRedirectService>>());
    }

    // ── Redirect target ──────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ShouldRedirectToTheLocationIntakeUrlCarryingTheSource()
    {
        var result = await _sut.ResolveAsync(Slug, "qr", BrowserUserAgent);

        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=qr");
        result.Source.Should().Be(IntakeSourceVocabulary.Qr);
        result.SlugResolved.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_WhenBaseUrlHasTrailingSlash_ShouldNotDoubleIt()
    {
        var sut = new IntakeRedirectService(
            _slugLookupRepoMock.Object,
            _hitRepoMock.Object,
            MsOptions.Create(new IntakeUrlOptions { BaseUrl = $"{IntakeBaseUrl}/" }),
            Mock.Of<ILogger<IntakeRedirectService>>());

        var result = await sut.ResolveAsync(Slug, "qr", BrowserUserAgent);

        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=qr");
    }

    [Fact]
    public async Task ResolveAsync_WhenSrcAbsent_ShouldDefaultToPrint()
    {
        // Printed material cannot carry a query string, so "no src" is the print channel.
        var result = await _sut.ResolveAsync(Slug, null, BrowserUserAgent);

        result.Source.Should().Be(IntakeSourceVocabulary.Print);
        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=print");
    }

    [Fact]
    public async Task ResolveAsync_WhenSrcUnknown_ShouldStillRedirectAndKeepTheValue()
    {
        var result = await _sut.ResolveAsync(Slug, "nfc", BrowserUserAgent);

        result.Source.Should().Be("nfc");
        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=nfc");
    }

    [Fact]
    public async Task ResolveAsync_WhenSrcMalformed_ShouldStillRedirectAndRecordItAsOther()
    {
        var result = await _sut.ResolveAsync(Slug, "<script>alert(1)</script>", BrowserUserAgent);

        result.Source.Should().Be(IntakeSourceVocabulary.Other);
        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=other");
    }

    [Fact]
    public async Task ResolveAsync_ShouldNormaliseTheSlugBeforeResolvingIt()
    {
        var result = await _sut.ResolveAsync($"  {Slug.ToUpperInvariant()}  ", "qr", BrowserUserAgent);

        result.SlugResolved.Should().BeTrue();
        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=qr");
    }

    // ── Unknown slugs ────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenSlugUnknown_ShouldStillRedirectAndLetIntakeShowNotFound()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);

        var result = await _sut.ResolveAsync("gone", "qr", BrowserUserAgent);

        result.SlugResolved.Should().BeFalse();
        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/gone?src=qr");
    }

    [Fact]
    public async Task ResolveAsync_WhenSlugUnknown_ShouldRecordTheHitUnderTheUnresolvedPartition()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync("gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);

        await _sut.ResolveAsync("gone", "qr", BrowserUserAgent);

        _hitRepoMock.Verify(r => r.AppendAsync(
            It.Is<IntakeRedirectHit>(h =>
                h.LocationId == IntakeRedirectHit.UnresolvedLocationId
                && h.TenantId == null
                && h.Slug == "gone"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WhenSlugLookupThrows_ShouldStillRedirect()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync(Slug, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cosmos is having a day"));

        var result = await _sut.ResolveAsync(Slug, "qr", BrowserUserAgent);

        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=qr");
        result.SlugResolved.Should().BeFalse();
    }

    // ── Hit logging ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ShouldAppendOneHitPartitionedByLocation()
    {
        await _sut.ResolveAsync(Slug, "textrepl", BrowserUserAgent);

        _hitRepoMock.Verify(r => r.AppendAsync(
            It.Is<IntakeRedirectHit>(h =>
                h.LocationId == LocationId
                && h.TenantId == TenantId
                && h.Slug == Slug
                && h.Source == IntakeSourceVocabulary.TextReplacement
                && !h.IsLikelyBot),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WhenUserAgentIsALinkPreviewFetcher_ShouldFlagTheHitAsABot()
    {
        // iMessage and friends fetch the URL to build a preview before anyone taps it.
        await _sut.ResolveAsync(Slug, "textrepl", "facebookexternalhit/1.1");

        _hitRepoMock.Verify(r => r.AppendAsync(
            It.Is<IntakeRedirectHit>(h => h.IsLikelyBot),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ShouldTruncateAnOverlongUserAgent()
    {
        var overlong = new string('u', IntakeRedirectHit.MaxUserAgentLength + 50);

        await _sut.ResolveAsync(Slug, "qr", overlong);

        _hitRepoMock.Verify(r => r.AppendAsync(
            It.Is<IntakeRedirectHit>(h =>
                h.UserAgent != null && h.UserAgent.Length == IntakeRedirectHit.MaxUserAgentLength),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WhenHitLogThrows_ShouldStillRedirect()
    {
        _hitRepoMock.Setup(r => r.AppendAsync(It.IsAny<IntakeRedirectHit>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("table storage is having a day"));

        var result = await _sut.ResolveAsync(Slug, "qr", BrowserUserAgent);

        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=qr");
        result.SlugResolved.Should().BeTrue();
    }

    // ── Guards ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_WhenSlugBlank_ShouldThrowArgumentException(string? slug)
    {
        var act = () => _sut.ResolveAsync(slug!, "qr", BrowserUserAgent);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Invite pass-through (Spec A-14, issue #663) ──────────────────────

    [Fact]
    public async Task ResolveAsync_WithAnInviteToken_ShouldCarryItToTheIntakeUrl()
    {
        const string token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        var result = await _sut.ResolveAsync(Slug, "advisor", BrowserUserAgent, token);

        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=advisor&inv={token}");
    }

    [Fact]
    public async Task ResolveAsync_WithAMalformedInviteToken_ShouldDropItAndStillRedirect()
    {
        var result = await _sut.ResolveAsync(Slug, "advisor", BrowserUserAgent, "<script>");

        result.TargetUrl.Should().Be($"{IntakeBaseUrl}/{Slug}?src=advisor");
    }
}
