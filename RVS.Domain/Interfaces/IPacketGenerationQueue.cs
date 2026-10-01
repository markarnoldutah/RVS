using RVS.Domain.Packets;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Dispatch seam between the code that requests a packet (intake submission, manager
/// regeneration) and the background worker that renders it (issue #434).
///
/// The default implementation is an in-process channel; this interface is the single point a
/// durable transport (e.g. Azure Storage Queue) would be swapped in without touching callers.
/// <see cref="TryEnqueue"/> must never block the caller — the intake path returns <c>201</c>
/// without waiting on the packet (<c>Spec A-8</c>, <c>B-1</c>, <c>X-7</c>).
/// </summary>
public interface IPacketGenerationQueue
{
    /// <summary>
    /// Enqueues a generation job without blocking. Returns <c>false</c> if the job could not be
    /// accepted (e.g. the queue is saturated); the caller should log and rely on the request's
    /// persisted <c>Pending</c> state plus on-demand regeneration for recovery.
    /// </summary>
    bool TryEnqueue(PacketGenerationJob job);

    /// <summary>
    /// Yields queued jobs until <paramref name="cancellationToken"/> is signalled. Intended for a
    /// single background consumer.
    /// </summary>
    IAsyncEnumerable<PacketGenerationJob> DequeueAllAsync(CancellationToken cancellationToken);
}
