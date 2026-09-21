using Microsoft.Extensions.Options;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Links;
using RVS.Domain.Security;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Creates advisor intake invites (<c>Spec A-14</c>, issue #663): a prefilled, single-use intake
/// link texted to a caller who agreed to receive it, or opened by the advisor with
/// <i>Fill it in myself</i>.
///
/// A texted invite passes its refusals in order (consent, a valid number, texting enabled, the
/// number not opted out, rate limits) before anything is written. It is then persisted
/// <b>before</b> the text is sent, so the consent record exists even if the send fails, and
/// updated with the ACS message id afterwards.
/// </summary>
public sealed class IntakeInviteService : IIntakeInviteService
{
    private readonly IIntakeInviteRepository _inviteRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly ICustomerProfileRepository _customerProfileRepository;
    private readonly ISmsNotificationService _smsService;
    private readonly IIntakeInviteRateLimiter _rateLimiter;
    private readonly IUserContextAccessor _userContext;
    private readonly IntakeInviteOptions _options;
    private readonly IntakeUrlOptions _intakeUrlOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IntakeInviteService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeInviteService"/>.
    /// </summary>
    public IntakeInviteService(
        IIntakeInviteRepository inviteRepository,
        ILocationRepository locationRepository,
        ICustomerProfileRepository customerProfileRepository,
        ISmsNotificationService smsService,
        IIntakeInviteRateLimiter rateLimiter,
        IUserContextAccessor userContext,
        IOptions<IntakeInviteOptions> options,
        IOptions<IntakeUrlOptions> intakeUrlOptions,
        TimeProvider timeProvider,
        ILogger<IntakeInviteService> logger)
    {
        _inviteRepository = inviteRepository;
        _locationRepository = locationRepository;
        _customerProfileRepository = customerProfileRepository;
        _smsService = smsService;
        _rateLimiter = rateLimiter;
        _userContext = userContext;
        _options = options.Value;
        _intakeUrlOptions = intakeUrlOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IntakeInviteCreateResult> CreateAsync(
        string tenantId, string locationId, IntakeInviteCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentNullException.ThrowIfNull(request);

        var advisorUserId = CurrentUserIdOrThrow();
        var firstName = ValidateFirstName(request.FirstName);

        string? phone;
        if (request.SelfEntry)
        {
            phone = string.IsNullOrWhiteSpace(request.Phone) ? null : NormalizePhoneOrThrow(request.Phone);
        }
        else
        {
            if (!request.ConsentCaptured)
            {
                throw new ArgumentException(
                    "Confirm that the caller gave consent to receive the text before sending it.", nameof(request));
            }

            phone = NormalizePhoneOrThrow(request.Phone);
        }

        var location = await _locationRepository.GetByIdAsync(tenantId, locationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Location '{locationId}' was not found.");

        return request.SelfEntry
            ? await CreateSelfEntryAsync(tenantId, location, advisorUserId, firstName, phone, cancellationToken)
            : await CreateAndTextAsync(tenantId, location, advisorUserId, firstName, phone!, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IntakeInvite> GetByIdAsync(
        string tenantId, string locationId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var invite = await _inviteRepository.GetByIdAsync(tenantId, id, cancellationToken);

        // An invite at another location is reported the same as a missing one.
        if (invite is null || !string.Equals(invite.LocationId, locationId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException($"Intake invite '{id}' was not found.");
        }

        return invite;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IntakeInvite>> ListRecentForCurrentAdvisorAsync(
        string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        var advisorUserId = CurrentUserIdOrThrow();
        var since = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-_options.RecentWindowHours);

        return _inviteRepository.ListRecentByAdvisorAsync(
            tenantId, locationId, advisorUserId, since, _options.RecentMaxItems, cancellationToken);
    }

    /// <inheritdoc />
    public IntakeInviteCapability GetCapability() => new(SmsEnabled: _smsService.IsEnabled);

    private async Task<IntakeInviteCreateResult> CreateSelfEntryAsync(
        string tenantId, Location location, string advisorUserId, string firstName, string? phone,
        CancellationToken cancellationToken)
    {
        var token = InviteToken.Generate();
        var invite = NewInvite(tenantId, location.Id, advisorUserId, firstName, phone, token, isSelfEntry: true);

        await _inviteRepository.CreateAsync(invite, cancellationToken);

        _logger.LogInformation(
            "Intake invite (self-entry) created for tenant {TenantId}, location {LocationId}", tenantId, location.Id);

        var intakeUrl = IntakeLinkBuilder.IntakeUrl(
            _intakeUrlOptions.BaseUrl, location.Slug, IntakeSourceVocabulary.Advisor, token);

        return new IntakeInviteCreateResult(invite, intakeUrl);
    }

    private async Task<IntakeInviteCreateResult> CreateAndTextAsync(
        string tenantId, Location location, string advisorUserId, string firstName, string phone,
        CancellationToken cancellationToken)
    {
        if (!_smsService.IsEnabled)
        {
            throw new ConflictException(
                "Texting is not enabled yet. Use Fill it in myself to complete the intake during the call.");
        }

        if (await IsSmsOptedOutAsync(tenantId, phone, cancellationToken))
        {
            throw new ConflictException(
                "This number has opted out of texts. Use Fill it in myself to complete the intake during the call.");
        }

        var limit = _rateLimiter.TryAcquire(tenantId, location.Id, advisorUserId);
        if (limit != IntakeInviteRateLimitResult.Allowed)
        {
            throw new RateLimitExceededException(limit switch
            {
                IntakeInviteRateLimitResult.AdvisorLimitReached => "You've sent the most intake links allowed in an hour. Try again later.",
                IntakeInviteRateLimitResult.LocationLimitReached => "This location has sent the most intake links allowed in an hour. Try again later.",
                _ => "Your dealership has sent the most intake links allowed in an hour. Try again later.",
            });
        }

        var token = InviteToken.Generate();
        var invite = NewInvite(tenantId, location.Id, advisorUserId, firstName, phone, token, isSelfEntry: false);

        // Persist first: the consent record must exist whether or not the text goes out.
        await _inviteRepository.CreateAsync(invite, cancellationToken);

        var link = IntakeLinkBuilder.ShortLink(
            _intakeUrlOptions.RedirectOrIntakeBaseUrl, location.Slug, IntakeSourceVocabulary.Advisor, token);
        var message = IntakeInviteContent.BuildSmsBody(location.Name, firstName, link);

        var messageId = await _smsService.SendSmsAsync(tenantId, location.Id, phone, message, cancellationToken);

        invite.AcsMessageId = messageId;
        invite.SentAtUtc = messageId is null ? null : _timeProvider.GetUtcNow().UtcDateTime;
        invite.DeliveryStatus = messageId is null ? IntakeInviteDeliveryStatus.Failed : IntakeInviteDeliveryStatus.Queued;
        invite.MarkAsUpdated(advisorUserId);

        try
        {
            await _inviteRepository.UpdateAsync(invite, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The text is already out and the token works regardless of this field. Failing the
            // request would only prompt a resend, and a second text to the caller.
            _logger.LogError(ex,
                "Intake invite {InviteId} for tenant {TenantId}: failed to record the send (MessageId: {MessageId})",
                invite.Id, tenantId, messageId);
        }

        _logger.LogInformation(
            "Intake invite texted for tenant {TenantId}, location {LocationId}: {DeliveryStatus}",
            tenantId, location.Id, invite.DeliveryStatus);

        return new IntakeInviteCreateResult(invite, null);
    }

    private IntakeInvite NewInvite(
        string tenantId, string locationId, string advisorUserId, string firstName, string? phone,
        string token, bool isSelfEntry)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        return new IntakeInvite
        {
            Id = InviteToken.Hash(token),
            TenantId = tenantId,
            LocationId = locationId,
            AdvisorUserId = advisorUserId,
            CreatedByUserId = advisorUserId,
            CreatedAtUtc = now,
            FirstName = firstName,
            Phone = phone,
            IsSelfEntry = isSelfEntry,
            ConsentCapturedAtUtc = isSelfEntry ? null : now,
            ExpiresAtUtc = now.AddHours(_options.ExpiryHours),
            DeliveryStatus = isSelfEntry ? IntakeInviteDeliveryStatus.NotSent : IntakeInviteDeliveryStatus.Pending,
        };
    }

    /// <summary>
    /// Whether any profile in the tenant that opted out of texts has this number. Stored numbers
    /// are as the customer typed them, so each is normalised before comparing.
    /// </summary>
    private async Task<bool> IsSmsOptedOutAsync(string tenantId, string e164, CancellationToken cancellationToken)
    {
        var optedOut = await _customerProfileRepository.ListSmsOptedOutPhonesAsync(tenantId, cancellationToken);

        return optedOut.Any(stored => PhoneNumberNormalizer.Normalize(stored) == e164);
    }

    private string CurrentUserIdOrThrow() =>
        string.IsNullOrWhiteSpace(_userContext.UserId)
            ? throw new UnauthorizedAccessException("User identifier is missing.")
            : _userContext.UserId;

    private static string ValidateFirstName(string? firstName)
    {
        var trimmed = firstName?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("Enter the caller's first name.", nameof(firstName));
        }

        if (trimmed.Length > IntakeInviteCreateRequestDto.FirstNameMaxLength
            || trimmed.Any(c => char.IsControl(c) || c is '<' or '>'))
        {
            throw new ArgumentException(
                $"The first name must be at most {IntakeInviteCreateRequestDto.FirstNameMaxLength} characters of plain text.",
                nameof(firstName));
        }

        return trimmed;
    }

    private static string NormalizePhoneOrThrow(string? phone) =>
        PhoneNumberNormalizer.Normalize(phone)
            ?? throw new ArgumentException("Enter a valid US or Canadian phone number.", nameof(phone));
}
