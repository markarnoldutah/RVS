using FluentAssertions;
using RVS.Domain.DTOs;
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
        bool isSelfEntry = false) => new()
        {
            Id = "inv-1",
            LocationId = "loc-1",
            FirstName = "Jane",
            Phone = "+18015551234",
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
    public void Describe_WhenSelfEntry_ShouldSayNothingWasTexted()
    {
        var display = IntakeInviteStatusFormatting.Describe(Invite("notSent", isSelfEntry: true), Now);

        display.Label.Should().Be("Not texted");
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
