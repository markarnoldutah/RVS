using FluentAssertions;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for the derived members of <see cref="ServicePacket"/> that both renderers read, so the
/// HTML and PDF renderings cannot word them differently.
/// </summary>
public class ServicePacketTests
{
    private static ServicePacket Packet() => new()
    {
        Unit = new PacketUnitHeader(),
        Customer = new PacketCustomer { FullName = "Jane Doe" },
        Origin = new PacketOrigin
        {
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero),
            ReferenceCode = "DEADBEEF",
        },
        IssueDescription = "It rattles.",
        Diagnostics = [],
        Photos = [],
    };

    // ── AI disclaimer (issue #780) ──────────────────────────────────────────
    //
    // The per-section "AI-generated" badges gave way to one disclaimer in the footer. It names
    // the machine-written sections actually on the packet, so the packet still never passes
    // machine wording off as the customer's.

    [Fact]
    public void AiDisclaimer_WhenNoAiContent_ShouldBeNull()
    {
        Packet().AiDisclaimer.Should().BeNull();
    }

    [Fact]
    public void AiDisclaimer_WhenCuratedIssueAndAssessmentPresent_ShouldNameBothSections()
    {
        var packet = Packet() with
        {
            CuratedIssue = "Generator shuts down under load.",
            AiSummary = new PacketAiSummary { Text = "Likely overheating." },
        };

        packet.AiDisclaimer.Should().Be(
            "AI disclosure: the Issue and Preliminary assessment sections are AI-generated from the customer's "
            + "submission and may contain errors. The Reported issue and diagnostic answers are the customer's own words.");
    }

    [Fact]
    public void AiDisclaimer_WhenOnlyTheAssessmentPresent_ShouldNameOnlyIt()
    {
        var packet = Packet() with { AiSummary = new PacketAiSummary { Text = "Likely overheating." } };

        packet.AiDisclaimer.Should().StartWith("AI disclosure: the Preliminary assessment section is AI-generated");
    }

    [Fact]
    public void AiDisclaimer_WhenOnlyTheCuratedIssuePresent_ShouldNameOnlyIt()
    {
        var packet = Packet() with { CuratedIssue = "Generator shuts down under load." };

        packet.AiDisclaimer.Should().StartWith("AI disclosure: the Issue section is AI-generated");
    }
}
