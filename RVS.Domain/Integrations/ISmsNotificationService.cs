namespace RVS.Domain.Integrations;

/// <summary>
/// Sends transactional SMS notifications via Azure Communication Services, with a no-op
/// implementation registered whenever <c>AzureCommunicationServices:Sms:Enabled</c> is off.
/// </summary>
public interface ISmsNotificationService
{
    /// <summary>
    /// Whether this service actually sends. <c>false</c> for the no-op implementation and while
    /// <c>AzureCommunicationServices:Sms:Enabled</c> is off; <see cref="INotificationOrchestrator"/>
    /// then routes a <c>Text</c> customer's confirmation to email (<c>Spec A-2</c>, issue #662).
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sends an SMS on behalf of a location. The sending number is resolved per location
    /// (<see cref="ISmsSenderNumberResolver"/>), the send counts against the tenant's hourly
    /// limit (<see cref="ITenantSmsRateLimiter"/>), and the recipient is normalised to E.164
    /// before it reaches ACS. A number that cannot be normalised is not sent to.
    /// </summary>
    /// <param name="tenantId">Tenant the message is sent for; keys the hourly limit.</param>
    /// <param name="locationId">Location the message is sent for; keys the sending number.</param>
    /// <param name="toPhoneNumber">Recipient phone number, ideally already E.164 (e.g., +18015551234).</param>
    /// <param name="message">SMS message body (max 160 characters per segment).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The ACS message id when ACS accepted the message, which is what a delivery report is
    /// matched back by (issue #663); <c>null</c> when nothing was sent or ACS rejected it.
    /// Never throws for a failed send.
    /// </returns>
    Task<string?> SendSmsAsync(
        string tenantId, string locationId, string toPhoneNumber, string message,
        CancellationToken cancellationToken = default);
}
