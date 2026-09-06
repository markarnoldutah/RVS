using RVS.Domain.Entities;

namespace RVS.Domain.Packets;

/// <summary>
/// Assembles a <see cref="ServicePacket"/> from a <see cref="ServiceRequest"/> and a
/// <see cref="PacketCompositionContext"/>, in the order and with the degradation rules of
/// <c>Spec B-2</c>.
///
/// This is a pure transform: it reads only its two arguments and returns a new
/// <see cref="ServicePacket"/>. It performs no I/O, mints no tokens, generates no SAS
/// URLs, and knows nothing about HTML or PDF. Pricing, quotes, labor rates, service-event
/// data, and any other customer's data are simply never read, so they cannot appear.
/// </summary>
public static class PacketComposer
{
    /// <summary>
    /// Builds the packet composition model for <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The service request to compose a packet for.</param>
    /// <param name="context">Content resolved by the caller that is not on the request.</param>
    public static ServicePacket Compose(ServiceRequest request, PacketCompositionContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return new ServicePacket
        {
            Unit = ComposeUnit(request.AssetInfo),
            Customer = ComposeCustomer(request.CustomerSnapshot),
            Origin = new PacketOrigin
            {
                LocationName = NullIfBlank(context.LocationName),
                LocationPhone = NullIfBlank(context.LocationPhone),
                SubmittedAtUtc = context.SubmittedAtUtc,
                ReferenceCode = DeriveReferenceCode(request.Id),
            },
            IssueCategory = NullIfBlank(request.IssueCategory),
            IssueDescription = request.IssueDescription ?? string.Empty,
            Diagnostics = ComposeDiagnostics(request.DiagnosticResponses),
            AiSummary = ComposeAiSummary(request.TechnicianSummary),
            Photos = ComposePhotos(request.Attachments, context.PhotoUrls),
            PasteBlock = NullIfBlank(context.PasteBlock),
            StatusLink = NullIfBlank(context.StatusLinkUrl) is { } url
                ? new PacketStatusLink { Url = url }
                : null,
        };
    }

    private static PacketUnitHeader ComposeUnit(AssetInfoEmbedded asset) => new()
    {
        Year = asset.Year,
        Make = NullIfBlank(asset.Manufacturer),
        Model = NullIfBlank(asset.Model),
        Vin = NullIfBlank(asset.AssetId),
    };

    private static PacketCustomer ComposeCustomer(CustomerSnapshotEmbedded snapshot) => new()
    {
        FullName = $"{snapshot.FirstName} {snapshot.LastName}".Trim(),
        Phone = NullIfBlank(snapshot.Phone),
        Email = NullIfBlank(snapshot.Email),
        PreferredContact = null,
    };

    private static IReadOnlyList<PacketDiagnosticEntry> ComposeDiagnostics(
        IEnumerable<DiagnosticResponseEmbedded> responses)
    {
        var entries = new List<PacketDiagnosticEntry>();

        foreach (var response in responses)
        {
            if (string.IsNullOrWhiteSpace(response.QuestionText))
            {
                continue;
            }

            var answers = new List<string>(response.SelectedOptions ?? []);
            if (!string.IsNullOrWhiteSpace(response.FreeTextResponse))
            {
                answers.Add(response.FreeTextResponse);
            }

            entries.Add(new PacketDiagnosticEntry
            {
                Question = response.QuestionText,
                Answers = answers,
            });
        }

        return entries;
    }

    private static PacketAiSummary? ComposeAiSummary(string? technicianSummary) =>
        NullIfBlank(technicianSummary) is { } text
            ? new PacketAiSummary { Text = text.Trim() }
            : null;

    private static IReadOnlyList<PacketPhoto> ComposePhotos(
        IEnumerable<ServiceRequestAttachmentEmbedded> attachments,
        IReadOnlyDictionary<string, string> photoUrls)
    {
        var photos = new List<PacketPhoto>();

        foreach (var attachment in attachments)
        {
            var isImage = attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
            if (!isImage)
            {
                continue;
            }

            if (!photoUrls.TryGetValue(attachment.AttachmentId, out var url) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            photos.Add(new PacketPhoto
            {
                Url = url,
                FileName = attachment.FileName,
            });
        }

        return photos;
    }

    /// <summary>
    /// Short reference code: the first segment of the service request id, upper-cased
    /// (e.g. <c>a1b2c3d4-...</c> becomes <c>A1B2C3D4</c>). Deterministic and stable across
    /// regenerations. Falls back to the whole id when it contains no <c>-</c>.
    /// </summary>
    private static string DeriveReferenceCode(string id)
    {
        var segment = id.Split('-', 2)[0];
        return segment.ToUpperInvariant();
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
