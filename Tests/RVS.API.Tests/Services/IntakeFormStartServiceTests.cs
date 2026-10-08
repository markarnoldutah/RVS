using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Services;

/// <summary>
/// Tests for <see cref="IntakeFormStartService"/> — the completion-rate denominator
/// (<c>Spec A-13</c>, issue #839). The form never waits on it, so nothing in it may fail the
/// call: an unknown slug, an expired one, a junk session id or a storage outage costs the row
/// and nothing else.
/// </summary>
public class IntakeFormStartServiceTests
{
    private const string Slug = "nova-hurricane";
    private const string TenantId = "ten_nova_rv";
    private const string LocationId = "loc_hurricane";
    private const string SessionId = "3f2b8c0e9d4a4f6b8e1c2d3a4b5c6d7e";
    private const string BrowserUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";

    private readonly Mock<ISlugLookupRepository> _slugLookupRepoMock = new();
    private readonly Mock<ITenantConfigRepository> _tenantConfigRepoMock = new();
    private readonly Mock<IIntakeFormStartRepository> _startRepoMock = new();
    private readonly IntakeFormStartService _sut;

    public IntakeFormStartServiceTests()
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

        _sut = new IntakeFormStartService(
            _slugLookupRepoMock.Object,
            _tenantConfigRepoMock.Object,
            _startRepoMock.Object,
            Mock.Of<ILogger<IntakeFormStartService>>());
    }

    // ── Guards ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordAsync_WhenSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var act = () => _sut.RecordAsync(slug!, SessionId, "qr", BrowserUserAgent);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── The row ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordAsync_WhenSlugResolves_ShouldAppendOneStartForTheLocation()
    {
        IntakeFormStart? recorded = null;
        _startRepoMock.Setup(r => r.AppendAsync(It.IsAny<IntakeFormStart>(), It.IsAny<CancellationToken>()))
            .Callback<IntakeFormStart, CancellationToken>((s, _) => recorded = s)
            .Returns(Task.CompletedTask);

        var before = DateTimeOffset.UtcNow;
        await _sut.RecordAsync(Slug, SessionId, "qr", BrowserUserAgent);

        _startRepoMock.Verify(r => r.AppendAsync(It.IsAny<IntakeFormStart>(), It.IsAny<CancellationToken>()), Times.Once);
        recorded.Should().NotBeNull();
        recorded!.LocationId.Should().Be(LocationId);
        recorded.TenantId.Should().Be(TenantId);
        recorded.Slug.Should().Be(Slug);
        recorded.Source.Should().Be(IntakeSourceVocabulary.Qr);
        recorded.SessionId.Should().Be(SessionId);
        recorded.IsLikelyBot.Should().BeFalse();
        recorded.UserAgent.Should().Be(BrowserUserAgent);
        recorded.OccurredAtUtc.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task RecordAsync_WhenSlugHasCaseAndWhitespace_ShouldNormaliseBeforeLookup()
    {
        await _sut.RecordAsync("  Nova-Hurricane ", SessionId, null, BrowserUserAgent);

        _startRepoMock.Verify(r => r.AppendAsync(
            It.Is<IntakeFormStart>(s => s.Slug == Slug), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, IntakeSourceVocabulary.Print)]
    [InlineData("", IntakeSourceVocabulary.Print)]
    [InlineData(" ADVISOR ", IntakeSourceVocabulary.Advisor)]
    [InlineData("not a tag!", IntakeSourceVocabulary.Other)]
    public async Task RecordAsync_ShouldNormaliseTheSourceTheSameWayRedirectHitsDo(string? src, string expected)
    {
        await _sut.RecordAsync(Slug, SessionId, src, BrowserUserAgent);

        _startRepoMock.Verify(r => r.AppendAsync(
            It.Is<IntakeFormStart>(s => s.Source == expected), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordAsync_WhenUserAgentIsABot_ShouldFlagItAndStillRecord()
    {
        await _sut.RecordAsync(Slug, SessionId, "qr", "Googlebot/2.1 (+http://www.google.com/bot.html)");

        _startRepoMock.Verify(r => r.AppendAsync(
            It.Is<IntakeFormStart>(s => s.IsLikelyBot), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordAsync_WhenUserAgentIsLong_ShouldTruncateIt()
    {
        var userAgent = new string('x', IntakeRedirectHit.MaxUserAgentLength + 50);

        await _sut.RecordAsync(Slug, SessionId, "qr", userAgent);

        _startRepoMock.Verify(r => r.AppendAsync(
            It.Is<IntakeFormStart>(s => s.UserAgent!.Length == IntakeRedirectHit.MaxUserAgentLength),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordAsync_WhenUserAgentIsBlank_ShouldStoreNull()
    {
        await _sut.RecordAsync(Slug, SessionId, "qr", "  ");

        _startRepoMock.Verify(r => r.AppendAsync(
            It.Is<IntakeFormStart>(s => s.UserAgent == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Nothing written ──────────────────────────────────────────────────

    [Fact]
    public async Task RecordAsync_WhenSlugIsUnknown_ShouldNotWrite()
    {
        await _sut.RecordAsync("no-such-slug", SessionId, "qr", BrowserUserAgent);

        _startRepoMock.Verify(r => r.AppendAsync(It.IsAny<IntakeFormStart>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_WhenSlugLookupThrows_ShouldNotWriteOrThrow()
    {
        _slugLookupRepoMock.Setup(r => r.GetBySlugAsync(Slug, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cosmos down"));

        var act = () => _sut.RecordAsync(Slug, SessionId, "qr", BrowserUserAgent);

        await act.Should().NotThrowAsync();
        _startRepoMock.Verify(r => r.AppendAsync(It.IsAny<IntakeFormStart>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_WhenSlugHasExpired_ShouldNotWrite()
    {
        // Spec A-19: past the 60-day capture window the form is replaced by a notice, so there
        // is nothing to start.
        _tenantConfigRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTenantConfig(loginsEnabled: false, disabledDaysAgo: 61));

        await _sut.RecordAsync(Slug, SessionId, "qr", BrowserUserAgent);

        _startRepoMock.Verify(r => r.AppendAsync(It.IsAny<IntakeFormStart>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_WhenTenantDisabledWithinCaptureWindow_ShouldStillWrite()
    {
        _tenantConfigRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTenantConfig(loginsEnabled: false, disabledDaysAgo: 30));

        await _sut.RecordAsync(Slug, SessionId, "qr", BrowserUserAgent);

        _startRepoMock.Verify(r => r.AppendAsync(It.IsAny<IntakeFormStart>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad id")]
    [InlineData("<script>")]
    public async Task RecordAsync_WhenSessionIdIsMissingOrMalformed_ShouldNotWrite(string? sessionId)
    {
        await _sut.RecordAsync(Slug, sessionId, "qr", BrowserUserAgent);

        _startRepoMock.Verify(r => r.AppendAsync(It.IsAny<IntakeFormStart>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Never fails ──────────────────────────────────────────────────────

    [Fact]
    public async Task RecordAsync_WhenStorageThrows_ShouldStillComplete()
    {
        _startRepoMock.Setup(r => r.AppendAsync(It.IsAny<IntakeFormStart>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("table storage down"));

        var act = () => _sut.RecordAsync(Slug, SessionId, "qr", BrowserUserAgent);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordAsync_WhenTenantConfigLookupThrows_ShouldStillWrite()
    {
        // The expiry check exists to keep notice-page visits out of the count. Failing it costs
        // at most a row that should not have been there; it does not cost a real start.
        _tenantConfigRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cosmos down"));

        await _sut.RecordAsync(Slug, SessionId, "qr", BrowserUserAgent);

        _startRepoMock.Verify(r => r.AppendAsync(It.IsAny<IntakeFormStart>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TenantConfig BuildTenantConfig(bool loginsEnabled, int disabledDaysAgo) => new()
    {
        Id = TenantId,
        TenantId = TenantId,
        AccessGate = new TenantAccessGateEmbedded
        {
            LoginsEnabled = loginsEnabled,
            DisabledReason = loginsEnabled ? null : "PastDue",
            DisabledAtUtc = loginsEnabled ? null : DateTimeOffset.UtcNow.AddDays(-disabledDaysAgo),
        },
        CreatedByUserId = "admin",
    };
}
