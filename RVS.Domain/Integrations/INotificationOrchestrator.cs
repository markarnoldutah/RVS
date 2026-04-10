namespace RVS.Domain.Integrations;

/// <summary>
/// Routes transactional notifications to the appropriate channel (email or SMS)
/// based on the customer's notification preference. This is the single entry point
/// for all notification dispatch in the application.
/// </summary>
public interface INotificationOrchestrator
{
    /// <summary>
    /// Sends a service request confirmation notification via the customer's preferred channel.
    /// Routes to email or SMS based on <paramref name="notificationPreference"/>.
    /// </summary>
    /// <param name="notificationPreference">Customer's chosen channel: "email" (default) or "sms".</param>
    /// <param name="toEmail">Recipient email address (used when preference is "email").</param>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format (used when preference is "sms").</param>
    /// <param name="serviceRequestId">Identifier of the confirmed service request.</param>
    /// <param name="dealershipName">Display name of the dealership for message context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendServiceRequestConfirmationAsync(
        string notificationPreference,
        string? toEmail,
        string? toPhoneNumber,
        string serviceRequestId,
        string dealershipName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a status change notification via the customer's preferred channel.
    /// </summary>
    /// <param name="notificationPreference">Customer's chosen channel: "email" or "sms".</param>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="serviceRequestId">Identifier of the service request.</param>
    /// <param name="newStatus">The new status of the service request.</param>
    /// <param name="dealershipName">Display name of the dealership.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendStatusChangeAsync(
        string notificationPreference,
        string? toEmail,
        string? toPhoneNumber,
        string serviceRequestId,
        string newStatus,
        string dealershipName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a magic link via the customer's preferred channel.
    /// </summary>
    /// <param name="notificationPreference">Customer's chosen channel: "email" or "sms".</param>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="magicLinkUrl">The full magic link URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendMagicLinkAsync(
        string notificationPreference,
        string? toEmail,
        string? toPhoneNumber,
        string magicLinkUrl,
        CancellationToken cancellationToken = default);
}
