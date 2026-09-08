using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Packets;

namespace RVS.API.Packets;

/// <summary>
/// Mints per-request read SAS URLs for a service request's photo attachments so the packet
/// renderers can reference them as <c>&lt;img src&gt;</c> — never base64 (<c>Spec B-3</c>,
/// <c>X-6</c>, issue <c>#433</c>).
///
/// The URLs are generated on every call and returned to the caller only; nothing is written
/// back to the <see cref="ServiceRequest"/> or to storage, so a SAS token is never persisted.
///
/// <para><b>Lifetime.</b> The HTML packet is delivered by email (<c>Spec B-4</c>) and may sit
/// unopened in a shop inbox over a weekend or a holiday. The staff-view read SAS default
/// (1 hour) would leave a service advisor looking at broken thumbnails, so packet photo URLs
/// are minted for <see cref="PhotoSasLifetime"/> — seven days, the maximum a user delegation
/// key can sign. That is still genuinely time-limited (<c>Spec X-6</c>) and bounds exposure
/// if the mail is forwarded; a regenerated packet mints fresh URLs. The PDF attachment
/// (<c>#432</c>) embeds photo bytes and is unaffected by this lifetime.</para>
/// </summary>
public sealed class PacketPhotoUrlResolver : IPacketPhotoUrlResolver
{
    /// <summary>
    /// How long a packet photo SAS URL stays valid. Seven days is Azure's ceiling for a
    /// user-delegation-key-signed SAS and comfortably outlasts realistic email-open latency.
    /// </summary>
    internal static readonly TimeSpan PhotoSasLifetime = TimeSpan.FromDays(7);

    /// <summary>Blob container holding service-request attachments (mirrors <c>AttachmentService</c>).</summary>
    private const string ContainerName = "rvs-attachments";

    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<PacketPhotoUrlResolver> _logger;

    public PacketPhotoUrlResolver(IBlobStorageService blobStorage, ILogger<PacketPhotoUrlResolver> logger)
    {
        _blobStorage = blobStorage;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        ServiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var urls = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var attachment in request.Attachments)
        {
            if (attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(attachment.BlobUri))
            {
                _logger.LogWarning(
                    "Skipping image attachment {AttachmentId} on service request {ServiceRequestId}: no blob path.",
                    attachment.AttachmentId, request.Id);
                continue;
            }

            urls[attachment.AttachmentId] = await _blobStorage.GenerateReadSasUrlAsync(
                ContainerName, attachment.BlobUri, PhotoSasLifetime, cancellationToken);
        }

        return urls;
    }
}
