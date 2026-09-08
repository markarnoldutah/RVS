using FluentAssertions;
using RVS.Domain.Integrations;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketEmailComposer"/> — the pure transform that turns a composed
/// <see cref="ServicePacket"/> plus its rendered HTML into a transport-agnostic
/// <see cref="PacketEmailMessage"/> (<c>Spec B-4</c>, issue #437).
///
/// Contract under test: the subject matches
/// <c>[RVS] {category} — {year} {make} {model} — {customer last name}</c> and degrades
/// field-by-field; the plain-text body is the paste block, generated on the fly when the
/// packet has none; the HTML body, recipients, and attachments pass through untouched.
/// </summary>
public class PacketEmailComposerTests
{
    private const string Html = "<!DOCTYPE html><html><body><h1>Packet</h1></body></html>";
    private static readonly string[] OneRecipient = ["service@dealer.example"];

    private static ServicePacket BuildPacket(
        string? category = "Slide System",
        int? year = 2021,
        string? make = "Jayco",
        string? model = "Eagle",
        string? pasteBlock = "----- RV SERVICE FLOW -----\nCATEGORY: SLIDE SYSTEM\nCOMPLAINT: Slide will not retract\n----- RV SERVICE FLOW -----",
        string? statusLinkUrl = null) => new()
    {
        Unit = new PacketUnitHeader { Year = year, Make = make, Model = model, Vin = "1HGBH41JXMN109186" },
        Customer = new PacketCustomer { FullName = "Jane Doe" },
        Origin = new PacketOrigin
        {
            LocationName = "Salt Lake Service",
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero),
            ReferenceCode = "A1B2C3D4",
        },
        IssueCategory = category,
        IssueDescription = "Slide will not retract",
        Diagnostics = [],
        Photos = [],
        PasteBlock = pasteBlock,
        StatusLink = statusLinkUrl is null ? null : new PacketStatusLink { Url = statusLinkUrl },
    };

    // ── Guard clauses ──────────────────────────────────────────────────────

    [Fact]
    public void Compose_WhenPacketIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketEmailComposer.Compose(null!, Html, "Doe", OneRecipient);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compose_WhenHtmlBodyIsNullOrWhiteSpace_ShouldThrowArgumentException(string? html)
    {
        var act = () => PacketEmailComposer.Compose(BuildPacket(), html!, "Doe", OneRecipient);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Compose_WhenRecipientsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketEmailComposer.Compose(BuildPacket(), Html, "Doe", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Compose_WhenRecipientsIsEmpty_ShouldThrowArgumentException()
    {
        var act = () => PacketEmailComposer.Compose(BuildPacket(), Html, "Doe", []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Compose_WhenEveryRecipientIsBlank_ShouldThrowArgumentException()
    {
        var act = () => PacketEmailComposer.Compose(BuildPacket(), Html, "Doe", ["", "   "]);

        act.Should().Throw<ArgumentException>();
    }

    // ── Subject ────────────────────────────────────────────────────────────

    [Fact]
    public void Compose_WithEveryFieldPresent_ShouldBuildTheSpecSubject()
    {
        var message = PacketEmailComposer.Compose(BuildPacket(), Html, "Doe", OneRecipient);

        message.Subject.Should().Be("[RVS] Slide System — 2021 Jayco Eagle — Doe");
    }

    [Fact]
    public void Compose_WhenCategoryIsNull_ShouldUseUncategorizedInTheSubject()
    {
        var message = PacketEmailComposer.Compose(BuildPacket(category: null), Html, "Doe", OneRecipient);

        message.Subject.Should().Be("[RVS] Uncategorized — 2021 Jayco Eagle — Doe");
    }

    [Fact]
    public void Compose_WhenYearMakeAndModelAreAllMissing_ShouldSayUnknownVehicle()
    {
        var packet = BuildPacket(year: null, make: null, model: null);

        var message = PacketEmailComposer.Compose(packet, Html, "Doe", OneRecipient);

        message.Subject.Should().Be("[RVS] Slide System — Unknown vehicle — Doe");
    }

    [Fact]
    public void Compose_WhenOnlySomeVehicleFieldsArePresent_ShouldJoinWhatItHasWithoutDoubleSpaces()
    {
        var packet = BuildPacket(year: 2021, make: null, model: "Eagle");

        var message = PacketEmailComposer.Compose(packet, Html, "Doe", OneRecipient);

        message.Subject.Should().Be("[RVS] Slide System — 2021 Eagle — Doe");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compose_WhenCustomerLastNameIsBlank_ShouldSayUnknown(string? lastName)
    {
        var message = PacketEmailComposer.Compose(BuildPacket(), Html, lastName!, OneRecipient);

        message.Subject.Should().Be("[RVS] Slide System — 2021 Jayco Eagle — Unknown");
    }

    [Fact]
    public void Compose_ShouldTrimTheCustomerLastName()
    {
        var message = PacketEmailComposer.Compose(BuildPacket(), Html, "  Doe  ", OneRecipient);

        message.Subject.Should().Be("[RVS] Slide System — 2021 Jayco Eagle — Doe");
    }

    // ── Bodies ─────────────────────────────────────────────────────────────

    [Fact]
    public void Compose_ShouldPassTheHtmlBodyThroughUnchanged()
    {
        var message = PacketEmailComposer.Compose(BuildPacket(), Html, "Doe", OneRecipient);

        message.HtmlBody.Should().Be(Html);
    }

    [Fact]
    public void Compose_WhenThePacketHasAPasteBlock_ShouldUseItAsThePlainTextBody()
    {
        var packet = BuildPacket(pasteBlock: "----- RV SERVICE FLOW -----\nCATEGORY: SLIDE SYSTEM\nCOMPLAINT: Slide will not retract\n----- RV SERVICE FLOW -----");

        var message = PacketEmailComposer.Compose(packet, Html, "Doe", OneRecipient);

        message.PlainTextBody.Should().Be(packet.PasteBlock);
    }

    [Fact]
    public void Compose_WhenThePacketHasNoPasteBlock_ShouldGenerateOneForThePlainTextBody()
    {
        var packet = BuildPacket(pasteBlock: null, statusLinkUrl: "https://rvs.example/s/tok");

        var message = PacketEmailComposer.Compose(packet, Html, "Doe", OneRecipient);

        message.PlainTextBody.Should().Contain("CATEGORY: SLIDE SYSTEM");
        message.PlainTextBody.Should().Contain("Slide will not retract");
        message.PlainTextBody.Should().Contain("https://rvs.example/s/tok");
        message.PlainTextBody.Should().Contain(PasteBlockGenerator.Delimiter);
    }

    [Fact]
    public void Compose_WhenThePacketHasAWhitespacePasteBlock_ShouldGenerateOneInstead()
    {
        var packet = BuildPacket(pasteBlock: "   ");

        var message = PacketEmailComposer.Compose(packet, Html, "Doe", OneRecipient);

        message.PlainTextBody.Should().Contain(PasteBlockGenerator.Delimiter);
        message.PlainTextBody.Trim().Should().NotBeEmpty();
    }

    // ── Recipients & attachments ───────────────────────────────────────────

    [Fact]
    public void Compose_ShouldCarryTheRecipientListThrough()
    {
        string[] recipients = ["a@dealer.example", "b@dealer.example"];

        var message = PacketEmailComposer.Compose(BuildPacket(), Html, "Doe", recipients);

        message.Recipients.Should().Equal("a@dealer.example", "b@dealer.example");
    }

    [Fact]
    public void Compose_ShouldDropBlankRecipients()
    {
        string[] recipients = ["a@dealer.example", "  ", ""];

        var message = PacketEmailComposer.Compose(BuildPacket(), Html, "Doe", recipients);

        message.Recipients.Should().Equal("a@dealer.example");
    }

    [Fact]
    public void Compose_WhenGivenNoAttachments_ShouldProduceAnEmptyAttachmentList()
    {
        var message = PacketEmailComposer.Compose(BuildPacket(), Html, "Doe", OneRecipient);

        message.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void Compose_ShouldCarryAttachmentsThrough()
    {
        var pdf = new PacketEmailAttachment
        {
            FileName = "service-packet-A1B2C3D4.pdf",
            ContentType = "application/pdf",
            Content = new byte[] { 1, 2, 3 },
        };
        var photo = new PacketEmailAttachment
        {
            FileName = "slide.jpg",
            ContentType = "image/jpeg",
            Content = new byte[] { 4, 5, 6 },
        };

        var message = PacketEmailComposer.Compose(BuildPacket(), Html, "Doe", OneRecipient, [pdf, photo]);

        message.Attachments.Should().Equal(pdf, photo);
    }
}
