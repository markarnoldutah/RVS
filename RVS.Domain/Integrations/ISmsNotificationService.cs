namespace RVS.Domain.Integrations;

/// <summary>
/// Sends transactional SMS notifications via Azure Communication Services in production,
/// with a no-op implementation for local development.
/// </summary>
public interface ISmsNotificationService
{
    /// <summary>
    /// Sends a generic SMS message.
    /// </summary>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format (e.g., +18015551234).</param>
    /// <param name="message">SMS message body (max 160 characters per segment).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendSmsAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a magic link URL to a customer via SMS.
    /// </summary>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="magicLinkUrl">The full magic link URL for the customer status page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendMagicLinkSmsAsync(string toPhoneNumber, string magicLinkUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an SMS confirmation for a newly submitted service request.
    /// </summary>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="serviceRequestId">Identifier of the confirmed service request.</param>
    /// <param name="dealershipName">Display name of the dealership for message context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendServiceRequestConfirmationSmsAsync(
        string toPhoneNumber, string serviceRequestId, string dealershipName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an SMS notification when a service request status changes.
    /// </summary>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="serviceRequestId">Identifier of the service request.</param>
    /// <param name="newStatus">The new status of the service request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendStatusChangeSmsAsync(
        string toPhoneNumber, string serviceRequestId, string newStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a dealer-to-customer message via SMS linked to a service request.
    /// </summary>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="serviceRequestId">Identifier of the related service request.</param>
    /// <param name="dealershipName">Display name of the dealership.</param>
    /// <param name="messageText">The dealer's message text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendDealerMessageSmsAsync(
        string toPhoneNumber, string serviceRequestId, string dealershipName,
        string messageText, CancellationToken cancellationToken = default);
}
