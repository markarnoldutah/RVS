namespace RVS.Domain.Entities;

/// <summary>
/// One request that passed through the <c>go.rvintake.com</c> redirect on its way to a
/// location's intake form (<c>Spec A-13</c>, issue #599).
///
/// Deliberately **not** an <see cref="EntityBase"/> and deliberately not in Cosmos. Hits and
/// service requests are different events with different volumes and lifecycles — most hits
/// never convert, and the write path is high-volume while the read path is occasional — so
/// hits live in an append-only Azure Table Storage table partitioned by location, where a
/// write costs storage rather than Cosmos request units. The
/// <see cref="ServiceRequest.IntakeSource"/> on the resulting request stays the authoritative
/// source-of-job record; this store exists so a conversion rate has a denominator.
///
/// Carries no customer identity: no IP address, no cookie, no token. The only client-supplied
/// value stored is a truncated User-Agent, kept so <see cref="IsLikelyBot"/> can be
/// re-evaluated later if the preview-fetcher landscape shifts.
/// </summary>
public sealed record IntakeRedirectHit
{
    /// <summary>
    /// The location the slug resolved to — the partition key. When the slug could not be
    /// resolved this is <see cref="UnresolvedLocationId"/>; the redirect still happens and the
    /// hit is still recorded.
    /// </summary>
    public required string LocationId { get; init; }

    /// <summary>Tenant owning the location, or <c>null</c> when the slug did not resolve.</summary>
    public string? TenantId { get; init; }

    /// <summary>The slug as requested, lower-cased. Kept even when it does not resolve.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Channel tag, already through <see cref="Validation.IntakeSourceVocabulary.Normalize"/> —
    /// so <c>print</c> when no <c>src</c> was supplied, never blank.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>UTC time the redirect was served.</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// Whether the User-Agent looked like a link-preview fetcher or crawler rather than a person
    /// (<see cref="Validation.BotUserAgentFilter"/>). Flagged, never dropped: the store is
    /// append-only and reporting does the excluding.
    /// </summary>
    public required bool IsLikelyBot { get; init; }

    /// <summary>Client User-Agent, truncated to <see cref="MaxUserAgentLength"/>. Null when absent.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Partition used when a slug does not resolve to a location.</summary>
    public const string UnresolvedLocationId = "unresolved";

    /// <summary>Stored User-Agent cap — enough to identify a client, short enough to stay cheap.</summary>
    public const int MaxUserAgentLength = 256;
}
