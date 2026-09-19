namespace RVS.Domain.Integrations;

/// <summary>
/// Routes transactional notifications to the email or SMS channel.
/// This is the single entry point for all notification dispatch in the application.
/// </summary>
public interface INotificationOrchestrator
{
    /// <summary>
    /// Sends one service request confirmation, on the channel the customer's preferred contact
    /// method chooses, with the opt-outs as a hard veto (<c>Spec A-2</c>, issue #662):
    /// <list type="bullet">
    /// <item><c>Text</c>, with SMS enabled, a phone number and no SMS opt-out → SMS.</item>
    /// <item><c>Text</c>, but SMS unavailable → email, logged at Warning.</item>
    /// <item><c>Email</c>, <c>Phone</c> or <c>null</c> (requests from before the preference was captured) → email.</item>
    /// <item>Email opted out or no address → SMS if permitted; otherwise nothing, logged at Warning.</item>
    /// </list>
    /// </summary>
    /// <param name="tenantId">Tenant the request was submitted to.</param>
    /// <param name="locationId">Location the request was submitted to; keys the SMS sending number.</param>
    /// <param name="preferredContact">
    /// The customer's preferred contact method (<see cref="Validation.PreferredContactMethod"/>), or <c>null</c>.
    /// </param>
    /// <param name="smsOptOut">When <c>true</c>, never send by SMS.</param>
    /// <param name="emailOptOut">When <c>true</c>, never send by email.</param>
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
        string? preferredContact,
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
