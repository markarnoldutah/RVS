using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Handles the inbound events Twilio's messaging webhooks deliver (issue #665): carrier keywords
/// texted to the sending number, and status callbacks for texts RVS sent.
///
/// A keyword handler, not a conversation. Twilio's Advanced Opt-Out enforces STOP and answers
/// STOP, START and HELP itself; this mirrors the customer's latest STOP or START into RVS's
/// records so a send is refused up front instead of failing at Twilio, and so the opt-out
/// survives a location moving to its own number later. RVS sends no reply of its own.
/// </summary>
public sealed class InboundSmsEventService : IInboundSmsEventService
{
    /// <summary>Audit identity for a write no human initiated.</summary>
    private const string SystemUserId = "system:sms-inbound";

    private readonly ICustomerProfileRepository _customerProfiles;
    private readonly IIntakeInviteRepository _invites;
    private readonly ILogger<InboundSmsEventService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="InboundSmsEventService"/>.
    /// </summary>
    public InboundSmsEventService(
        ICustomerProfileRepository customerProfiles,
        IIntakeInviteRepository invites,
        ILogger<InboundSmsEventService> logger)
    {
        ArgumentNullException.ThrowIfNull(customerProfiles);
        ArgumentNullException.ThrowIfNull(invites);
        ArgumentNullException.ThrowIfNull(logger);

        _customerProfiles = customerProfiles;
        _invites = invites;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> HandleInboundMessageAsync(
        string fromPhoneNumber,
        string inboundMessageId,
        string? messageBody,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(inboundMessageId);

        var keyword = SmsKeywordVocabulary.Classify(messageBody);
        if (keyword == SmsKeyword.None)
        {
            // Everything that is not an exact keyword is ignored: not stored, not answered.
            // The body is never logged — it is a customer's message (Spec X-7).
            return 0;
        }

        if (!PhoneNumberNormalizer.TryNormalize(fromPhoneNumber, out var phoneE164))
        {
            _logger.LogWarning(
                "Inbound SMS keyword {Keyword} from a number that does not normalise to E.164; ignored",
                keyword);
            return 0;
        }

        if (keyword == SmsKeyword.Help)
        {
            // Twilio's Advanced Opt-Out has already sent the fixed HELP reply configured on the
            // Messaging Service. HELP changes no state, so there is nothing left to do.
            return 0;
        }

        var profiles = await _customerProfiles.ListByPhoneE164AcrossTenantsAsync(phoneE164, cancellationToken);
        if (profiles.Count == 0)
        {
            // Normal: an invite recipient who never submitted has no profile. The carrier still
            // enforces its own block.
            _logger.LogInformation("Inbound SMS keyword {Keyword} matched no customer profile", keyword);
            return 0;
        }

        var changed = 0;
        foreach (var profile in profiles)
        {
            if (!profile.ApplySmsKeyword(keyword, receivedAtUtc))
            {
                continue;
            }

            profile.MarkAsUpdated(SystemUserId);

            try
            {
                await _customerProfiles.UpdateAsync(profile, cancellationToken);
                changed++;
            }
            catch (Exception ex)
            {
                // One dealer's write failing must not leave the other dealers still texting.
                _logger.LogError(
                    ex,
                    "Inbound SMS keyword {Keyword}: failed to update profile {ProfileId} in tenant {TenantId}",
                    keyword, profile.Id, profile.TenantId);
            }
        }

        _logger.LogInformation(
            "Inbound SMS keyword {Keyword} applied to {Changed} of {Matched} customer profiles",
            keyword, changed, profiles.Count);

        return changed;
    }

    /// <inheritdoc />
    public async Task<bool> HandleDeliveryReportAsync(
        string providerMessageId,
        string? deliveryStatus,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMessageId);

        var status = MapDeliveryStatus(deliveryStatus);
        if (status is null)
        {
            _logger.LogDebug(
                "Delivery report for {MessageId} carried status {Status}, which is not terminal; ignored",
                providerMessageId, deliveryStatus);
            return false;
        }

        var invite = await _invites.GetByProviderMessageIdAcrossTenantsAsync(providerMessageId, cancellationToken);
        if (invite is null)
        {
            // Confirmations (Spec A-2) go out on the same number and raise callbacks too.
            _logger.LogDebug("Delivery report for {MessageId} matched no invite", providerMessageId);
            return false;
        }

        invite.DeliveryStatus = status;
        invite.MarkAsUpdated(SystemUserId);
        await _invites.UpdateAsync(invite, cancellationToken);

        _logger.LogInformation(
            "Invite {InviteId} in tenant {TenantId} reported {Status} at {ReceivedAtUtc:o}",
            invite.Id, invite.TenantId, status, receivedAtUtc);

        return true;
    }

    /// <summary>
    /// Maps a Twilio message status to an <see cref="IntakeInviteDeliveryStatus"/>, or
    /// <c>null</c> when the callback says nothing terminal (<c>queued</c>, <c>accepted</c>,
    /// <c>sending</c>, <c>sent</c>) and the invite should be left alone. <c>undelivered</c> is
    /// the carrier refusing the message; <c>failed</c> is Twilio never sending it.
    /// </summary>
    private static string? MapDeliveryStatus(string? twilioStatus) => twilioStatus?.Trim().ToLowerInvariant() switch
    {
        "delivered" => IntakeInviteDeliveryStatus.Delivered,
        "undelivered" or "failed" => IntakeInviteDeliveryStatus.Failed,
        _ => null,
    };
}
