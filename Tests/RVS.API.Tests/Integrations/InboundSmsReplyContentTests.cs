using FluentAssertions;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Tests for <see cref="InboundSmsReplyContent"/> — the fixed HELP reply (issue #665). The exact
/// string is submitted as a sample message on the toll-free verification application (#659), so
/// it is pinned here rather than left to drift.
/// </summary>
public class InboundSmsReplyContentTests
{
    [Fact]
    public void Help_ShouldBeTheTextSubmittedOnTheVerificationApplication()
    {
        InboundSmsReplyContent.Help.Should().Be(
            "RV Intake: We send service-request links and confirmations for your RV dealership. " +
            "For help with your request, contact the dealership directly. " +
            "Msg & data rates may apply. Reply STOP to opt out.");
    }

    [Fact]
    public void Help_ShouldNameRvsSayWhoItSendsForCarryRatesAndRepeatStop()
    {
        InboundSmsReplyContent.Help.Should().StartWith("RV Intake:");
        InboundSmsReplyContent.Help.Should().Contain("your RV dealership");
        InboundSmsReplyContent.Help.Should().Contain("Msg & data rates may apply.");
        InboundSmsReplyContent.Help.Should().EndWith("Reply STOP to opt out.");
    }

    [Fact]
    public void Help_ShouldFitTwoGsm7Segments()
    {
        InboundSmsReplyContent.Help.Length.Should().BeLessThanOrEqualTo(306);
    }
}
