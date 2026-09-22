using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// The redeemability rule and the redeem transition of an A-14 invite (<c>Spec A-14</c>, issue #664).
/// </summary>
public class IntakeInviteTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

    private static IntakeInvite BuildInvite(DateTime? expiresAtUtc = null, DateTime? redeemedAtUtc = null) => new()
    {
        Id = new string('a', 64),
        TenantId = "ten_test",
        LocationId = "loc_test",
        AdvisorUserId = "auth0|advisor",
        FirstName = "Jane",
        Phone = "+18015551234",
        ExpiresAtUtc = expiresAtUtc ?? Now.AddHours(72),
        RedeemedAtUtc = redeemedAtUtc,
        CreatedByUserId = "auth0|advisor",
    };

    // ── IsRedeemableAt ───────────────────────────────────────────────────────

    [Fact]
    public void IsRedeemableAt_WhenUnexpiredAndUnredeemed_ShouldBeTrue()
    {
        BuildInvite().IsRedeemableAt(Now).Should().BeTrue();
    }

    [Fact]
    public void IsRedeemableAt_WhenExpired_ShouldBeFalse()
    {
        BuildInvite(expiresAtUtc: Now.AddSeconds(-1)).IsRedeemableAt(Now).Should().BeFalse();
    }

    [Fact]
    public void IsRedeemableAt_AtTheExpiryInstant_ShouldBeFalse()
    {
        BuildInvite(expiresAtUtc: Now).IsRedeemableAt(Now).Should().BeFalse();
    }

    [Fact]
    public void IsRedeemableAt_WhenAlreadyRedeemed_ShouldBeFalse()
    {
        BuildInvite(redeemedAtUtc: Now.AddHours(-1)).IsRedeemableAt(Now).Should().BeFalse();
    }

    // ── MarkRedeemed ─────────────────────────────────────────────────────────

    [Fact]
    public void MarkRedeemed_ShouldStampRedemptionAndServiceRequest()
    {
        var invite = BuildInvite();

        invite.MarkRedeemed("sr_1", Now, "intake");

        invite.RedeemedAtUtc.Should().Be(Now);
        invite.ServiceRequestId.Should().Be("sr_1");
        invite.UpdatedByUserId.Should().Be("intake");
        invite.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkRedeemed_ShouldSpendTheToken()
    {
        var invite = BuildInvite();

        invite.MarkRedeemed("sr_1", Now, "intake");

        invite.IsRedeemableAt(Now).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MarkRedeemed_WhenServiceRequestIdIsBlank_ShouldThrowArgumentException(string? serviceRequestId)
    {
        var act = () => BuildInvite().MarkRedeemed(serviceRequestId!, Now, "intake");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkRedeemed_WhenAlreadyRedeemed_ShouldThrowInvalidOperationException()
    {
        var invite = BuildInvite(redeemedAtUtc: Now.AddHours(-1));

        var act = () => invite.MarkRedeemed("sr_2", Now, "intake");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Channel (issue #693) ─────────────────────────────────────────────────

    [Fact]
    public void Channel_WhenTheStoredDocumentPredatesIt_ShouldReadAsSms()
    {
        // Every invite written before #693 was texted and has no "channel" field.
        var json = """{"id":"inv_1","tenantId":"ten_test","locationId":"loc_test","firstName":"Jane","phone":"+18015551234"}""";

        var invite = Newtonsoft.Json.JsonConvert.DeserializeObject<IntakeInvite>(json);

        invite!.Channel.Should().Be(IntakeInviteChannel.Sms);
        invite.Email.Should().BeNull();
    }

    [Fact]
    public void Channel_ShouldRoundTripAsCamelCaseJson()
    {
        var invite = new IntakeInvite { Id = "inv_1", TenantId = "ten_test", Channel = IntakeInviteChannel.Email, Email = "jane@example.com" };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(invite);

        json.Should().Contain("\"channel\":\"email\"").And.Contain("\"email\":\"jane@example.com\"");
    }

    [Theory]
    [InlineData("sms", true)]
    [InlineData("email", true)]
    [InlineData("SMS", false)]
    [InlineData("fax", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IntakeInviteChannel_IsKnown_ShouldAcceptOnlyTheTwoChannels(string? channel, bool expected)
    {
        IntakeInviteChannel.IsKnown(channel).Should().Be(expected);
    }
}
