using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

/// <summary>
/// Tests for <see cref="InboundSmsEventService"/> — inbound carrier keywords and delivery
/// reports from ACS via Event Grid (issue #665).
/// </summary>
public class InboundSmsEventServiceTests
{
    private const string Phone = "+18015551234";
    private const string MessageId = "Outgoing_abc";

    private static readonly DateTime EventAt = new(2026, 9, 19, 16, 0, 0, DateTimeKind.Utc);

    private const string InboundMessageId = "Incoming_1";

    private readonly Mock<ICustomerProfileRepository> _profiles = new(MockBehavior.Strict);
    private readonly Mock<IIntakeInviteRepository> _invites = new(MockBehavior.Strict);
    private readonly Mock<ISmsNotificationService> _sms = new();
    private readonly Mock<IInboundSmsDeduplicator> _deduplicator = new();

    public InboundSmsEventServiceTests()
    {
        // Default: every inbound message is new. A test that wants a redelivery overrides this.
        _deduplicator.Setup(d => d.TryBeginHandling(It.IsAny<string>())).Returns(true);
    }

    private InboundSmsEventService CreateService()
    {
        return new InboundSmsEventService(
            _profiles.Object,
            _invites.Object,
            _sms.Object,
            _deduplicator.Object,
            Mock.Of<ILogger<InboundSmsEventService>>());
    }

    private static CustomerProfile Profile(string tenantId, string id) => new()
    {
        Id = id,
        TenantId = tenantId,
        Email = $"{id}@example.com",
        Phone = "(801) 555-1234",
        PhoneE164 = Phone,
    };

    private static IntakeInvite Invite() => new()
    {
        Id = "hash-1",
        TenantId = "ten_nova_rv",
        LocationId = "loc_hurricane",
        AdvisorUserId = "auth0|advisor-1",
        FirstName = "Kim",
        Phone = Phone,
        AcsMessageId = MessageId,
        DeliveryStatus = IntakeInviteDeliveryStatus.Queued,
    };

    // ---- Inbound keywords -------------------------------------------------------------------

    [Fact]
    public async Task HandleInboundMessageAsync_WhenStop_ShouldOptOutEveryTenantsProfile()
    {
        // The toll-free number is shared, and the carrier blocks it for every dealer at once.
        var acme = Profile("ten_acme_rv", "cp-1");
        var nova = Profile("ten_nova_rv", "cp-2");
        _profiles.Setup(r => r.ListByPhoneE164AcrossTenantsAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync([acme, nova]);
        _profiles.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile p, CancellationToken _) => p);

