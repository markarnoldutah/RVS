namespace RVS.Domain.Integrations;

/// <summary>
/// Caps outbound SMS per tenant per rolling hour at
/// <c>AzureCommunicationServices:Sms:MaxMessagesPerTenantPerHour</c> (issue #661), so a bug or
/// an abusive caller cannot run up a tenant's carrier bill or the shared number's reputation.
/// </summary>
public interface ITenantSmsRateLimiter
{
    /// <summary>
    /// Takes one send from <paramref name="tenantId"/>'s hourly allowance. Returns <c>false</c>,
    /// and takes nothing, when the allowance is spent.
    /// </summary>
    /// <param name="tenantId">Tenant the message is sent for.</param>
    bool TryAcquire(string tenantId);
}
