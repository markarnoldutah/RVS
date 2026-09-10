using FluentAssertions;
using RVS.Domain.Integrations;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketEmailSizeFitter"/> — the pure transform that trims a packet
/// email's attachment set to what Azure Communication Services will actually accept
/// (<c>Spec B-4</c>, issue #521).
///
/// Contract under test: the PDF outranks every photo; photos survive as a contiguous prefix
/// so the ones the packet shows on page one are the ones that ship; base64 inflation and both
/// message bodies are counted against the budget; a PDF that cannot fit is dropped while photos
/// still fill the budget (a regression shape — a real PDF is roughly 1.5–3 MB); and the kept set
/// never exceeds the budget. The transform is
/// deterministic and never throws over a merely oversized input — dropping an attachment is
/// the designed outcome, not an error.
/// </summary>
public class PacketEmailSizeFitterTests
{
    private const string Html = "<!DOCTYPE html><html><body><h1>Packet</h1></body></html>";
    private const string PlainText = "----- RV SERVICE FLOW -----\nCATEGORY: SLIDE SYSTEM\n----- RV SERVICE FLOW -----";

    private static PacketEmailAttachment Pdf(int bytes) => new()
    {
        FileName = "service-packet-A1B2C3D4.pdf",
        ContentType = "application/pdf",
        Content = new byte[bytes],
    };

    private static PacketEmailAttachment Photo(int index, int bytes) => new()
    {
        FileName = $"photo-{index}.jpg",
        ContentType = "image/jpeg",
        Content = new byte[bytes],
    };

    /// <summary>A budget generous enough that nothing is ever dropped for size.</summary>
    private const long RoomyBudget = 10_000_000;