        var changed = await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "STOP", EventAt);

        changed.Should().Be(2);
        acme.SmsOptOut.Should().BeTrue();
        nova.SmsOptOut.Should().BeTrue();
        _profiles.Verify(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenStart_ShouldClearTheOptOut()
    {
        var profile = Profile("ten_acme_rv", "cp-1");
        profile.SmsOptOut = true;
        profile.SmsOptOutAtUtc = EventAt.AddDays(-1);
        _profiles.Setup(r => r.ListByPhoneE164AcrossTenantsAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        _profiles.Setup(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var changed = await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "start", EventAt);

        changed.Should().Be(1);
        profile.SmsOptOut.Should().BeFalse();
        profile.SmsOptInAtUtc.Should().Be(EventAt);
    }

    [Theory]
    [InlineData("thanks!")]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleInboundMessageAsync_WhenNotAKeyword_ShouldNotEvenLookUpTheNumber(string? body)
    {
        // Strict mocks: any repository call here fails the test.
        var changed = await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, body, EventAt);

        changed.Should().Be(0);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenNumberMatchesNoProfile_ShouldBeANoOp()
    {
        // An invite recipient who texts STOP before ever submitting has no profile. The carrier
        // still enforces its own block, so nothing is wrong.
        _profiles.Setup(r => r.ListByPhoneE164AcrossTenantsAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var changed = await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "STOP", EventAt);

        changed.Should().Be(0);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenEventIsOlderThanTheLastKeyword_ShouldNotPersist()
    {
        // Event Grid is unordered: a stale STOP must not undo a later START.
        var profile = Profile("ten_acme_rv", "cp-1");
        profile.SmsKeywordAtUtc = EventAt;
        _profiles.Setup(r => r.ListByPhoneE164AcrossTenantsAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);

        var changed = await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "STOP", EventAt.AddMinutes(-5));

        changed.Should().Be(0);
        profile.SmsOptOut.Should().BeFalse();
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenOneProfileWriteFails_ShouldStillUpdateTheOthers()
    {
        // One dealer's write failing must not leave the other dealers still texting.
        var acme = Profile("ten_acme_rv", "cp-1");
        var nova = Profile("ten_nova_rv", "cp-2");
        _profiles.Setup(r => r.ListByPhoneE164AcrossTenantsAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync([acme, nova]);
        _profiles.Setup(r => r.UpdateAsync(acme, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("conflict"));
        _profiles.Setup(r => r.UpdateAsync(nova, It.IsAny<CancellationToken>())).ReturnsAsync(nova);

        var changed = await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "STOP", EventAt);

        changed.Should().Be(1);
        _profiles.Verify(r => r.UpdateAsync(nova, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenNumberIsNotE164_ShouldNormaliseItFirst()
    {
        _profiles.Setup(r => r.ListByPhoneE164AcrossTenantsAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var changed = await CreateService().HandleInboundMessageAsync("(801) 555-1234", InboundMessageId, "STOP", EventAt);

        changed.Should().Be(0);
        _profiles.Verify(r => r.ListByPhoneE164AcrossTenantsAsync(Phone, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenNumberCannotNormalise_ShouldBeANoOp()
    {
        var changed = await CreateService().HandleInboundMessageAsync("not-a-number", InboundMessageId, "STOP", EventAt);

        changed.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleInboundMessageAsync_WhenFromIsMissing_ShouldThrowArgumentException(string? from)
    {
        var act = () => CreateService().HandleInboundMessageAsync(from!, InboundMessageId, "STOP", EventAt);

        await act.Should().ThrowAsync<ArgumentException>();
    }


    // ---- HELP (issue #665) ------------------------------------------------------------------

    [Fact]
    public async Task HandleInboundMessageAsync_WhenHelp_ShouldSendTheFixedReplyAndTouchNoRecords()
    {
        // Strict repository mocks: HELP needs no CustomerProfile and changes no state.
        var changed = await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "HELP", EventAt);

        changed.Should().Be(0);
        _sms.Verify(
            s => s.SendSystemSmsAsync(Phone, InboundSmsReplyContent.Help, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenHelpIsRedelivered_ShouldNotTextTwice()
    {
        // Event Grid delivers at least once; a second reply to one HELP is a real annoyance.
        _deduplicator.Setup(d => d.TryBeginHandling(InboundMessageId)).Returns(false);

        await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "HELP", EventAt);

        _sms.Verify(
            s => s.SendSystemSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenHelpAndSmsIsDisabled_ShouldStillNotThrow()
    {
        // Sms:Enabled is checked inside the send, which returns null rather than throwing.
        _sms.Setup(s => s.SendSystemSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var changed = await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "help", EventAt);

        changed.Should().Be(0);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenHelpSendThrows_ShouldNotBubbleUp()
    {
        // A failed reply must not make the webhook 500 and have Event Grid retry the whole event.
        _sms.Setup(s => s.SendSystemSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("acs down"));

        var act = () => CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "HELP", EventAt);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleInboundMessageAsync_WhenStop_ShouldNotSendAnyReply()
    {
        // The carrier already replied to STOP; a second message would be noise.
        var profile = Profile("ten_acme_rv", "cp-1");
        _profiles.Setup(r => r.ListByPhoneE164AcrossTenantsAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        _profiles.Setup(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        await CreateService().HandleInboundMessageAsync(Phone, InboundMessageId, "STOP", EventAt);

        _sms.Verify(
            s => s.SendSystemSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---- Delivery reports -------------------------------------------------------------------

    [Theory]
    [InlineData("Delivered", IntakeInviteDeliveryStatus.Delivered)]
    [InlineData("delivered", IntakeInviteDeliveryStatus.Delivered)]
    [InlineData("Failed", IntakeInviteDeliveryStatus.Failed)]
    [InlineData("Expired", IntakeInviteDeliveryStatus.Failed)]
    public async Task HandleDeliveryReportAsync_WhenInviteIsKnown_ShouldRecordTheStatus(
        string acsStatus, string expected)
    {
        var invite = Invite();
        _invites.Setup(r => r.GetByAcsMessageIdAcrossTenantsAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);
        _invites.Setup(r => r.UpdateAsync(invite, It.IsAny<CancellationToken>())).ReturnsAsync(invite);

        var updated = await CreateService().HandleDeliveryReportAsync(MessageId, acsStatus, EventAt);

        updated.Should().BeTrue();
        invite.DeliveryStatus.Should().Be(expected);
    }

    [Fact]
    public async Task HandleDeliveryReportAsync_WhenSenderBlocked_ShouldRecordFailed()
    {
        // What ACS reports when the carrier has already honoured a STOP for this number.
        var invite = Invite();
        _invites.Setup(r => r.GetByAcsMessageIdAcrossTenantsAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);
        _invites.Setup(r => r.UpdateAsync(invite, It.IsAny<CancellationToken>())).ReturnsAsync(invite);

        var updated = await CreateService().HandleDeliveryReportAsync(MessageId, "Failed", EventAt);

        updated.Should().BeTrue();
        invite.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Failed);
    }

    [Fact]
    public async Task HandleDeliveryReportAsync_WhenStatusIsUnrecognised_ShouldLeaveTheInviteAlone()
    {
        var invite = Invite();
        _invites.Setup(r => r.GetByAcsMessageIdAcrossTenantsAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        var updated = await CreateService().HandleDeliveryReportAsync(MessageId, "Unknown", EventAt);

        updated.Should().BeFalse();
        invite.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Queued);
    }

    [Fact]
    public async Task HandleDeliveryReportAsync_WhenMessageIsNotAnInvite_ShouldBeANoOp()
    {
        // A-2 confirmations go through the same number and raise reports too.
        _invites.Setup(r => r.GetByAcsMessageIdAcrossTenantsAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntakeInvite?)null);

        var updated = await CreateService().HandleDeliveryReportAsync(MessageId, "Delivered", EventAt);

        updated.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleDeliveryReportAsync_WhenMessageIdIsMissing_ShouldThrowArgumentException(string? id)
    {
        var act = () => CreateService().HandleDeliveryReportAsync(id!, "Delivered", EventAt);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
