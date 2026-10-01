using System.ComponentModel.DataAnnotations;

namespace RVS.API.Options;

/// <summary>
/// Configuration for the inbound Event Grid webhook (issue #665), bound from
/// <c>EventGrid:Inbound</c>. Event Grid cannot present a bearer token to an anonymous endpoint,
/// so the subscription carries a shared secret in its endpoint URL and the endpoint checks it.
/// </summary>
public sealed class EventGridInboundOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "EventGrid:Inbound";

    /// <summary>
    /// The shared secret the Event Grid subscription must present as the <c>key</c> query
    /// parameter. Injected from Key Vault in staging and production. When it is absent the
    /// endpoint refuses every request: an unauthenticated webhook that writes opt-outs is worse
    /// than one that is switched off.
    /// </summary>
    [MinLength(32, ErrorMessage = "EventGrid:Inbound:Key must be at least 32 characters.")]
    public string? Key { get; init; }

    /// <summary>Whether the endpoint has a secret to check against.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Key);
}
