using RVS.API.Integrations;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Handles the inbound ACS events delivered by Event Grid (issue #665): carrier keywords texted
/// to the sending number, and delivery reports for texts RVS sent.
///
/// A keyword handler, not a conversation. On a toll-free number the carrier enforces STOP itself
/// and sends its own reply; this mirrors the customer's latest keyword into RVS's records so a
/// send is refused up front instead of failing at the carrier, and so the opt-out survives a
/// location moving to its own number later.
/// </summary>
public sealed class InboundSmsEventService : IInboundSmsEventService
{
    /// <summary>Audit identity for a write no human initiated.</summary>
    private const string SystemUserId = "system:sms-inbound";

    private readonly ICustomerProfileRepository _customerProfiles;
    private readonly IIntakeInviteRepository _invites;
    private readonly ISmsNotificationService _sms;
    private readonly IInboundSmsDeduplicator _deduplicator;
    private readonly ILogger<InboundSmsEventService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="InboundSmsEventService"/>.
    /// </summary>
    public InboundSmsEventService(
        ICustomerProfileRepository customerProfiles,
        IIntakeInviteRepository invites,
        ISmsNotificationService sms,
        IInboundSmsDeduplicator deduplicator,
        ILogger<InboundSmsEventService> logger)
    {
        ArgumentNullException.ThrowIfNull(customerProfiles);
        ArgumentNullException.ThrowIfNull(invites);
        ArgumentNullException.ThrowIfNull(sms);
        ArgumentNullException.ThrowIfNull(deduplicator);
        ArgumentNullException.ThrowIfNull(logger);

        _customerProfiles = customerProfiles;
        _invites = invites;
        _sms = sms;
        _deduplicator = deduplicator;
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
            await ReplyToHelpAsync(phoneE164, inboundMessageId, cancellationToken);
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

    /// <summary>
    /// Sends the one fixed HELP reply. It changes no state and needs no customer record, so a
    /// number RVS has never seen still gets an answer.
    ///
    /// Guarded by the deduplicator, because Event Grid delivers at least once and a second reply
    /// to one HELP is exactly the kind of noise the carriers police. A failed send is logged and
    /// swallowed: throwing would 500 the webhook and have Event Grid retry the whole event.
    /// </summary>
    private async Task ReplyToHelpAsync(string phoneE164, string inboundMessageId, CancellationToken cancellationToken)
    {
        if (!_deduplicator.TryBeginHandling(inboundMessageId))
        {
            _logger.LogInformation("Inbound HELP {MessageId} already answered; not replying again", inboundMessageId);
            return;
        }

        try
        {
            // Returns null rather than throwing when SMS is disabled for the environment.
            var sentMessageId = await _sms.SendSystemSmsAsync(phoneE164, InboundSmsReplyContent.Help, cancellationToken);
            _logger.LogInformation(
                "Inbound HELP {MessageId} answered (reply message id {ReplyMessageId})",
                inboundMessageId, sentMessageId ?? "none — SMS is disabled or the send failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to answer inbound HELP {MessageId}", inboundMessageId);
        }
    }

    /// <inheritdoc />
    public async Task<bool> HandleDeliveryReportAsync(
        string acsMessageId,
        string? deliveryStatus,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acsMessageId);

        var status = MapDeliveryStatus(deliveryStatus);
        if (status is null)
        {
            _logger.LogDebug(
                "Delivery report for {MessageId} carried status {Status}, which is not terminal; ignored",
                acsMessageId, deliveryStatus);
            return false;
        }

        var invite = await _invites.GetByAcsMessageIdAcrossTenantsAsync(acsMessageId, cancellationToken);
        if (invite is null)
        {
            // Confirmations (Spec A-2) go out on the same number and raise reports too.
            _logger.LogDebug("Delivery report for {MessageId} matched no invite", acsMessageId);
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
    /// Maps an ACS delivery status to an <see cref="IntakeInviteDeliveryStatus"/>, or
    /// <c>null</c> when the report says nothing terminal and the invite should be left alone.
    /// <c>Failed</c> covers the carrier having already honoured a STOP for the number, which ACS
    /// reports as "Sender blocked for given recipient".
    /// </summary>
    private static string? MapDeliveryStatus(string? acsStatus) => acsStatus?.Trim().ToLowerInvariant() switch
    {
        "delivered" => IntakeInviteDeliveryStatus.Delivered,
        "failed" or "expired" => IntakeInviteDeliveryStatus.Failed,
        _ => null,
    };
}
