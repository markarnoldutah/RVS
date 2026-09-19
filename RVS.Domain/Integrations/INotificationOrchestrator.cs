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
    /// <param name="tenantId">Tenant the request was submitted to.</param>
    /// <param name="locationId">Location the request was submitted to; keys the SMS sending number.</param>
    /// <param name="smsOptOut">When <c>true</c>, skip SMS channel.</param>
    /// <param name="emailOptOut">When <c>true</c>, skip email channel.</param>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="serviceRequestId">Identifier of the confirmed service request.</param>
    /// <param name="dealershipName">Display name of the dealership for message context.</param>
    /// <param name="statusUrl">The full URL of the customer's status page.</param>
    /// <param name="dealerPhone">The dealer's contact phone number, when known.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendServiceRequestConfirmationAsync(
        string tenantId,
        string locationId,
        bool smsOptOut,
        bool emailOptOut,
        string? toEmail,
        string? toPhoneNumber,
        string serviceRequestId,
        string dealershipName,
        string statusUrl,
        string? dealerPhone,
        CancellationToken cancellationToken = default);
}
