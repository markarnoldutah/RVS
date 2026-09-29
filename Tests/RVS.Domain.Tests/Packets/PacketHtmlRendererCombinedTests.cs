using System.Text.RegularExpressions;
using FluentAssertions;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketHtmlRenderer.RenderCombined"/> — one HTML document carrying every
/// packet of a multi-problem submission, the body of its single packet email (<c>Spec A-17</c>,
/// <c>B-4</c>, issue #806).
/// </summary>
public class PacketHtmlRendererCombinedTests
{
    private static ServicePacket Packet(string reference, string category, string description, bool withPhoto = false) => new()
    {
        Unit = new PacketUnitHeader { Year = 2021, Make = "Jayco", Model = "Eagle" },
        Customer = new PacketCustomer { FullName = "Jane Doe", LastName = "Doe" },
        Origin = new PacketOrigin
        {
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 28, 15, 0, 0, TimeSpan.Zero),
            ReferenceCode = reference,
        },
        IssueCategory = category,
        IssueDescription = description,
        Diagnostics = [],
        Photos = withPhoto ? [new PacketPhoto { Url = "https://blob/p.jpg", FileName = "p.jpg", ContentType = "image/jpeg" }] : [],
        ManagerLinks = new PacketManagerLinks { RequestUrl = $"https://manager.rvintake.com/sr/{reference}", Actions = [] },
    };

    private static IReadOnlyList<ServicePacket> ThreePackets() =>
    [
        Packet("AAAA1111", "Slides", "Slide will not retract"),
        Packet("BBBB2222", "Appliances", "Fridge is warm"),
        Packet("CCCC3333", "Awning", "Awning fabric torn"),
    ];

    [Fact]
    public void RenderCombined_WhenPacketsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketHtmlRenderer.RenderCombined(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RenderCombined_WhenPacketsIsEmpty_ShouldThrowArgumentException()
    {
        var act = () => PacketHtmlRenderer.RenderCombined([]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RenderCombined_WithOnePacket_ShouldMatchTheSinglePacketRendering()
    {
        var packet = Packet("AAAA1111", "Slides", "Slide will not retract");

        PacketHtmlRenderer.RenderCombined([packet]).Should().Be(PacketHtmlRenderer.Render(packet));
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldBeOneDocument()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        Regex.Matches(html, "<!DOCTYPE html>").Should().HaveCount(1);
        Regex.Matches(html, "<html").Should().HaveCount(1);
        Regex.Matches(html, "</body>").Should().HaveCount(1);
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldCarryEveryPacketInOrder()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        var first = html.IndexOf("Slide will not retract", StringComparison.Ordinal);
        var second = html.IndexOf("Fridge is warm", StringComparison.Ordinal);
        var third = html.IndexOf("Awning fabric torn", StringComparison.Ordinal);
        first.Should().BeGreaterThan(0);
        second.Should().BeGreaterThan(first);
        third.Should().BeGreaterThan(second);
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldLabelEachProblemByPosition()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        html.Should().Contain("Problem 1 of 3").And.Contain("Problem 2 of 3").And.Contain("Problem 3 of 3");
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldLeadWithAnIndexOfTheProblems()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        var index = html.IndexOf("<!-- section:submission-index -->", StringComparison.Ordinal);
        index.Should().BeGreaterThan(0);
        index.Should().BeLessThan(html.IndexOf("Slide will not retract", StringComparison.Ordinal));
        var indexBlock = html[index..html.IndexOf("</table>", index, StringComparison.Ordinal)];
        indexBlock.Should().Contain("3 problems reported").And.Contain("AAAA1111").And.Contain("BBBB2222")
            .And.Contain("CCCC3333").And.Contain("Appliances");
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldStartEveryLaterPacketOnANewSheet()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        // One packet per sheet when printed: the second and third break before, the first does not.
        Regex.Matches(html, "<main class=\"packet\"[^>]*break-before:page").Should().HaveCount(2);
    }

    [Fact]
    public void RenderCombined_WhenPhotosWereDropped_ShouldPointEachPacketWithPhotosAtItsOwnRequest()
    {
        IReadOnlyList<ServicePacket> packets =
        [
            Packet("AAAA1111", "Slides", "Slide will not retract", withPhoto: true),
            Packet("BBBB2222", "Appliances", "Fridge is warm"),
        ];

        var html = PacketHtmlRenderer.RenderCombined(packets, photosDropped: true);

        // Only the packet that had images can have lost one; the note sends the reader to it.
        Regex.Matches(html, "class=\"photo-note\"").Should().HaveCount(1);
        html.Should().MatchRegex("class=\"photo-note\">[^<]*<a href=\"https://manager.rvintake.com/sr/AAAA1111\"");
        PacketHtmlRenderer.RenderCombined(packets).Should().NotContain("class=\"photo-note\"");
    }
}
