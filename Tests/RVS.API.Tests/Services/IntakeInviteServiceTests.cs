using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Options;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Security;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace RVS.API.Tests.Services;

/// <summary>
/// Tests for <see cref="IntakeInviteService"/> — creating advisor intake invites
/// (<c>Spec A-14</c>, issue #663).
/// </summary>
public class IntakeInviteServiceTests
{
    private const string TenantId = "ten_nova_rv";
    private const string LocationId = "loc_hurricane";
    private const string Slug = "nova-hurricane";
    private const string AdvisorId = "auth0|advisor-1";
    private const string MessageId = "Outgoing_abc";
    private const string EmailOperationId = "email-op-1";
    private const string RedirectBaseUrl = "https://go.rvintake.com";
    private const string IntakeBaseUrl = "https://rvintake.com";

    private static readonly DateTimeOffset Now = new(2026, 9, 18, 15, 30, 0, TimeSpan.Zero);

    private readonly Mock<IIntakeInviteRepository> _inviteRepoMock = new();
    private readonly Mock<ILocationRepository> _locationRepoMock = new();
    private readonly Mock<ICustomerProfileRepository> _profileRepoMock = new();
    private readonly Mock<ISmsNotificationService> _smsMock = new();
    private readonly Mock<INotificationService> _emailMock = new();
    private readonly Mock<IIntakeInviteRateLimiter> _rateLimiterMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly List<string> _calls = [];

    private IntakeInvite? _created;
    private string? _sentMessage;
    private string? _sentEmailHtml;
    private string? _sentEmailText;

