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
    private const string RedirectBaseUrl = "https://go.rvintake.com";
    private const string IntakeBaseUrl = "https://rvintake.com";

    private static readonly DateTimeOffset Now = new(2026, 9, 18, 15, 30, 0, TimeSpan.Zero);

    private readonly Mock<IIntakeInviteRepository> _inviteRepoMock = new();
    private readonly Mock<ILocationRepository> _locationRepoMock = new();
    private readonly Mock<ICustomerProfileRepository> _profileRepoMock = new();
    private readonly Mock<ISmsNotificationService> _smsMock = new();
    private readonly Mock<IIntakeInviteRateLimiter> _rateLimiterMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly List<string> _calls = [];

    private IntakeInvite? _created;
    private string? _sentMessage;

    public IntakeInviteServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns(AdvisorId);

        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, LocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location { Id = LocationId, TenantId = TenantId, Name = "Nova RV Hurricane", Slug = Slug });

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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
