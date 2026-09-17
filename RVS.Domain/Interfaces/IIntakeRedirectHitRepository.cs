using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Append-only store of <see cref="IntakeRedirectHit"/> records (<c>Spec A-13</c>, issue #599),
/// partitioned by location. Backed by Azure Table Storage rather than Cosmos: this is a
/// high-volume write path that is read occasionally, and Cosmos would charge request units on
/// every machine-made link-preview fetch.
///
/// There is no update and no delete. Retention is a storage-lifecycle concern, not an
/// application one.
/// </summary>
public interface IIntakeRedirectHitRepository
{
    /// <summary>
    /// Appends one hit. Implementations must never throw into the redirect path — the redirect
    /// is the customer's journey and logging is telemetry; a storage failure costs the hit, not
    /// the redirect.
    /// </summary>
    /// <param name="hit">The hit to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendAsync(IntakeRedirectHit hit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the hits recorded for one location within a UTC window, newest first.
    /// </summary>
    /// <param name="locationId">Location partition to read.</param>
    /// <param name="fromUtc">Inclusive start of the window. <c>null</c> for no lower bound.</param>
    /// <param name="toUtc">Exclusive end of the window. <c>null</c> for no upper bound.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<IntakeRedirectHit>> QueryAsync(
        string locationId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default);
}
