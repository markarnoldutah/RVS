using FluentAssertions;
using RVS.Domain.Integrations;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketEmailComposer.ComposeCombined"/> — the one packet email a
/// multi-problem submission sends (<c>Spec A-17</c>, <c>B-4</c>, issue #806). The subject trades
/// the single category for the problem count and every category; the plain-text body carries
/// each problem's paste block in order.
/// </summary>
public class PacketEmailComposerCombinedTests
{
    private const string Html = "<!DOCTYPE html><html><body>packets</body></html>";
    private static readonly string[] OneRecipient = ["service@dealer.example"];

    private static ServicePacket Packet(string? category, string description, string reference = "A1B2C3D4") => new()
    {
        Unit = new PacketUnitHeader { Year = 2021, Make = "Jayco", Model = "Eagle" },
        Customer = new PacketCustomer { FullName = "Jane Doe" },
        Origin = new PacketOrigin
        {
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero),
            ReferenceCode = reference,
        },
        IssueCategory = category,
        IssueDescription = description,
        Diagnostics = [],
        Photos = [],
        PasteBlock = $"----- RV INTAKE -----\nCOMPLAINT: {description}\n----- RV INTAKE -----",
    };

    [Fact]
    public void ComposeCombined_WhenPacketsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketEmailComposer.ComposeCombined(null!, Html, "Doe", OneRecipient);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComposeCombined_WhenPacketsIsEmpty_ShouldThrowArgumentException()
    {
        var act = () => PacketEmailComposer.ComposeCombined([], Html, "Doe", OneRecipient);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ComposeCombined_WithOnePacket_ShouldMatchTheSinglePacketEmail()
    {
        var packet = Packet("Slides", "Slide will not retract");

        var combined = PacketEmailComposer.ComposeCombined([packet], Html, "Doe", OneRecipient);
        var single = PacketEmailComposer.Compose(packet, Html, "Doe", OneRecipient);

        combined.Subject.Should().Be(single.Subject);
        combined.PlainTextBody.Should().Be(single.PlainTextBody);
    }

    [Fact]
    public void ComposeCombined_WithSeveralPackets_ShouldCountAndListTheCategoriesInTheSubject()
    {
        IReadOnlyList<ServicePacket> packets =
        [
            Packet("Slides", "Slide will not retract"),
            Packet(null, "Something rattles"),
            Packet("Awning", "Awning fabric torn"),
        ];

        var message = PacketEmailComposer.ComposeCombined(packets, Html, "Doe", OneRecipient);

        message.Subject.Should().Be("New SR: Doe: 2021 Jayco Eagle - 3 issues: Slides, Uncategorized, Awning");
    }

    [Fact]
    public void ComposeCombined_WithSeveralPackets_ShouldCarryEveryPasteBlockInOrder()
    {
        IReadOnlyList<ServicePacket> packets =
        [
            Packet("Slides", "Slide will not retract"),
            Packet("Awning", "Awning fabric torn"),
        ];

        var body = PacketEmailComposer.BuildCombinedPlainTextBody(packets);

        body.Should().Contain("PROBLEM 1 OF 2").And.Contain("PROBLEM 2 OF 2");
        body.IndexOf("Slide will not retract", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("Awning fabric torn", StringComparison.Ordinal));
        body.Should().MatchRegex(@"^[\x00-\x7F]*$", "a DMS field mangles anything but ASCII (Spec B-5)");
    }

    [Fact]
    public void ComposeCombined_ShouldPassHtmlRecipientsAndAttachmentsThrough()
    {
        var attachment = new PacketEmailAttachment { FileName = "packets.pdf", ContentType = "application/pdf", Content = new byte[] { 1 } };

        var message = PacketEmailComposer.ComposeCombined(
            [Packet("Slides", "a"), Packet("Awning", "b")], Html, "Doe", [" service@dealer.example ", " "], [attachment]);

        message.HtmlBody.Should().Be(Html);
        message.Recipients.Should().Equal("service@dealer.example");
        message.Attachments.Should().ContainSingle().Which.Should().BeSameAs(attachment);
        message.PlainTextBody.Should().Be(PacketEmailComposer.BuildCombinedPlainTextBody([Packet("Slides", "a"), Packet("Awning", "b")]));
    }
}
