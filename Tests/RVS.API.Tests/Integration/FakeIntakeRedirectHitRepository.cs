using System.Collections.Concurrent;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Integration;

/// <summary>
/// In-memory <see cref="IIntakeRedirectHitRepository"/> for the redirect integration tests
/// (<c>Spec A-13</c>, issue #599). Substituted for the Table Storage repository so the hit log
/// can be asserted on without a live storage account — and so the test host never reaches for a
/// credential to write telemetry.
/// </summary>
public sealed class FakeIntakeRedirectHitRepository : IIntakeRedirectHitRepository
{
    private readonly ConcurrentQueue<IntakeRedirectHit> _hits = new();

    /// <summary>Everything appended so far, oldest first.</summary>
    public IReadOnlyList<IntakeRedirectHit> Hits => [.. _hits];

    public Task AppendAsync(IntakeRedirectHit hit, CancellationToken cancellationToken = default)
    {
        _hits.Enqueue(hit);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IntakeRedirectHit>> QueryAsync(
        string locationId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<IntakeRedirectHit>>(
            [.. _hits.Where(h => h.LocationId == locationId)]);
}
