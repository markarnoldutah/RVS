using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Append-only store of <see cref="IntakeFormStart"/> records (<c>Spec A-13</c>, issue #839),
/// partitioned by location. Azure Table Storage for the same reasons as
/// <see cref="IIntakeRedirectHitRepository"/>: a write on every visit, read once a month.
///
/// There is no update, no delete and no application read path — completion rate is read by hand
/// from the saved pilot-metrics queries.
/// </summary>
public interface IIntakeFormStartRepository
{
    /// <summary>
    /// Appends one start. May throw on a storage failure; the caller swallows it, because the
    /// form never waits on this.
    /// </summary>
    /// <param name="start">The start to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendAsync(IntakeFormStart start, CancellationToken cancellationToken = default);
}