    // ── Guards ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Fit_WhenHtmlBodyIsNullOrWhiteSpace_ShouldThrowArgumentException(string? html)
    {
        var act = () => PacketEmailSizeFitter.Fit([Pdf(100)], html!, PlainText, RoomyBudget);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Fit_WhenPlainTextBodyIsNullOrWhiteSpace_ShouldThrowArgumentException(string? plainText)
    {
        var act = () => PacketEmailSizeFitter.Fit([Pdf(100)], Html, plainText!, RoomyBudget);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fit_WhenBudgetIsNotPositive_ShouldThrowArgumentOutOfRangeException(long budget)
    {
        var act = () => PacketEmailSizeFitter.Fit([Pdf(100)], Html, PlainText, budget);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fit_WhenAttachmentsIsNull_ShouldReturnNothingKeptAndNothingDropped()
    {
        var result = PacketEmailSizeFitter.Fit(null, Html, PlainText, RoomyBudget);

        result.Attachments.Should().BeEmpty();
        result.Dropped.Should().BeEmpty();
        result.AnythingDropped.Should().BeFalse();
    }

    [Fact]
    public void Fit_WhenAttachmentsIsEmpty_ShouldReturnNothingKeptAndNothingDropped()
    {
        var result = PacketEmailSizeFitter.Fit([], Html, PlainText, RoomyBudget);

        result.Attachments.Should().BeEmpty();
        result.Dropped.Should().BeEmpty();
    }

    // ── The happy path ─────────────────────────────────────────────────────

    [Fact]
    public void Fit_WhenEverythingFits_ShouldKeepEveryAttachmentInInputOrder()
    {
        IReadOnlyList<PacketEmailAttachment> candidates =
            [Pdf(200_000), Photo(1, 300_000), Photo(2, 300_000), Photo(3, 300_000)];

        var result = PacketEmailSizeFitter.Fit(candidates, Html, PlainText, RoomyBudget);

        result.Dropped.Should().BeEmpty();
        result.AnythingDropped.Should().BeFalse();
        result.Attachments.Select(a => a.FileName).Should().Equal(
            "service-packet-A1B2C3D4.pdf", "photo-1.jpg", "photo-2.jpg", "photo-3.jpg");
    }

    [Fact]
    public void Fit_WhenEverythingFits_ShouldReportAnEstimateInsideTheBudget()
    {
        var result = PacketEmailSizeFitter.Fit([Pdf(200_000), Photo(1, 300_000)], Html, PlainText, RoomyBudget);

        result.EstimatedRequestBytes.Should().BeLessThanOrEqualTo(RoomyBudget);
        // 500 KB of raw attachment inflates by ~4/3 under base64, so the estimate has to exceed it.
        result.EstimatedRequestBytes.Should().BeGreaterThan(500_000);
    }

    // ── Dropping photos ────────────────────────────────────────────────────

    [Fact]
    public void Fit_WhenPhotosOverflowTheBudget_ShouldDropFromTheEndAndKeepThePdf()
    {
        // 1 MB each: base64 makes every photo cost ~1.34 MB, so a 5 MB budget holds the
        // 200 KB PDF and three photos, not six.
        IReadOnlyList<PacketEmailAttachment> candidates =
            [Pdf(200_000), .. Enumerable.Range(1, 6).Select(i => Photo(i, 1_000_000))];

        var result = PacketEmailSizeFitter.Fit(candidates, Html, PlainText, 5_000_000);

        result.AnythingDropped.Should().BeTrue();
        result.Attachments.Should().Contain(a => a.ContentType == "application/pdf");
        result.EstimatedRequestBytes.Should().BeLessThanOrEqualTo(5_000_000);

        // Survivors are a contiguous prefix of the photos; the tail is what got dropped.
        var keptPhotos = result.Attachments.Where(a => a.ContentType == "image/jpeg").Select(a => a.FileName).ToList();
        var droppedPhotos = result.Dropped.Select(a => a.FileName).ToList();
        keptPhotos.Should().Equal(Enumerable.Range(1, keptPhotos.Count).Select(i => $"photo-{i}.jpg"));
        droppedPhotos.Should().Equal(
            Enumerable.Range(keptPhotos.Count + 1, 6 - keptPhotos.Count).Select(i => $"photo-{i}.jpg"));
    }

    [Fact]
    public void Fit_ShouldKeepThePdfEvenWhenItArrivesAfterThePhotos()
    {
        // AttachPdf ordering is the orchestrator's business; the fitter must not let input
        // order cost the packet its PDF.
        IReadOnlyList<PacketEmailAttachment> candidates =
            [.. Enumerable.Range(1, 6).Select(i => Photo(i, 1_000_000)), Pdf(200_000)];

        var result = PacketEmailSizeFitter.Fit(candidates, Html, PlainText, 5_000_000);

        result.Attachments.Should().Contain(a => a.ContentType == "application/pdf");
        result.Dropped.Should().OnlyContain(a => a.ContentType == "image/jpeg");
    }

    [Fact]
    public void Fit_WhenOnlyPhotosAreSuppliedAndTheyOverflow_ShouldKeepThePrefix()
    {
        IReadOnlyList<PacketEmailAttachment> candidates =
            [.. Enumerable.Range(1, 6).Select(i => Photo(i, 1_000_000))];

        var result = PacketEmailSizeFitter.Fit(candidates, Html, PlainText, 3_000_000);

        result.Attachments.Should().HaveCountGreaterThan(0);
        result.Attachments.Select(a => a.FileName).Should().Equal(
            Enumerable.Range(1, result.Attachments.Count).Select(i => $"photo-{i}.jpg"));
        result.EstimatedRequestBytes.Should().BeLessThanOrEqualTo(3_000_000);
    }

    [Fact]
    public void Fit_ShouldAccountForBase64Inflation()
    {
        // 3 MB of raw bytes fits a 3.5 MB budget; base64-encoded (~4 MB) it does not.
        var result = PacketEmailSizeFitter.Fit([Photo(1, 3_000_000)], Html, PlainText, 3_500_000);

        result.Attachments.Should().BeEmpty();
        result.Dropped.Should().HaveCount(1);
    }

    // ── The PDF that cannot fit at all ─────────────────────────────────────

    [Fact]
    public void Fit_WhenThePdfCannotFit_ShouldStillAttachThePhotosThatFit()
    {
        // QuestPDF keeps a real packet PDF at roughly 1.5–3 MB, so this is a regression shape, not
        // traffic: something made the PDF large (a renderer change, an embedded logo). The PDF
        // is still decided first; when it cannot fit, the photos that do fit are worth more to
        // the reader than a bare email.
        IReadOnlyList<PacketEmailAttachment> candidates =
            [Pdf(9_000_000), Photo(1, 100_000), Photo(2, 100_000)];

        var result = PacketEmailSizeFitter.Fit(candidates, Html, PlainText, 5_000_000);

        result.PdfDropped.Should().BeTrue();
        result.Attachments.Select(a => a.FileName).Should().Equal("photo-1.jpg", "photo-2.jpg");
        result.Dropped.Should().ContainSingle().Which.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public void Fit_WhenThePdfCannotFit_ShouldStillKeepPhotosAsAContiguousPrefix()
    {
        // 2 MB photos cost ~2.67 MB each under base64: a 5 MB budget holds one, not three.
        IReadOnlyList<PacketEmailAttachment> candidates =
            [Pdf(9_000_000), Photo(1, 2_000_000), Photo(2, 2_000_000), Photo(3, 2_000_000)];

        var result = PacketEmailSizeFitter.Fit(candidates, Html, PlainText, 5_000_000);

        result.PdfDropped.Should().BeTrue();
        result.Attachments.Select(a => a.FileName).Should().Equal("photo-1.jpg");
        result.Dropped.Select(a => a.FileName).Should().Equal(
            "service-packet-A1B2C3D4.pdf", "photo-2.jpg", "photo-3.jpg");
    }

    [Fact]
    public void Fit_WhenThePdfFits_ShouldNotReportThePdfAsDropped()
    {
        var result = PacketEmailSizeFitter.Fit(
            [Pdf(200_000), Photo(1, 4_000_000)], Html, PlainText, 1_000_000);

        result.PdfDropped.Should().BeFalse();
        result.Attachments.Should().ContainSingle(a => a.ContentType == "application/pdf");
        result.Dropped.Should().ContainSingle(a => a.ContentType == "image/jpeg");
    }

    // ── The bodies count too ───────────────────────────────────────────────

    [Fact]
    public void Fit_ShouldCountTheHtmlBodyAgainstTheBudget()
    {
        // The HTML packet is the email body (Spec B-3), and a photo-heavy packet's inline
        // markup is not free. A body big enough to fill the budget leaves no room to attach.
        var fatHtml = new string('x', 2_000_000);

        var withFatBody = PacketEmailSizeFitter.Fit([Photo(1, 500_000)], fatHtml, PlainText, 2_100_000);
        var withSlimBody = PacketEmailSizeFitter.Fit([Photo(1, 500_000)], Html, PlainText, 2_100_000);

        withFatBody.Attachments.Should().BeEmpty("the body consumed the budget");
        withSlimBody.Attachments.Should().HaveCount(1);
    }

    [Fact]
    public void Fit_ShouldCountThePlainTextBodyAgainstTheBudget()
    {
        var fatPasteBlock = new string('y', 2_000_000);

        var result = PacketEmailSizeFitter.Fit([Photo(1, 500_000)], Html, fatPasteBlock, 2_100_000);

        result.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void Fit_WhenTheBodiesAloneExceedTheBudget_ShouldDropEverythingAndNotThrow()
    {
        var fatHtml = new string('x', 3_000_000);

        var result = PacketEmailSizeFitter.Fit([Pdf(100), Photo(1, 100)], fatHtml, PlainText, 1_000_000);

        result.Attachments.Should().BeEmpty();
        result.Dropped.Should().HaveCount(2);
    }

    // ── Determinism ────────────────────────────────────────────────────────

    [Fact]
    public void Fit_ShouldBeDeterministicForTheSameInput()
    {
        IReadOnlyList<PacketEmailAttachment> candidates =
            [Pdf(200_000), .. Enumerable.Range(1, 8).Select(i => Photo(i, 900_000))];

        var first = PacketEmailSizeFitter.Fit(candidates, Html, PlainText, 4_000_000);
        var second = PacketEmailSizeFitter.Fit(candidates, Html, PlainText, 4_000_000);

        second.Attachments.Select(a => a.FileName).Should().Equal(first.Attachments.Select(a => a.FileName));
        second.Dropped.Select(a => a.FileName).Should().Equal(first.Dropped.Select(a => a.FileName));
        second.EstimatedRequestBytes.Should().Be(first.EstimatedRequestBytes);
    }

    [Fact]
    public void Fit_ShouldNotMutateTheSuppliedAttachmentList()
    {
        var candidates = new List<PacketEmailAttachment>
        {
            Pdf(200_000), Photo(1, 1_000_000), Photo(2, 1_000_000), Photo(3, 1_000_000),
        };

        PacketEmailSizeFitter.Fit(candidates, Html, PlainText, 1_500_000);

        candidates.Should().HaveCount(4);
    }

    // ── The default budget matches the documented ACS ceiling ──────────────

    [Fact]
    public void DefaultMaxRequestBytes_ShouldSitBelowTheAcsHardCap()
    {
        PacketEmailSizeFitter.DefaultMaxRequestBytes.Should().BeLessThan(PacketEmailSizeFitter.AcsMaxRequestBytes);
        PacketEmailSizeFitter.AcsMaxRequestBytes.Should().Be(10_000_000);
    }

    [Fact]
    public void Fit_AtTheDefaultBudget_ShouldPassTheSixIphonePhotoCaseThatUsedToFailDelivery()
    {
        // Issue #521: six ~3 MB iPhone photos attached as originals overran ACS and the request
        // delivered with no packet at all. It must now deliver, trimmed. The PDF is sized as
        // measured with six real 12 MP photos: QuestPDF resamples embedded images, so it is about
        // 1.6 MB regardless of source resolution.
        var pdf = Pdf(1_600_000);
        IReadOnlyList<PacketEmailAttachment> candidates =
            [pdf, .. Enumerable.Range(1, 6).Select(i => Photo(i, 3_000_000))];

        var result = PacketEmailSizeFitter.Fit(candidates, Html, PlainText);

        result.EstimatedRequestBytes.Should().BeLessThanOrEqualTo(PacketEmailSizeFitter.DefaultMaxRequestBytes);
        result.Attachments.Should().Contain(a => a.ContentType == "application/pdf");
        result.AnythingDropped.Should().BeTrue();
    }
}
