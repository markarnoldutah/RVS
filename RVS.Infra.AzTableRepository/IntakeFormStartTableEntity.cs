using Azure;
using Azure.Data.Tables;

namespace RVS.Infra.AzTableRepository;

/// <summary>
/// Table Storage row shape for one intake form start (<c>Spec A-13</c>, issue #839).
///
/// Keyed exactly like <see cref="IntakeRedirectHitTableEntity"/>: <b>PartitionKey</b> is the
/// location id, so a month's read is a single-partition query; <b>RowKey</b> is
/// <c>{inverted ticks}-{short guid}</c>, newest first and unique per start.
/// </summary>
internal sealed class IntakeFormStartTableEntity : ITableEntity
{
    /// <summary>Location id — the partition.</summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>Inverted-tick key: newest first, unique per start.</summary>
    public string RowKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <summary>Tenant owning the location.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>The slug as requested, lower-cased.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Normalised channel tag — never blank; <c>print</c> when no <c>src</c> was supplied.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>The intake app's per-tab visit id. Completion rate counts distinct values.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>When the start was recorded, by the application's clock rather than <see cref="Timestamp"/>.</summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Whether the User-Agent looked like a crawler.</summary>
    public bool IsLikelyBot { get; set; }

    /// <summary>Truncated client User-Agent. No IP, no cookie, no token.</summary>
    public string? UserAgent { get; set; }
}
