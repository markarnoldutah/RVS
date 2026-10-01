namespace RVS.Domain.Packets;

/// <summary>
/// A unit of work for the packet generation pipeline (issue #434): compose and render the
/// service packet for one <see cref="Entities.ServiceRequest"/>, then store the PDF.
///
/// Enqueued on intake submission and on an on-demand manager regeneration. Carries only
/// identifiers — the worker loads the request fresh so it always renders current data.
/// </summary>
/// <param name="TenantId">Partition key of the service request.</param>
/// <param name="ServiceRequestId">Identifier of the service request to generate a packet for.</param>
/// <param name="Trigger">
/// What caused the job — <c>"intake"</c> or <c>"regeneration"</c>. Used for logging only.
/// </param>
public sealed record PacketGenerationJob(string TenantId, string ServiceRequestId, string Trigger);
