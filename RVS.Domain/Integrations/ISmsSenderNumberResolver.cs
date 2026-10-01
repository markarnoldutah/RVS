namespace RVS.Domain.Integrations;

/// <summary>
/// Resolves the ACS number an SMS is sent from, keyed by location (issue #661). Today every
/// location resolves to the one shared toll-free number in
/// <c>AzureCommunicationServices:Sms:FromPhoneNumber</c>; the seam exists so a location can get
/// its own number later without a schema change or a change to any caller.
/// </summary>
public interface ISmsSenderNumberResolver
{
    /// <summary>
    /// Returns the E.164 sending number for <paramref name="locationId"/>, or <c>null</c> when
    /// the location has no number to send from, in which case the send must be skipped.
    /// </summary>
    /// <param name="tenantId">Tenant that owns the location.</param>
    /// <param name="locationId">Location the message is sent for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> ResolveAsync(string tenantId, string locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The environment's shared sending number, for a send that has no location to resolve from
    /// — today only the fixed HELP reply (issue #665), which answers an inbound text carrying a
    /// phone number and nothing else. When locations get their own numbers this stays the shared
    /// one: an inbound keyword still cannot say which dealer it meant.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sending number in E.164, or <c>null</c> when none is configured.</returns>
    Task<string?> ResolveDefaultAsync(CancellationToken cancellationToken = default);
}
