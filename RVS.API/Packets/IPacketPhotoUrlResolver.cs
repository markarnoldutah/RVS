using RVS.Domain.Entities;
using RVS.Domain.Packets;

namespace RVS.API.Packets;

/// <summary>
/// Resolves a <see cref="ServiceRequest"/>'s image attachments to time-limited read SAS
/// URLs for <see cref="PacketCompositionContext.PhotoUrls"/> (<c>Spec B-2</c> item 8,
/// <c>B-3</c>, <c>X-6</c>, issue <c>#433</c>).
///
/// URLs are minted on every call and never persisted. The packet generation orchestrator
/// (issue <c>#434</c>) calls this while assembling the <see cref="PacketCompositionContext"/>.
/// </summary>
public interface IPacketPhotoUrlResolver
{
    /// <summary>
    /// Returns a read SAS URL for each image attachment on <paramref name="request"/>, keyed
    /// by <see cref="ServiceRequestAttachmentEmbedded.AttachmentId"/>. Non-image attachments,
    /// and image attachments with no stored blob path, are omitted.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        ServiceRequest request, CancellationToken cancellationToken = default);
}
