using Azure;
using Azure.Data.Tables;

namespace RVS.Infra.AzTableRepository;

/// <summary>
/// Table Storage row shape for one <c>go.rvintake.com</c> redirect hit (<c>Spec A-13</c>,
/// issue #599).
///
/// <b>PartitionKey</b> is the location id, so every read is a single-partition query — the same
/// discipline the Cosmos repositories keep, for the same reason.
///
/// <b>RowKey</b> is <c>{inverted ticks}-{short guid}</c>. Table Storage sorts row keys
/// ascending as strings, so inverting the tick count puts the newest hit first and makes "the
/// last N days" a prefix range rather than a scan. The guid suffix keeps two hits recorded in
/// the same tick from colliding — with link-preview fetchers firing in bursts, that is a real
/// case, not a theoretical one.
/// </summary>
internal sealed class IntakeRedirectHitTableEntity : ITableEntity
{
    /// <summary>Location id — the partition. <c>unresolved</c> when the slug did not resolve.</summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>Inverted-tick key: newest first, unique per hit.</summary>
    public string RowKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <summary>Tenant owning the location. Null when the slug did not resolve.</summary>
    public string? TenantId { get; set; }

    /// <summary>The slug as requested, lower-cased.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Normalised channel tag — never blank; <c>print</c> when no <c>src</c> was supplied.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// When the redirect was served, as recorded by the application. Kept as its own column
    /// rather than relying on <see cref="Timestamp"/>, which the service sets on write and which
    /// would drift from the application's clock under retry.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Whether the User-Agent looked like a link-preview fetcher or crawler.</summary>
    public bool IsLikelyBot { get; set; }

    /// <summary>Truncated client User-Agent. No IP, no cookie, no token — this store holds no customer identity.</summary>
    public string? UserAgent { get; set; }
}
