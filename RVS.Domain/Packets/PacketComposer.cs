using RVS.Domain.Entities;
using RVS.Domain.Validation;

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

        var photos = ComposePhotos(request.Attachments, context.PhotoUrls);

        return new ServicePacket
        {
            Unit = ComposeUnit(request.AssetInfo),
            Customer = ComposeCustomer(request.CustomerSnapshot),
            Origin = new PacketOrigin
            {
                LocationName = NullIfBlank(context.LocationName),
                LocationPhone = NullIfBlank(context.LocationPhone),
                LocationTimeZoneId = NullIfBlank(context.LocationTimeZoneId),
                SubmittedAtUtc = context.SubmittedAtUtc,
                ReferenceCode = DeriveReferenceCode(request.Id),
            },
            IssueCategory = NullIfBlank(request.IssueCategory),
            CuratedIssue = ComposeCuratedIssue(request),
            AiSummary = ComposeAiSummary(request, photos),
            IssueDescription = ComposeComplaint(request),
            Diagnostics = ComposeDiagnostics(request.DiagnosticResponses),
            Photos = [.. photos.Select(p => p.Photo)],
            PasteBlock = NullIfBlank(context.PasteBlock),
            StatusLink = NullIfBlank(context.StatusLinkUrl) is { } url
                ? new PacketStatusLink { Url = url }
                : null,
            ManagerLinks = context.ManagerLinks,
            Branding = ComposeBranding(context),
        };
    }

    private static PacketBranding ComposeBranding(PacketCompositionContext context)
    {
        var brandName = NullIfBlank(context.BrandName)?.Trim();
        var logoDataUri = NullIfBlank(context.LogoDataUri)?.Trim();

        if (brandName is null && logoDataUri is null)
        {
            return PacketBranding.Default;
        }

        return new PacketBranding
        {
            BrandName = brandName ?? PacketBranding.Default.BrandName,
            LogoDataUri = logoDataUri,
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
        FirstName = NullIfBlank(snapshot.FirstName),
        LastName = NullIfBlank(snapshot.LastName),
        Phone = NullIfBlank(snapshot.Phone),
        Email = NullIfBlank(snapshot.Email),
        PreferredContact = NullIfBlank(snapshot.PreferredContact),
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

    /// <summary>
    /// The "Complaint — word for word" text: the words curation started from when they were
    /// recorded, otherwise the submitted description. Never trimmed — this block is the one
    /// place in the packet that reproduces the customer exactly.
    /// </summary>
    private static string ComposeComplaint(ServiceRequest request) =>
        NullIfBlank(request.IssueDescriptionVerbatim) ?? request.IssueDescription ?? string.Empty;

    /// <summary>
    /// The "Issue" text: the submitted (curated) description, but only when it actually differs
    /// from the verbatim complaint rendered below it. Whitespace alone is not a difference, and
    /// a request with no verbatim text has nothing to curate against — both cases return
    /// <c>null</c> so the packet never shows one complaint under two headings (issue #601).
    /// </summary>
    private static string? ComposeCuratedIssue(ServiceRequest request)
    {
        var verbatim = NullIfBlank(request.IssueDescriptionVerbatim)?.Trim();
        var curated = NullIfBlank(request.IssueDescription)?.Trim();

        if (verbatim is null || curated is null || verbatim == curated)
        {
            return null;
        }

        return curated;
    }

    private static PacketAiSummary? ComposeAiSummary(
        ServiceRequest request, IReadOnlyList<(string AttachmentId, PacketPhoto Photo)> photos)
    {
        var assessment = request.PreliminaryAssessment;
        var text = NullIfBlank(request.TechnicianSummary)?.Trim();
        var confidence = AssessmentConfidence.DisplayName(assessment?.Confidence);

        var summary = new PacketAiSummary
        {
            Text = text,
            PhotoFindings = ComposePhotoFindings(assessment?.PhotoFindings, request.Attachments, photos),
        };

        if (assessment is not null && confidence is not null)
        {
            summary = summary with
            {
                ProbableCause = NullIfBlank(assessment.ProbableCause)?.Trim(),
                PossibleFixes = TrimNonBlank(assessment.PossibleFixes),
                LikelyParts = TrimNonBlank(assessment.LikelyParts),
            };

            if (summary.HasStructuredAssessment)
            {
                summary = summary with { Confidence = confidence };
            }
        }

        return text is null && !summary.HasStructuredAssessment && !summary.HasPhotoFindings ? null : summary;
    }

    /// <summary>
    /// The <c>From photos</c> lines (issue #772): data plates, fault codes, then observations,
    /// each cited by the photo's position in the packet's photo list and its file name. A finding
    /// whose attachment has since left the request is dropped rather than cited to nothing.
    /// </summary>
    private static IReadOnlyList<PacketPhotoFinding> ComposePhotoFindings(
        PhotoFindingsEmbedded? findings,
        IEnumerable<ServiceRequestAttachmentEmbedded> attachments,
        IReadOnlyList<(string AttachmentId, PacketPhoto Photo)> photos)
    {
        if (findings is null)
        {
            return [];
        }

        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attachment in attachments)
        {
            labels.TryAdd(attachment.AttachmentId, NullIfBlank(attachment.FileName) ?? "photo");
        }

        for (var i = 0; i < photos.Count; i++)
        {
            var fileName = NullIfBlank(photos[i].Photo.FileName);
            labels[photos[i].AttachmentId] = fileName is null ? $"photo {i + 1}" : $"photo {i + 1}, {fileName}";
        }

        var lines = new List<PacketPhotoFinding>();
        void Add(string text, string attachmentId)
        {
            if (labels.TryGetValue(attachmentId, out var label) && !string.IsNullOrWhiteSpace(text))
            {
                lines.Add(new PacketPhotoFinding { Text = text, PhotoLabel = label });
            }
        }

        foreach (var plate in findings.DataPlates)
        {
            Add(PhotoFindingText.DataPlate(plate), plate.AttachmentId);
        }

        foreach (var fault in findings.FaultCodes)
        {
            Add(PhotoFindingText.FaultCode(fault), fault.AttachmentId);
        }

        foreach (var observation in findings.Observations)
        {
            Add(observation.Text.Trim(), observation.AttachmentId);
        }

        return lines;
    }

    private static IReadOnlyList<string> TrimNonBlank(IEnumerable<string>? values) =>
        [.. (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim())];

    private static IReadOnlyList<(string AttachmentId, PacketPhoto Photo)> ComposePhotos(
        IEnumerable<ServiceRequestAttachmentEmbedded> attachments,
        IReadOnlyDictionary<string, string> photoUrls)
    {
        var photos = new List<(string AttachmentId, PacketPhoto Photo)>();

        foreach (var attachment in attachments)
        {
            var isImage = attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
            var isVideo = attachment.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true;
            if (!isImage && !isVideo)
            {
                continue;
            }

            if (!photoUrls.TryGetValue(attachment.AttachmentId, out var url) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            photos.Add((attachment.AttachmentId, new PacketPhoto
            {
                Url = url,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
            }));
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
