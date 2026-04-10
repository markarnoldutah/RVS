namespace RVS.Domain.Integrations;

/// <summary>
/// Routes transactional notifications to email and/or SMS channels.
/// By default both channels are used; customers can opt out of either.
/// This is the single entry point for all notification dispatch in the application.
/// </summary>
public interface INotificationOrchestrator
{
    /// <summary>
    /// Sends a service request confirmation notification via all non-opted-out channels.
    /// </summary>
    /// <param name="smsOptOut">When <c>true</c>, skip SMS channel.</param>
    /// <param name="emailOptOut">When <c>true</c>, skip email channel.</param>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="serviceRequestId">Identifier of the confirmed service request.</param>
    /// <param name="dealershipName">Display name of the dealership for message context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendServiceRequestConfirmationAsync(
        bool smsOptOut,
        bool emailOptOut,
        string? toEmail,
        string? toPhoneNumber,
        string serviceRequestId,
        string dealershipName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a status change notification via all non-opted-out channels.
    /// </summary>
    /// <param name="smsOptOut">When <c>true</c>, skip SMS channel.</param>
    /// <param name="emailOptOut">When <c>true</c>, skip email channel.</param>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="serviceRequestId">Identifier of the service request.</param>
    /// <param name="newStatus">The new status of the service request.</param>
    /// <param name="dealershipName">Display name of the dealership.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendStatusChangeAsync(
        bool smsOptOut,
        bool emailOptOut,
        string? toEmail,
        string? toPhoneNumber,
        string serviceRequestId,
        string newStatus,
        string dealershipName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a magic link via all non-opted-out channels.
    /// </summary>
    /// <param name="smsOptOut">When <c>true</c>, skip SMS channel.</param>
    /// <param name="emailOptOut">When <c>true</c>, skip email channel.</param>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="magicLinkUrl">The full magic link URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendMagicLinkAsync(
        bool smsOptOut,
        bool emailOptOut,
        string? toEmail,
        string? toPhoneNumber,
        string magicLinkUrl,
        CancellationToken cancellationToken = default);
}
