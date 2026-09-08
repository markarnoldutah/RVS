namespace RVS.Domain.Interfaces;

/// <summary>
/// Outcome of a single <see cref="IPacketGenerationService.GenerateAsync"/> call, telling the
/// caller whether to re-enqueue the job.
/// </summary>
public enum PacketGenerationOutcome
{
    /// <summary>The packet was composed, rendered, and stored.</summary>
    Succeeded,

    /// <summary>This attempt failed but attempts remain — the job should be re-enqueued.</summary>
    Retry,

    /// <summary>This attempt failed and <see cref="Entities.PacketGenerationEmbedded.MaxAttempts"/> is reached — an alert was raised; do not re-enqueue.</summary>
    Exhausted,
}

/// <summary>
/// Composes and renders the one-page service packet for a request, then stores the PDF
/// (<c>Spec B-1</c> … <c>B-3</c>, issue #434). Runs off the intake request thread; a failure
/// here never rolls back the service request.
/// </summary>
public interface IPacketGenerationService
{
    /// <summary>
    /// Runs one generation attempt for the given request: loads it and its location, resolves
    /// photo URLs, composes the <see cref="Packets.ServicePacket"/>, renders HTML and PDF, uploads
    /// the PDF, and updates the request's <see cref="Entities.PacketGenerationEmbedded"/> state.
    /// Never throws for a generation failure — it records the failure on the request and returns
    /// <see cref="PacketGenerationOutcome.Retry"/> or <see cref="PacketGenerationOutcome.Exhausted"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> or <paramref name="serviceRequestId"/> is null/whitespace.</exception>
    /// <exception cref="KeyNotFoundException">No service request exists with that id in the tenant.</exception>
    Task<PacketGenerationOutcome> GenerateAsync(string tenantId, string serviceRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the request's packet-generation state to <c>Pending</c> and enqueues a fresh
    /// generation job. Used by the manager app's on-demand regenerate action.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> or <paramref name="serviceRequestId"/> is null/whitespace.</exception>
    /// <exception cref="KeyNotFoundException">No service request exists with that id in the tenant.</exception>
    Task RequestRegenerationAsync(string tenantId, string serviceRequestId, CancellationToken cancellationToken = default);
}
