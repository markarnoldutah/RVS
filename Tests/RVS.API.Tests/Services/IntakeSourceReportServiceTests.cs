using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Services;

/// <summary>
/// Tests for <see cref="IntakeSourceReportService"/> — the per-location channel report
/// (<c>Spec A-13</c>, issue #599). It joins the two stores channel data lives in: submissions
/// from Cosmos service requests, redirect hits from the append-only Table Storage hit log.
/// </summary>
public class IntakeSourceReportServiceTests
{
    private const string TenantId = "ten_nova_rv";
    private const string LocationId = "loc_hurricane";

    private readonly Mock<IServiceRequestRepository> _srRepoMock = new();
    private readonly Mock<IIntakeRedirectHitRepository> _hitRepoMock = new();
    private readonly IntakeSourceReportService _sut;

    public IntakeSourceReportServiceTests()
    {
        _srRepoMock.Setup(r => r.GetForAnalyticsAsync(
                It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _hitRepoMock.Setup(r => r.QueryAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _sut = new IntakeSourceReportService(
            _srRepoMock.Object,
            _hitRepoMock.Object,
            Mock.Of<ILogger<IntakeSourceReportService>>());
    }

    // ── Guards ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetForLocationAsync_WhenTenantIdBlank_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetForLocationAsync(tenantId!, LocationId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetForLocationAsync_WhenLocationIdBlank_ShouldThrowArgumentException(string? locationId)
    {
        var act = () => _sut.GetForLocationAsync(TenantId, locationId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Submissions ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetForLocationAsync_ShouldGroupSubmissionsByChannel()
    {
        GivenSubmissions("qr", "qr", "textrepl");

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        report.TotalSubmissions.Should().Be(3);
        report.Rows.Should().ContainSingle(r => r.Source == "qr").Which.Submissions.Should().Be(2);
        report.Rows.Should().ContainSingle(r => r.Source == "textrepl").Which.Submissions.Should().Be(1);
    }

    [Fact]
    public async Task GetForLocationAsync_WhenSubmissionHasNoChannel_ShouldCountItAsPrint()
    {
        // Requests created before channel tagging, and any print link that reached intake
        // without a src, belong to the print channel rather than a null bucket.
        GivenSubmissions(null, null, "qr");

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        report.Rows.Should().ContainSingle(r => r.Source == IntakeSourceVocabulary.Print)
            .Which.Submissions.Should().Be(2);
    }

    [Fact]
    public async Task GetForLocationAsync_ShouldNormaliseStoredChannelValues()
    {
        GivenSubmissions("QR", " qr ");

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        report.Rows.Should().ContainSingle(r => r.Source == "qr").Which.Submissions.Should().Be(2);
    }

    [Fact]
    public async Task GetForLocationAsync_ShouldOrderRowsBySubmissionsDescending()
    {
        GivenSubmissions("print", "qr", "qr", "qr", "textrepl", "textrepl");

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        report.Rows.Select(r => r.Source).Should().Equal("qr", "textrepl", "print");
    }

    [Fact]
    public async Task GetForLocationAsync_ShouldFlagWhetherAChannelIsOneOfTheTabledOnes()
    {
        GivenSubmissions("qr", "nfc");

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        report.Rows.Single(r => r.Source == "qr").IsKnownSource.Should().BeTrue();
        report.Rows.Single(r => r.Source == "nfc").IsKnownSource.Should().BeFalse();
    }

    // ── Redirect hits and conversion ─────────────────────────────────────

    [Fact]
    public async Task GetForLocationAsync_ShouldExcludeMachineFetchesFromReportedHits()
    {
        GivenHits(("qr", false), ("qr", false), ("qr", true), ("qr", true));

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        var row = report.Rows.Single(r => r.Source == "qr");
        row.RedirectHits.Should().Be(2);
        row.RawRedirectHits.Should().Be(4);
        report.TotalRedirectHits.Should().Be(2);
        report.TotalRawRedirectHits.Should().Be(4);
    }

    [Fact]
    public async Task GetForLocationAsync_ShouldComputeConversionRateFromNonBotHits()
    {
        GivenSubmissions("qr");
        GivenHits(("qr", false), ("qr", false), ("qr", false), ("qr", false), ("qr", true));

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        report.Rows.Single(r => r.Source == "qr").ConversionRate.Should().Be(0.25);
    }

    [Fact]
    public async Task GetForLocationAsync_WhenChannelHasNoNonBotHits_ShouldLeaveConversionRateNull()
    {
        // print is the normal case: printed links reach the redirect without a src, and cards
        // handed over in person may not touch the redirect at all.
        GivenSubmissions("print");

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        var row = report.Rows.Single(r => r.Source == IntakeSourceVocabulary.Print);
        row.Submissions.Should().Be(1);
        row.RedirectHits.Should().Be(0);
        row.ConversionRate.Should().BeNull();
    }

    [Fact]
    public async Task GetForLocationAsync_ShouldIncludeChannelsSeenOnlyInTheHitLog()
    {
        // A channel that gets opened but never converts is exactly what this report is for.
        GivenSubmissions("qr");
        GivenHits(("quickreply", false), ("quickreply", false));

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        var row = report.Rows.Single(r => r.Source == "quickreply");
        row.Submissions.Should().Be(0);
        row.RedirectHits.Should().Be(2);
        row.ConversionRate.Should().Be(0);
    }

    [Fact]
    public async Task GetForLocationAsync_ShouldRoundConversionRateToFourPlaces()
    {
        GivenSubmissions("qr");
        GivenHits(Enumerable.Repeat(("qr", false), 3).ToArray());

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        report.Rows.Single(r => r.Source == "qr").ConversionRate.Should().Be(0.3333);
    }

    // ── Windowing and scoping ────────────────────────────────────────────

    [Fact]
    public async Task GetForLocationAsync_ShouldScopeBothStoresToTheTenantLocationAndWindow()
    {
        var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

        var report = await _sut.GetForLocationAsync(TenantId, LocationId, from, to);

        report.LocationId.Should().Be(LocationId);
        report.FromUtc.Should().Be(from);
        report.ToUtc.Should().Be(to);

        _srRepoMock.Verify(r => r.GetForAnalyticsAsync(
            TenantId, from, to, LocationId, It.IsAny<CancellationToken>()), Times.Once);
        _hitRepoMock.Verify(r => r.QueryAsync(
            LocationId, from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetForLocationAsync_WhenNothingRecorded_ShouldReturnAnEmptyReport()
    {
        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        report.Rows.Should().BeEmpty();
        report.TotalSubmissions.Should().Be(0);
        report.TotalRedirectHits.Should().Be(0);
    }

    [Fact]
    public async Task GetForLocationAsync_WhenHitLogIsUnavailable_ShouldStillReportSubmissions()
    {
        // The hit log is a best-effort telemetry store; losing it must not take the
        // source-of-job numbers down with it.
        _hitRepoMock.Setup(r => r.QueryAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("table storage is having a day"));
        GivenSubmissions("qr", "qr");

        var report = await _sut.GetForLocationAsync(TenantId, LocationId);

        report.TotalSubmissions.Should().Be(2);
        report.Rows.Single(r => r.Source == "qr").RedirectHits.Should().Be(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void GivenSubmissions(params string?[] sources)
    {
        var requests = sources
            .Select((s, i) => new ServiceRequest
            {
                Id = $"sr_{i}",
                TenantId = TenantId,
                LocationId = LocationId,
                IntakeSource = s
            })
            .ToList();

        _srRepoMock.Setup(r => r.GetForAnalyticsAsync(
                It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);
    }

    private void GivenHits(params (string Source, bool IsBot)[] hits)
    {
        var records = hits
            .Select(h => new IntakeRedirectHit
            {
                LocationId = LocationId,
                TenantId = TenantId,
                Slug = "nova-hurricane",
                Source = h.Source,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                IsLikelyBot = h.IsBot
            })
            .ToList();

        _hitRepoMock.Setup(r => r.QueryAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);
    }
}