    public IntakeInviteServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns(AdvisorId);

        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, LocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location { Id = LocationId, TenantId = TenantId, Name = "Nova RV Hurricane", Slug = Slug, Phone = "(801) 555-0100" });

        _profileRepoMock.Setup(r => r.ListSmsOptedOutPhonesAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _smsMock.Setup(s => s.IsEnabled).Returns(true);
        _smsMock.Setup(s => s.SendSmsAsync(TenantId, LocationId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, CancellationToken>((_, _, _, message, _) =>
            {
                _calls.Add("send");
                _sentMessage = message;
            })
            .ReturnsAsync(MessageId);

        _emailMock.Setup(e => e.IsEnabled).Returns(true);
        _emailMock.Setup(e => e.SendTransactionalEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, CancellationToken>((_, _, html, text, _) =>
            {
                _calls.Add("email");
                _sentEmailHtml = html;
                _sentEmailText = text;
            })
            .ReturnsAsync(EmailOperationId);

        _rateLimiterMock.Setup(l => l.TryAcquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(IntakeInviteRateLimitResult.Allowed);

        _inviteRepoMock.Setup(r => r.CreateAsync(It.IsAny<IntakeInvite>(), It.IsAny<CancellationToken>()))
            .Callback<IntakeInvite, CancellationToken>((invite, _) =>
            {
                _calls.Add("create");
                _created = invite;
            })
            .ReturnsAsync((IntakeInvite invite, CancellationToken _) => invite);

        _inviteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<IntakeInvite>(), It.IsAny<CancellationToken>()))
            .Callback<IntakeInvite, CancellationToken>((_, _) => _calls.Add("update"))
            .ReturnsAsync((IntakeInvite invite, CancellationToken _) => invite);
    }

    private IntakeInviteService CreateService() => new(
        _inviteRepoMock.Object,
        _locationRepoMock.Object,
        _profileRepoMock.Object,
        _smsMock.Object,
        _emailMock.Object,
        _rateLimiterMock.Object,
        _userContextMock.Object,
        MsOptions.Create(new IntakeInviteOptions()),
        MsOptions.Create(new IntakeUrlOptions { BaseUrl = IntakeBaseUrl, RedirectBaseUrl = RedirectBaseUrl }),
        new FixedTimeProvider(Now),
        Mock.Of<ILogger<IntakeInviteService>>());

    private static IntakeInviteCreateRequestDto TextRequest(string phone = "(801) 555-1234") => new()
    {
        FirstName = "  Jane ",
        Phone = phone,
        ConsentCaptured = true,
    };

    private static IntakeInviteCreateRequestDto EmailRequest(string email = "  Jane.Doe@Example.com ") => new()
    {
        FirstName = "  Jane ",
        Channel = IntakeInviteChannel.Email,
        Email = email,
        ConsentCaptured = true,
    };

    private static IntakeInviteCreateRequestDto SelfEntryRequest(string? phone = null) => new()
    {
        FirstName = "Jane",
        Phone = phone,
        SelfEntry = true,
    };

    // ── Guards ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => CreateService().CreateAsync(tenantId!, LocationId, TextRequest());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateAsync_WhenLocationIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? locationId)
    {
        var act = () => CreateService().CreateAsync(TenantId, locationId!, TextRequest());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_WhenThereIsNoCurrentUser_ShouldThrowUnauthorizedAccessException()
    {
        _userContextMock.Setup(u => u.UserId).Returns((string?)null);

        var act = () => CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WhenFirstNameIsBlank_ShouldThrowArgumentException(string firstName)
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, TextRequest() with { FirstName = firstName });

        await act.Should().ThrowAsync<ArgumentException>();
        _inviteRepoMock.Verify(r => r.CreateAsync(It.IsAny<IntakeInvite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenFirstNameIsTooLong_ShouldThrowArgumentException()
    {
        var request = TextRequest() with { FirstName = new string('J', IntakeInviteCreateRequestDto.FirstNameMaxLength + 1) };

        var act = () => CreateService().CreateAsync(TenantId, LocationId, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_WhenLocationDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, LocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var act = () => CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Refusals ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithoutConsent_ShouldThrowArgumentExceptionAndSendNothing()
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, TextRequest() with { ConsentCaptured = false });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*consent*");
        _calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("555-1234")]
    [InlineData("+44 20 7946 0958")]
    public async Task CreateAsync_WhenPhoneIsNotAUsOrCanadaNumber_ShouldThrowArgumentException(string? phone)
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, TextRequest() with { Phone = phone });

        await act.Should().ThrowAsync<ArgumentException>();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenSmsIsDisabled_ShouldThrowConflictExceptionSayingTextingIsNotEnabled()
    {
        _smsMock.Setup(s => s.IsEnabled).Returns(false);

        var act = () => CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*not enabled*");
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenTheNumberHasOptedOutOfTexts_ShouldThrowConflictExceptionAndSendNothing()
    {
        // Stored as the customer typed it; matched after normalising both sides.
        _profileRepoMock.Setup(r => r.ListSmsOptedOutPhonesAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["801.555.1234"]);

        var act = () => CreateService().CreateAsync(TenantId, LocationId, TextRequest("+1 801 555 1234"));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*opted out*");
        _calls.Should().BeEmpty();
        _rateLimiterMock.Verify(l => l.TryAcquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAnotherNumberHasOptedOut_ShouldStillSend()
    {
        _profileRepoMock.Setup(r => r.ListSmsOptedOutPhonesAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["(435) 555-0000", "not a number"]);

        await CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        _calls.Should().Contain("send");
    }

    [Theory]
    [InlineData(IntakeInviteRateLimitResult.AdvisorLimitReached)]
    [InlineData(IntakeInviteRateLimitResult.LocationLimitReached)]
    [InlineData(IntakeInviteRateLimitResult.TenantLimitReached)]
    public async Task CreateAsync_WhenRateLimited_ShouldThrowRateLimitExceededExceptionAndSendNothing(IntakeInviteRateLimitResult limit)
    {
        _rateLimiterMock.Setup(l => l.TryAcquire(TenantId, LocationId, AdvisorId)).Returns(limit);

        var act = () => CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        await act.Should().ThrowAsync<RateLimitExceededException>();
        _calls.Should().BeEmpty();
    }

    // ── Happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ShouldPersistTheInviteBeforeTextingIt()
    {
        await CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        _calls.Should().Equal("create", "send", "update");
    }

    [Fact]
    public async Task CreateAsync_ShouldRecordConsentAdvisorPhoneAndExpiry()
    {
        await CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        _created.Should().NotBeNull();
        _created!.TenantId.Should().Be(TenantId);
        _created.LocationId.Should().Be(LocationId);
        _created.AdvisorUserId.Should().Be(AdvisorId);
        _created.CreatedByUserId.Should().Be(AdvisorId);
        _created.FirstName.Should().Be("Jane");
        _created.Phone.Should().Be("+18015551234");
        _created.IsSelfEntry.Should().BeFalse();
        _created.ConsentCapturedAtUtc.Should().Be(Now.UtcDateTime);
        _created.ExpiresAtUtc.Should().Be(Now.UtcDateTime.AddHours(72));
        _created.RedeemedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldTextTheAdvisorLinkToTheE164NumberAndStoreOnlyTheTokenHash()
    {
        await CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        _smsMock.Verify(s => s.SendSmsAsync(TenantId, LocationId, "+18015551234", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        var prefix = $"{RedirectBaseUrl}/{Slug}?src=advisor&inv=";
        _sentMessage.Should().Contain(prefix);
        var start = _sentMessage!.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        var token = _sentMessage.Substring(start, InviteToken.Length);

        InviteToken.IsWellFormed(token).Should().BeTrue();
        _created!.Id.Should().Be(InviteToken.Hash(token));
        _created.Id.Should().NotContain(token);
    }

    [Fact]
    public async Task CreateAsync_WhenAcsAcceptsTheText_ShouldStoreTheMessageIdAsQueued()
    {
        var result = await CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        result.Invite.AcsMessageId.Should().Be(MessageId);
        result.Invite.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Queued);
        result.Invite.SentAtUtc.Should().Be(Now.UtcDateTime);
        result.IntakeUrl.Should().BeNull("a texted invite's link went to the caller, not back to the advisor");
        _inviteRepoMock.Verify(r => r.UpdateAsync(
            It.Is<IntakeInvite>(i => i.AcsMessageId == MessageId && i.DeliveryStatus == IntakeInviteDeliveryStatus.Queued),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenTheTextIsNotSent_ShouldMarkTheInviteFailedAndKeepTheConsentRecord()
    {
        _smsMock.Setup(s => s.SendSmsAsync(TenantId, LocationId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        result.Invite.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Failed);
        result.Invite.SentAtUtc.Should().BeNull();
        result.Invite.ConsentCapturedAtUtc.Should().Be(Now.UtcDateTime);
    }

    [Fact]
    public async Task CreateAsync_WhenRecordingTheSendFails_ShouldStillReturnTheInvite()
    {
        // The text is already out. Failing the request would invite a resend, and a second text.
        _inviteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<IntakeInvite>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos unavailable"));

        var result = await CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        result.Invite.AcsMessageId.Should().Be(MessageId);
    }

    // ── Self-entry ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SelfEntry_ShouldWorkWhileSmsIsDisabledAndWithoutConsent()
    {
        _smsMock.Setup(s => s.IsEnabled).Returns(false);

        var result = await CreateService().CreateAsync(TenantId, LocationId, SelfEntryRequest("801-555-1234"));

        result.Invite.IsSelfEntry.Should().BeTrue();
        result.Invite.Phone.Should().Be("+18015551234");
        result.Invite.ConsentCapturedAtUtc.Should().BeNull();
        result.Invite.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.NotSent);
        result.Invite.AdvisorUserId.Should().Be(AdvisorId);
        result.Invite.ExpiresAtUtc.Should().Be(Now.UtcDateTime.AddHours(72));
    }

    [Fact]
    public async Task CreateAsync_SelfEntry_ShouldTextNobodyAndSkipTheRateLimit()
    {
        await CreateService().CreateAsync(TenantId, LocationId, SelfEntryRequest());

        _smsMock.Verify(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _rateLimiterMock.Verify(l => l.TryAcquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _calls.Should().Equal("create");
    }

    [Fact]
    public async Task CreateAsync_SelfEntry_ShouldReturnThePrefilledIntakeUrlCarryingTheToken()
    {
        var result = await CreateService().CreateAsync(TenantId, LocationId, SelfEntryRequest());

        var prefix = $"{IntakeBaseUrl}/{Slug}?src=advisor&inv=";
        result.IntakeUrl.Should().StartWith(prefix);
        var token = result.IntakeUrl![prefix.Length..];
        result.Invite.Id.Should().Be(InviteToken.Hash(token));
    }

    [Fact]
    public async Task CreateAsync_SelfEntry_WhenPhoneIsGivenButInvalid_ShouldThrowArgumentException()
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, SelfEntryRequest("555-1234"));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_SelfEntry_ShouldNotRefuseAnOptedOutNumber()
    {
        // Nothing is texted, so the opt-out has nothing to veto.
        _profileRepoMock.Setup(r => r.ListSmsOptedOutPhonesAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["+18015551234"]);

        var result = await CreateService().CreateAsync(TenantId, LocationId, SelfEntryRequest("+18015551234"));

        result.IntakeUrl.Should().NotBeNull();
    }

    // ── Email channel (issue #693) ───────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenChannelIsOmitted_ShouldTextAndRecordTheSmsChannel()
    {
        // Clients written before #693 send no channel; they meant a text.
        var result = await CreateService().CreateAsync(TenantId, LocationId, TextRequest());

        result.Invite.Channel.Should().Be(IntakeInviteChannel.Sms);
        _calls.Should().Equal("create", "send", "update");
    }

    [Theory]
    [InlineData("fax")]
    [InlineData("EMAIL")]
    public async Task CreateAsync_WhenChannelIsUnknown_ShouldThrowArgumentExceptionAndSendNothing(string channel)
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, EmailRequest() with { Channel = channel });

        await act.Should().ThrowAsync<ArgumentException>();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_Email_WithoutConsent_ShouldThrowArgumentExceptionAndSendNothing()
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, EmailRequest() with { ConsentCaptured = false });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*email*");
        _calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("jane")]
    [InlineData("jane@example")]
    [InlineData("jane@example.com<script>")]
    public async Task CreateAsync_Email_WhenAddressIsInvalid_ShouldThrowArgumentExceptionAndSendNothing(string? email)
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, EmailRequest() with { Email = email });

        await act.Should().ThrowAsync<ArgumentException>();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_Email_WhenEmailIsDisabled_ShouldThrowConflictExceptionAndSendNothing()
    {
        _emailMock.Setup(e => e.IsEnabled).Returns(false);

        var act = () => CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*not enabled*");
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_Email_WhenTheAddressHasOptedOutOfEmail_ShouldThrowConflictExceptionAndSendNothing()
    {
        _profileRepoMock.Setup(r => r.GetByEmailAsync(TenantId, "jane.doe@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerProfile { TenantId = TenantId, Email = "jane.doe@example.com", EmailOptOut = true });

        var act = () => CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*opted out*");
        _calls.Should().BeEmpty();
        _rateLimiterMock.Verify(l => l.TryAcquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Email_WhenTheProfileHasOnlyOptedOutOfTexts_ShouldStillEmail()
    {
        _profileRepoMock.Setup(r => r.GetByEmailAsync(TenantId, "jane.doe@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerProfile { TenantId = TenantId, Email = "jane.doe@example.com", SmsOptOut = true });

        await CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        _calls.Should().Contain("email");
    }

    [Theory]
    [InlineData(IntakeInviteRateLimitResult.AdvisorLimitReached)]
    [InlineData(IntakeInviteRateLimitResult.LocationLimitReached)]
    [InlineData(IntakeInviteRateLimitResult.TenantLimitReached)]
    public async Task CreateAsync_Email_WhenRateLimited_ShouldThrowRateLimitExceededExceptionAndSendNothing(IntakeInviteRateLimitResult limit)
    {
        // One budget for both channels: email is no cheaper a way to spam someone.
        _rateLimiterMock.Setup(l => l.TryAcquire(TenantId, LocationId, AdvisorId)).Returns(limit);

        var act = () => CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        await act.Should().ThrowAsync<RateLimitExceededException>();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_Email_ShouldWorkWhileTextingIsDisabled()
    {
        _smsMock.Setup(s => s.IsEnabled).Returns(false);

        var result = await CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        result.Invite.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Queued);
    }

    [Fact]
    public async Task CreateAsync_Email_ShouldPersistTheInviteBeforeEmailingItAndTextNobody()
    {
        await CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        _calls.Should().Equal("create", "email", "update");
        _smsMock.Verify(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Email_ShouldRecordTheChannelNormalisedAddressConsentAndExpiry()
    {
        await CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        _created.Should().NotBeNull();
        _created!.Channel.Should().Be(IntakeInviteChannel.Email);
        _created.Email.Should().Be("jane.doe@example.com");
        _created.Phone.Should().BeNull();
        _created.FirstName.Should().Be("Jane");
        _created.AdvisorUserId.Should().Be(AdvisorId);
        _created.IsSelfEntry.Should().BeFalse();
        _created.ConsentCapturedAtUtc.Should().Be(Now.UtcDateTime);
        _created.ExpiresAtUtc.Should().Be(Now.UtcDateTime.AddHours(72));
    }

    [Fact]
    public async Task CreateAsync_Email_ShouldEmailTheAdvisorLinkToTheAddressAndStoreOnlyTheTokenHash()
    {
        await CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        _emailMock.Verify(e => e.SendTransactionalEmailAsync(
            "jane.doe@example.com", It.Is<string>(subject => subject.Contains("Nova RV Hurricane")),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        var prefix = $"{RedirectBaseUrl}/{Slug}?src=advisor&amp;inv=";
        _sentEmailHtml.Should().Contain(prefix);
        var start = _sentEmailHtml!.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        var token = _sentEmailHtml.Substring(start, InviteToken.Length);

        InviteToken.IsWellFormed(token).Should().BeTrue();
        _created!.Id.Should().Be(InviteToken.Hash(token));
        _sentEmailText.Should().Contain($"{RedirectBaseUrl}/{Slug}?src=advisor&inv={token}");
    }

    [Fact]
    public async Task CreateAsync_Email_ShouldGiveTheLocationPhoneForQuestions()
    {
        await CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        _sentEmailHtml.Should().Contain("contact Nova RV Hurricane directly at: (801) 555-0100");
        _sentEmailText.Should().Contain("contact Nova RV Hurricane directly at: (801) 555-0100");
    }

    [Fact]
    public async Task CreateAsync_Email_WhenAcsAcceptsTheEmail_ShouldStoreTheOperationIdAsQueued()
    {
        var result = await CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        result.Invite.AcsMessageId.Should().Be(EmailOperationId);
        result.Invite.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Queued);
        result.Invite.SentAtUtc.Should().Be(Now.UtcDateTime);
        result.IntakeUrl.Should().BeNull("an emailed invite's link went to the caller, not back to the advisor");
    }

    [Fact]
    public async Task CreateAsync_Email_WhenTheEmailIsNotSent_ShouldMarkTheInviteFailedAndKeepTheConsentRecord()
    {
        _emailMock.Setup(e => e.SendTransactionalEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await CreateService().CreateAsync(TenantId, LocationId, EmailRequest());

        result.Invite.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Failed);
        result.Invite.SentAtUtc.Should().BeNull();
        result.Invite.ConsentCapturedAtUtc.Should().Be(Now.UtcDateTime);
    }

    [Fact]
    public async Task CreateAsync_Email_WhenAValidPhoneIsAlsoGiven_ShouldKeepItForThePrefill()
    {
        var result = await CreateService().CreateAsync(TenantId, LocationId, EmailRequest() with { Phone = "801-555-1234" });

        result.Invite.Phone.Should().Be("+18015551234");
    }

    [Fact]
    public async Task CreateAsync_Text_WhenAnInvalidEmailIsAlsoGiven_ShouldThrowArgumentException()
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, TextRequest() with { Email = "not-an-email" });

        await act.Should().ThrowAsync<ArgumentException>();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_SelfEntry_WhenAnEmailIsGiven_ShouldStoreItNormalisedAndEmailNobody()
    {
        var result = await CreateService().CreateAsync(TenantId, LocationId, SelfEntryRequest() with { Email = " Jane@Example.com" });

        result.Invite.Email.Should().Be("jane@example.com");
        _emailMock.Verify(e => e.SendTransactionalEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_SelfEntry_WhenEmailIsGivenButInvalid_ShouldThrowArgumentException()
    {
        var act = () => CreateService().CreateAsync(TenantId, LocationId, SelfEntryRequest() with { Email = "jane@" });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task GetByIdAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => CreateService().GetByIdAsync(tenantId!, LocationId, "inv_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _inviteRepoMock.Setup(r => r.GetByIdAsync(TenantId, "inv_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntakeInvite?)null);

        var act = () => CreateService().GetByIdAsync(TenantId, LocationId, "inv_1");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheInviteBelongsToAnotherLocation_ShouldThrowKeyNotFoundException()
    {
        _inviteRepoMock.Setup(r => r.GetByIdAsync(TenantId, "inv_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntakeInvite { Id = "inv_1", TenantId = TenantId, LocationId = "loc_other" });

        var act = () => CreateService().GetByIdAsync(TenantId, LocationId, "inv_1");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ShouldReturnIt()
    {
        var invite = new IntakeInvite { Id = "inv_1", TenantId = TenantId, LocationId = LocationId };
        _inviteRepoMock.Setup(r => r.GetByIdAsync(TenantId, "inv_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        var result = await CreateService().GetByIdAsync(TenantId, LocationId, "inv_1");

        result.Should().BeSameAs(invite);
    }

    // ── ListRecentForCurrentAdvisorAsync ─────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ListRecentForCurrentAdvisorAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => CreateService().ListRecentForCurrentAdvisorAsync(tenantId!, LocationId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ListRecentForCurrentAdvisorAsync_ShouldListTheCurrentAdvisorsInvitesForTheShift()
    {
        var invites = new List<IntakeInvite> { new() { TenantId = TenantId, LocationId = LocationId } };
        _inviteRepoMock.Setup(r => r.ListRecentByAdvisorAsync(
                TenantId, LocationId, AdvisorId, Now.UtcDateTime.AddHours(-12), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invites);

        var result = await CreateService().ListRecentForCurrentAdvisorAsync(TenantId, LocationId);

        result.Should().BeSameAs(invites);
    }

    [Fact]
    public async Task ListRecentForCurrentAdvisorAsync_WhenThereIsNoCurrentUser_ShouldThrowUnauthorizedAccessException()
    {
        _userContextMock.Setup(u => u.UserId).Returns((string?)null);

        var act = () => CreateService().ListRecentForCurrentAdvisorAsync(TenantId, LocationId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── GetCapability ────────────────────────────────────────────────────

    [Fact]
    public void GetCapability_WhenTextingIsEnabled_ShouldReportItEnabled()
    {
        _smsMock.Setup(s => s.IsEnabled).Returns(true);

        CreateService().GetCapability().SmsEnabled.Should().BeTrue();
    }

    [Fact]
    public void GetCapability_WhenTextingIsDisabled_ShouldReportItDisabled()
    {
        // The send dialog reads this to show "texting not yet enabled" instead of a dead Send
        // button, rather than discovering it from a 409 after the advisor has typed a number.
        _smsMock.Setup(s => s.IsEnabled).Returns(false);

        CreateService().GetCapability().SmsEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetCapability_ShouldReportWhetherEmailIsEnabled(bool enabled)
    {
        _emailMock.Setup(e => e.IsEnabled).Returns(enabled);

        CreateService().GetCapability().EmailEnabled.Should().Be(enabled);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
