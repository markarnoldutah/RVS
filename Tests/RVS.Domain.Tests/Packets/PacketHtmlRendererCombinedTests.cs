using System.Text.RegularExpressions;
using FluentAssertions;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketHtmlRenderer.RenderCombined"/> — the one packet of a multi-issue
/// submission, laid out like a dealer work order: the identifying masthead once at the top, then
/// every issue below it with its own action buttons (<c>Spec A-17</c>, <c>B-2</c>, issue #806).
/// </summary>
public class PacketHtmlRendererCombinedTests
{
    private static ServicePacket Packet(
        string reference, string category, string description, bool withPhoto = false, string? curatedIssue = null,
        PacketAiSummary? aiSummary = null) => new()
    {
        Unit = new PacketUnitHeader { Year = 2021, Make = "Jayco", Model = "Eagle" },
        Customer = new PacketCustomer { FullName = "Jane Doe", LastName = "Doe" },
        Origin = new PacketOrigin
        {
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 28, 15, 0, 0, TimeSpan.Zero),
            ReferenceCode = reference,
        },
        IssueCategory = category,
        CuratedIssue = curatedIssue,
        AiSummary = aiSummary,
        IssueDescription = description,
        Diagnostics = [],
        Photos = withPhoto ? [new PacketPhoto { Url = "https://blob/p.jpg", FileName = "p.jpg", ContentType = "image/jpeg" }] : [],
        PasteBlock = $"PASTE {reference}",
        StatusLink = new PacketStatusLink { Url = "https://rvintake.com/status/tok" },
        ManagerLinks = new PacketManagerLinks
        {
            RequestUrl = $"https://manager.rvintake.com/sr/{reference}",
            Actions =
            [
                new PacketManagerActionLink
                {
                    Label = "In progress",
                    Status = "InProgress",
                    Url = $"https://manager.rvintake.com/sr/{reference}?status=InProgress",
                },
            ],
        },
    };

    private static IReadOnlyList<ServicePacket> ThreePackets() =>
    [
        Packet("AAAA1111", "Slides", "Slide will not retract"),
        Packet("BBBB2222", "Appliances", "Fridge is warm"),
        Packet("CCCC3333", "Awning", "Awning fabric torn"),
    ];

    private static int Count(string html, string pattern) => Regex.Matches(html, pattern).Count;

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
    public void RenderCombined_WithSeveralPackets_ShouldBeOneDocumentWithOneMastheadAndOneFooter()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        Count(html, "<!DOCTYPE html>").Should().Be(1);
        Count(html, "<main").Should().Be(1);
        Count(html, "<!-- section:unit -->").Should().Be(1);
        Count(html, "<!-- section:customer -->").Should().Be(1);
        Count(html, "class=\"packet-foot\"").Should().Be(1);
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldNameTheFirstIssuesReferenceInTheMasthead()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        html.Should().Contain("Intake #: <strong>AAAA1111</strong>");
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldListTheIssuesUnderTheMasthead()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        var index = html.IndexOf("<!-- section:issue-index -->", StringComparison.Ordinal);
        index.Should().BeGreaterThan(html.IndexOf("<!-- section:origin -->", StringComparison.Ordinal));
        index.Should().BeLessThan(html.IndexOf("Slide will not retract", StringComparison.Ordinal));
        var block = html[index..html.IndexOf("</section>", index, StringComparison.Ordinal)];
        block.Should().Contain("3 issues").And.Contain("Slides").And.Contain("Appliances").And.Contain("Awning");
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldHeadEachIssueWithItsPositionCategoryAndReference()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        html.Should().Contain("Issue 1 of 3").And.Contain("Issue 2 of 3").And.Contain("Issue 3 of 3");
        var second = html.IndexOf("Issue 2 of 3", StringComparison.Ordinal);
        html.IndexOf("BBBB2222", second, StringComparison.Ordinal).Should().BeGreaterThan(second);
        html.IndexOf("Fridge is warm", StringComparison.Ordinal).Should().BeGreaterThan(second);
        html.IndexOf("Fridge is warm", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Issue 3 of 3", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldGiveEachIssueItsOwnActionButtons()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        Count(html, "<!-- section:manager-actions -->").Should().Be(3);
        foreach (var reference in new[] { "AAAA1111", "BBBB2222", "CCCC3333" })
        {
            html.Should().Contain($"https://manager.rvintake.com/sr/{reference}?status=InProgress");
        }

        // Each issue's buttons sit under its own heading, not in a block above the masthead.
        html.IndexOf("<!-- section:manager-actions -->", StringComparison.Ordinal)
            .Should().BeGreaterThan(html.IndexOf("Issue 1 of 3", StringComparison.Ordinal));
        var secondButton = html.IndexOf("https://manager.rvintake.com/sr/BBBB2222?status=InProgress", StringComparison.Ordinal);
        secondButton.Should().BeGreaterThan(html.IndexOf("Issue 2 of 3", StringComparison.Ordinal));
        secondButton.Should().BeLessThan(html.IndexOf("Issue 3 of 3", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldGiveEachIssueItsOwnPasteBlock()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        Count(html, "<!-- section:paste-block -->").Should().Be(3);
        html.Should().Contain("PASTE BBBB2222");
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldShowTheCustomersStatusLinkOnceAfterTheLastIssue()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        // One status page per customer (Spec X-1): it lists every issue, so it is linked once.
        Count(html, "<!-- section:status-link -->").Should().Be(1);
        html.IndexOf("<!-- section:status-link -->", StringComparison.Ordinal)
            .Should().BeGreaterThan(html.IndexOf("Awning fabric torn", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderCombined_WithSeveralPackets_ShouldLetTheIssuesFlowRatherThanForceASheetEach()
    {
        var html = PacketHtmlRenderer.RenderCombined(ThreePackets());

        html.Should().NotContain("page-break-before:always");
    }

    [Fact]
    public void RenderCombined_ShouldCarryOneAiDisclosureCoveringEveryIssue()
    {
        IReadOnlyList<ServicePacket> packets =
        [
            Packet("AAAA1111", "Slides", "a", curatedIssue: "Slide stalls."),
            Packet("BBBB2222", "Appliances", "b", aiSummary: new PacketAiSummary { Text = "Likely a failed thermistor." }),
        ];

        var html = PacketHtmlRenderer.RenderCombined(packets);

        Count(html, "AI disclosure:").Should().Be(1);
        html.Should().Contain("the Issue and Preliminary assessment sections are AI-generated");
    }

    [Fact]
    public void RenderCombined_WhenPhotosWereDropped_ShouldPointEachIssueWithPhotosAtItsOwnRequest()
    {
        IReadOnlyList<ServicePacket> packets =
        [
            Packet("AAAA1111", "Slides", "Slide will not retract", withPhoto: true),
            Packet("BBBB2222", "Appliances", "Fridge is warm"),
        ];

        var html = PacketHtmlRenderer.RenderCombined(packets, photosDropped: true);

        Count(html, "class=\"photo-note\"").Should().Be(1);
        html.Should().MatchRegex("class=\"photo-note\">[^<]*<a href=\"https://manager.rvintake.com/sr/AAAA1111\"");
        PacketHtmlRenderer.RenderCombined(packets).Should().NotContain("class=\"photo-note\"");
    }
}
