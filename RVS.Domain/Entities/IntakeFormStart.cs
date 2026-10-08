namespace RVS.Domain.Entities;

/// <summary>
/// One visit reaching the first screen of a location's intake form (<c>Spec A-13</c>,
/// issue #839) — the denominator of the completion rate. Submissions are already in Cosmos;
/// a start that is not recorded when it happens cannot be added later.
///
/// Same shape and the same reasoning as <see cref="IntakeRedirectHit"/>: deliberately **not** an
/// <see cref="EntityBase"/> and not in Cosmos, but an append-only Azure Table Storage row
/// partitioned by location. Only resolved slugs are recorded, so there is no unresolved
/// partition here.
///
/// Carries no customer identity. <see cref="SessionId"/> is a random per-tab value the intake
/// app generates, so one visit — refreshes and Back into Step 1 included — counts once.
/// </summary>
public sealed record IntakeFormStart
{
    /// <summary>The location the slug resolved to — the partition key.</summary>
    public required string LocationId { get; init; }

    /// <summary>Tenant owning the location.</summary>
    public required string TenantId { get; init; }

    /// <summary>The slug as requested, lower-cased.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Channel tag, already through <see cref="Validation.IntakeSourceVocabulary.Normalize"/>, so
    /// completion rate splits by channel the same way A-13's redirect hits do.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// The intake app's per-tab visit id. Completion rate counts distinct values, so a client
    /// that does fire twice for one visit still counts once.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>UTC time the start was recorded.</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// Whether the User-Agent looked like a crawler rather than a person
    /// (<see cref="Validation.BotUserAgentFilter"/>). Flagged, never dropped: reading does the
    /// excluding.
    /// </summary>
    public required bool IsLikelyBot { get; init; }

    /// <summary>Client User-Agent, truncated to <see cref="IntakeRedirectHit.MaxUserAgentLength"/>. Null when absent.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Longest session id accepted. The intake app sends a 32-character guid.</summary>
    public const int MaxSessionIdLength = 64;

    /// <summary>Shortest session id accepted — anything shorter is not one the intake app made.</summary>
    public const int MinSessionIdLength = 8;

    /// <summary>
    /// Whether <paramref name="sessionId"/> looks like a value the intake app generated: 8–64
    /// letters, digits, hyphens or underscores. Anything else is not recorded.
    /// </summary>
    /// <param name="sessionId">The client-supplied session id.</param>
    public static bool IsWellFormedSessionId(string? sessionId)
    {
        if (sessionId is null || sessionId.Length is < MinSessionIdLength or > MaxSessionIdLength)
        {
            return false;
        }

        foreach (var c in sessionId)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
