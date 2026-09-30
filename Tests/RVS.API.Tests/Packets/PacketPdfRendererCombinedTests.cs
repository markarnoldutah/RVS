using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using RVS.API.Packets;
using RVS.Domain.Packets;

namespace RVS.API.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketPdfRenderer.RenderCombined"/> — the one stored and emailed PDF of a
/// multi-issue submission: the masthead once, then every issue in order, flowing across pages
/// like a dealer work order (<c>Spec A-17</c>, <c>B-2</c>, issue #806).
/// </summary>
public class PacketPdfRendererCombinedTests
{
    private static ServicePacket Packet(string reference, string description) => new()
    {
        Unit = new PacketUnitHeader { Year = 2021, Make = "Jayco", Model = "Eagle" },
        Customer = new PacketCustomer { FullName = "Jane Doe" },
        Origin = new PacketOrigin
        {
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 28, 15, 0, 0, TimeSpan.Zero),
            ReferenceCode = reference,
        },
        IssueCategory = "Slides",
        IssueDescription = description,
        Diagnostics = [],
        Photos = [],
    };

    private static int PageCount(byte[] pdf) =>
        Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page(?![a-zA-Z])").Count;

    [Fact]
    public void RenderCombined_WhenPacketsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketPdfRenderer.RenderCombined(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RenderCombined_WhenPacketsIsEmpty_ShouldThrowArgumentException()
    {
        var act = () => PacketPdfRenderer.RenderCombined([]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RenderCombined_WithOnePacket_ShouldMatchTheSinglePacketPdf()
    {
        var packet = Packet("AAAA1111", "Slide will not retract");

        PacketPdfRenderer.RenderCombined([packet]).Should().Equal(PacketPdfRenderer.Render(packet));
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldBeOnePdfThatFlowsRatherThanASheetPerIssue()
    {
        IReadOnlyList<ServicePacket> packets =
        [
            Packet("AAAA1111", "Slide will not retract"),
            Packet("BBBB2222", "Fridge is warm"),
            Packet("CCCC3333", "Awning fabric torn"),
        ];

        var combined = PacketPdfRenderer.RenderCombined(packets);

        // One masthead and one footer for the lot, like a work order: three short issues share
        // pages instead of taking a sheet each.
        Encoding.ASCII.GetString(combined, 0, 5).Should().Be("%PDF-");
        PageCount(combined).Should().BeLessThan(packets.Sum(p => PageCount(PacketPdfRenderer.Render(p))));
    }

    [Fact]
    public void RenderCombined_WithManyLongIssues_ShouldRunOntoSeveralPages()
    {
        var longText = string.Join(' ', Enumerable.Repeat("The slide motor hums but the room does not move.", 40));
        IReadOnlyList<ServicePacket> packets = [.. Enumerable.Range(1, 6).Select(i => Packet($"REF0000{i}", longText))];

        var act = () => PacketPdfRenderer.RenderCombined(packets);

        PageCount(act.Should().NotThrow().Subject).Should().BeGreaterThan(1);
    }

    [Fact]
    public void RenderCombined_ShouldBeDeterministic()
    {
        IReadOnlyList<ServicePacket> packets = [Packet("AAAA1111", "a"), Packet("BBBB2222", "b")];

        PacketPdfRenderer.RenderCombined(packets).Should().Equal(PacketPdfRenderer.RenderCombined(packets));
    }
}
