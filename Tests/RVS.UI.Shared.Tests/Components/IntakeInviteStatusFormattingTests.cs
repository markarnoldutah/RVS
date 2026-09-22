using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.UI.Shared.Components;

namespace RVS.UI.Shared.Tests.Components;

/// <summary>
/// Tests for <see cref="IntakeInviteStatusFormatting"/> — what the Send intake link dialog
/// shows beside each invite (<c>Spec A-14</c>, issue #666).
/// </summary>
public class IntakeInviteStatusFormattingTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 17, 0, 0, DateTimeKind.Utc);

    private static IntakeInviteSummaryResponseDto Invite(
        string deliveryStatus,
        DateTime? redeemedAtUtc = null,
        DateTime? expiresAtUtc = null,
        bool isSelfEntry = false,
        string channel = IntakeInviteChannel.Sms) => new()
        {
            Id = "inv-1",
            LocationId = "loc-1",
            FirstName = "Jane",
            Phone = channel == IntakeInviteChannel.Sms ? "+18015551234" : null,
            Email = channel == IntakeInviteChannel.Email ? "jane@example.com" : null,
            Channel = channel,
            IsSelfEntry = isSelfEntry,
            CreatedAtUtc = Now.AddMinutes(-5),
            ExpiresAtUtc = expiresAtUtc ?? Now.AddHours(72),
            RedeemedAtUtc = redeemedAtUtc,
            DeliveryStatus = deliveryStatus
        };

    [Fact]
    public void Describe_WhenNull_ShouldThrowArgumentNullException()
    {
        var act = () => IntakeInviteStatusFormatting.Describe(null!, Now);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Describe_WhenPending_ShouldReadAsSending()
    {
        var display = IntakeInviteStatusFormatting.Describe(Invite("pending"), Now);

        display.Label.Should().Be("Sending…");
        display.Tone.Should().Be(IntakeInviteStatusTone.Progress);
    }

    [Fact]
    public void Describe_WhenQueued_ShouldReadAsSent()
    {
        // "queued" is ACS's word. The advisor's word is "sent" — the carrier hasn't confirmed yet.
        var display = IntakeInviteStatusFormatting.Describe(Invite("queued"), Now);

        display.Label.Should().Be("Sent");
        display.Tone.Should().Be(IntakeInviteStatusTone.Progress);
    }

    [Fact]
    public void Describe_WhenDelivered_ShouldReadAsDelivered()
    {
        var display = IntakeInviteStatusFormatting.Describe(Invite("delivered"), Now);

        display.Label.Should().Be("Delivered");
        display.Tone.Should().Be(IntakeInviteStatusTone.Success);
    }

    [Fact]
    public void Describe_WhenFailed_ShouldReadAsNotDelivered()
    {
        var display = IntakeInviteStatusFormatting.Describe(Invite("failed"), Now);

        display.Label.Should().Be("Not delivered");
        display.Tone.Should().Be(IntakeInviteStatusTone.Error);
    }

    [Fact]
    public void Describe_WhenSelfEntry_ShouldSayNothingWasSent()
    {
        var display = IntakeInviteStatusFormatting.Describe(Invite("notSent", isSelfEntry: true), Now);

        display.Label.Should().Be("Not sent");
        display.Tone.Should().Be(IntakeInviteStatusTone.Neutral);
    }

    [Fact]
    public void Describe_WhenRedeemed_ShouldOutrankTheDeliveryStatus()
    {
        // The form came back. Whether the carrier ever confirmed delivery no longer matters.
        var display = IntakeInviteStatusFormatting.Describe(Invite("queued", redeemedAtUtc: Now.AddMinutes(-1)), Now);

        display.Label.Should().Be("Form submitted");
        display.Tone.Should().Be(IntakeInviteStatusTone.Success);
    }

    [Fact]
    public void Describe_WhenExpiredAndUnredeemed_ShouldReadAsExpired()
    {
        var display = IntakeInviteStatusFormatting.Describe(
            Invite("delivered", expiresAtUtc: Now.AddMinutes(-1)), Now);

        display.Label.Should().Be("Expired");
        display.Tone.Should().Be(IntakeInviteStatusTone.Neutral);
    }

    [Fact]
    public void Describe_WhenExpiredButAlreadyRedeemed_ShouldStillReadAsSubmitted()
    {
        var display = IntakeInviteStatusFormatting.Describe(
            Invite("delivered", redeemedAtUtc: Now.AddHours(-2), expiresAtUtc: Now.AddMinutes(-1)), Now);

        display.Label.Should().Be("Form submitted");
    }

    [Fact]
    public void Describe_WhenTheStatusIsUnknown_ShouldFallBackToTheRawValue()
    {
        var display = IntakeInviteStatusFormatting.Describe(Invite("somethingNew"), Now);

        display.Label.Should().Be("somethingNew");
        display.Tone.Should().Be(IntakeInviteStatusTone.Neutral);
    }

    [Fact]
    public void Describe_ShouldStillSettleWhenTheStatusIsBlank()
    {
        var display = IntakeInviteStatusFormatting.Describe(Invite(""), Now);

        display.Label.Should().Be("Unknown");
        display.Tone.Should().Be(IntakeInviteStatusTone.Neutral);
    }

    // ── IsPolling ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("pending", true)]
    [InlineData("queued", true)]
    [InlineData("delivered", false)]
    [InlineData("failed", false)]
    [InlineData("notSent", false)]
    public void IsAwaitingDeliveryReport_ShouldOnlyBeTrueBeforeTheCarrierAnswers(string status, bool expected)
    {
        // The dialog polls while this is true, and stops as soon as it isn't.
        IntakeInviteStatusFormatting.IsAwaitingDeliveryReport(Invite(status)).Should().Be(expected);
    }

    [Fact]
    public void IsAwaitingDeliveryReport_WhenNull_ShouldThrowArgumentNullException()
    {
        var act = () => IntakeInviteStatusFormatting.IsAwaitingDeliveryReport(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Email channel (issue #693) ───────────────────────────────────────────

    [Fact]
    public void Describe_AnAcceptedEmail_ShouldReadAsEmailedWithNothingPending()
    {
        // No delivery report comes back for an email, so it must not look like it is waiting on one.
        var display = IntakeInviteStatusFormatting.Describe(
            Invite(IntakeInviteDeliveryStatus.Queued, channel: IntakeInviteChannel.Email), Now);

        display.Label.Should().Be("Emailed");
        display.Tone.Should().Be(IntakeInviteStatusTone.Neutral);
    }

    [Fact]
    public void Describe_AFailedEmail_ShouldReadAsNotDelivered()
    {
        IntakeInviteStatusFormatting.Describe(
                Invite(IntakeInviteDeliveryStatus.Failed, channel: IntakeInviteChannel.Email), Now)
            .Tone.Should().Be(IntakeInviteStatusTone.Error);
    }

    [Theory]
    [InlineData(IntakeInviteDeliveryStatus.Pending)]
    [InlineData(IntakeInviteDeliveryStatus.Queued)]
    public void IsAwaitingDeliveryReport_ForAnEmail_ShouldBeFalse(string status)
    {
        // Nothing reports email delivery back to the invite, so polling would only spin.
        IntakeInviteStatusFormatting.IsAwaitingDeliveryReport(Invite(status, channel: IntakeInviteChannel.Email))
            .Should().BeFalse();
    }

    [Fact]
    public void FormatContact_ForATextedInvite_ShouldReadThePhoneBack()
    {
        IntakeInviteStatusFormatting.FormatContact(Invite(IntakeInviteDeliveryStatus.Queued))
            .Should().Be("(801) 555-1234");
    }

    [Fact]
    public void FormatContact_ForAnEmailedInvite_ShouldShowTheAddress()
    {
        IntakeInviteStatusFormatting.FormatContact(Invite(IntakeInviteDeliveryStatus.Queued, channel: IntakeInviteChannel.Email))
            .Should().Be("jane@example.com");
    }

    [Fact]
    public void FormatContact_ForASelfEntryInviteWithOnlyAnEmail_ShouldShowTheAddress()
    {
        var invite = Invite(IntakeInviteDeliveryStatus.NotSent, isSelfEntry: true) with { Phone = null, Email = "jane@example.com" };

        IntakeInviteStatusFormatting.FormatContact(invite).Should().Be("jane@example.com");
    }

    // ── FormatPhone ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("+18015551234", "(801) 555-1234")]
    [InlineData("+14165550199", "(416) 555-0199")]
    public void FormatPhone_ForANorthAmericanE164Number_ShouldReadBackAsDialled(string e164, string expected)
    {
        IntakeInviteStatusFormatting.FormatPhone(e164).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatPhone_WhenThereIsNoNumber_ShouldReturnADash(string? phone)
    {
        IntakeInviteStatusFormatting.FormatPhone(phone).Should().Be("—");
    }

    [Fact]
    public void FormatPhone_ForAnythingElse_ShouldPassItThroughUnchanged()
    {
        IntakeInviteStatusFormatting.FormatPhone("+442071838750").Should().Be("+442071838750");
    }
}
