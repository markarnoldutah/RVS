using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RVS.Domain.Packets;

namespace RVS.API.Packets;

/// <summary>
/// Renders a <see cref="ServicePacket"/> to a PDF byte array with QuestPDF — pure-managed
/// .NET, in-process, synchronous, no headless browser and no per-render network call
/// (<c>Spec B-3</c>, <c>B-7</c>, decision <c>#426</c>, issue <c>#432</c>).
///
/// Content and ordering come entirely from <see cref="PacketPdfLayout"/>, the same
/// degradation-resolved model shape the HTML renderer follows, so the two renderings
/// cannot diverge. This class only paints that model: page geometry, framing, and photo
/// placement.
///
/// Photos are supplied as bytes by the caller (keyed by <see cref="PacketPhoto.Url"/>);
/// resolving the time-limited SAS URLs to bytes is the orchestrator's job (issue
/// <c>#433</c>). A photo with no bytes — or bytes in a format QuestPDF's decoder cannot
/// read, such as iPhone HEIC/HEIF (issue <c>#492</c> item 8) — renders as a labelled
/// placeholder cell so the packet still lays out correctly.
/// </summary>
public static class PacketPdfRenderer
{
    /// <summary>Page box: the intersection of ISO A4 (210 x 297 mm) and US Letter
    /// (216 x 279 mm). A PDF has one fixed media box and cannot defer the paper choice to
    /// the printer the way the HTML <c>@page</c> rule does, so the content is sized to fit
    /// inside the margins of either sheet (<c>Spec B-3</c>).</summary>
    private const float PageWidthMm = 210f;

    private const float PageHeightMm = 279f;

    private const float PageMarginMm = 14f;

    /// <summary>Sections 1–3 (<c>Spec B-2</c>) are painted together as the IDS-style
    /// masthead — a letterhead + top-right tracking number, then a three-column
    /// Customer / Location / Unit band — rather than as three stacked blocks.</summary>
    private static readonly string[] MastheadSectionIds = ["unit", "customer", "origin"];

    private static readonly IReadOnlyDictionary<string, byte[]> NoImages =
        new Dictionary<string, byte[]>();

    static PacketPdfRenderer()
    {
        // QuestPDF Community License — RVS qualifies today (Spec B-7 / issue #426).
        // Setting it here keeps the renderer self-contained for tests; the application
        // composition root may also set it, which is harmless (same value).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Renders <paramref name="packet"/> to a PDF document.
    /// </summary>
    /// <param name="packet">The composed packet.</param>
    /// <param name="photoImages">
    /// Image bytes keyed by <see cref="PacketPhoto.Url"/>. Photos with no entry render as a
    /// labelled placeholder. Pass <c>null</c> to render every photo as a placeholder.
    /// </param>
    public static byte[] Render(ServicePacket packet, IReadOnlyDictionary<string, byte[]>? photoImages = null)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var layout = PacketPdfLayout.Build(packet);
        var images = photoImages ?? NoImages;
        var submitted = packet.Origin.SubmittedAtUtc.UtcDateTime;

        var masthead = MastheadSectionIds
            .Select(id => layout.Sections.First(s => s.Id == id))
            .ToArray();
        var bodySections = layout.Sections.Where(s => !MastheadSectionIds.Contains(s.Id));

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageWidthMm, PageHeightMm, Unit.Millimetre);
                    page.Margin(PageMarginMm, Unit.Millimetre);
                    page.DefaultTextStyle(text => text.FontSize(10.5f).FontColor(Colors.Black));

                    page.Content().Column(column =>
                    {
                        column.Spacing(18f);

                        column.Item().Element(e => RenderMasthead(
                            e, packet, layout, unit: masthead[0], customer: masthead[1], origin: masthead[2]));

                        foreach (var section in bodySections)
                        {
                            RenderSection(column, section, images);
                        }
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Service Packet {packet.Origin.ReferenceCode}",
                Author = packet.Branding.BrandName,
                Subject = "RV service intake packet",
                // Pinned to the packet so the same packet renders byte-for-byte identically.
                CreationDate = submitted,
                ModifiedDate = submitted,
            })
            .GeneratePdf();
    }

    // ── Masthead: sections 1–3 as an IDS-style band ──────────────────────

    private static void RenderMasthead(
        IContainer container,
        ServicePacket packet,
        PacketPdfLayout layout,
        PacketPdfLayoutSection unit,
        PacketPdfLayoutSection customer,
        PacketPdfLayoutSection origin)
    {
        var branding = packet.Branding;
        var logoBytes = branding.HasLogo && TryDecodeDataUri(branding.LogoDataUri!, out var bytes)
            ? bytes
            : null;

        container.Column(col =>
        {
            col.Spacing(3f);

            // Optional logo + brand letterhead (left) + tracking number (right), mirroring IDS.
            col.Item().Row(row =>
            {
                row.RelativeItem().Row(brand =>
                {
                    brand.Spacing(8f);
                    if (logoBytes is not null)
                    {
                        brand.ConstantItem(34f).AlignMiddle().Image(logoBytes).FitWidth();
                    }

                    brand.RelativeItem().Column(left =>
                    {
                        left.Item().Text(branding.BrandName).Bold().FontSize(13f);
                        left.Item().Text("SERVICE INTAKE PACKET").FontSize(8f);
                    });
                });

                row.ConstantItem(170f).Column(right =>
                {
                    right.Item().AlignRight().Text(t =>
                    {
                        t.Span("RVS #: ").FontSize(11f);
                        t.Span(packet.Origin.ReferenceCode).Bold().FontSize(12f);
                    });
                    // Full timestamp (date + time, UTC) — the one Received line on the packet.
                    right.Item().AlignRight().Text($"Received: {layout.ReceivedDisplay}").FontSize(9f);
                });
            });

            // Customer name, family-name-first, above the unit descriptor headline.
            if (layout.CustomerHeadline is not null)
            {
                col.Item().PaddingTop(2f).Text(layout.CustomerHeadline).SemiBold().FontSize(11f);
            }

            // Unit descriptor headline.
            col.Item().PaddingTop(2f).Text(unit.Heading).SemiBold().FontSize(15f);

            // Three-column identity band: Customer | Location | Unit.
            col.Item().PaddingTop(2f).Row(row =>
            {
                row.Spacing(14f);
                row.RelativeItem().Element(e => RenderMastheadColumn(e, "Customer", customer));
                row.RelativeItem().Element(e => RenderMastheadColumn(e, "Location", origin));
                row.RelativeItem().Element(e => RenderMastheadColumn(e, "Unit", unit));
            });

            col.Item().PaddingTop(4f).LineHorizontal(2f);
        });
    }

    /// <summary>
    /// Decodes a <c>data:</c> URI's base64 payload to bytes (the masthead logo — see
    /// <see cref="PacketBranding.LogoDataUri"/>). Returns <c>false</c> for anything that is
    /// not a base64 <c>data:</c> URI so the masthead simply renders without a logo rather
    /// than throwing.
    /// </summary>
    private static bool TryDecodeDataUri(string dataUri, out byte[] bytes)
    {
        bytes = [];

        if (!dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var comma = dataUri.IndexOf(',');
        if (comma < 0 || !dataUri[..comma].Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(dataUri[(comma + 1)..]);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void RenderMastheadColumn(
        IContainer container, string heading, PacketPdfLayoutSection section)
    {
        container.Column(col =>
        {
            col.Spacing(1.5f);

            col.Item().Text(heading.ToUpperInvariant()).SemiBold().FontSize(8f);
            col.Item().PaddingBottom(1f).LineHorizontal(0.75f);

            // Customer name leads its column (Body); Unit/Location are label/value rows.
            if (section.Body is not null)
            {
                col.Item().Text(section.Body).SemiBold().FontSize(9.5f);
            }

            var rows = section.Rows.Where(r => r.Label != "RVS #").ToList();
            foreach (var row in rows)
            {
                col.Item().Text(t =>
                {
                    t.Span($"{row.Label}: ").SemiBold().FontSize(9f);
                    t.Span(row.Value).FontSize(9f);
                });
            }

            if (section.Body is null && rows.Count == 0)
            {
                col.Item().Text("Not recorded").Italic().FontSize(9f);
            }
        });
    }

    private static void RenderSection(
        ColumnDescriptor column,
        PacketPdfLayoutSection section,
        IReadOnlyDictionary<string, byte[]> images)
    {
        if (section.Emphasised)
        {
            // The diagnostic block: the heaviest frame on the page, legible in greyscale.
            column.Item().Border(2f).Padding(10f).Column(inner => RenderSectionBody(inner, section, images));
        }
        else
        {
            column.Item().Column(inner => RenderSectionBody(inner, section, images));
        }
    }

    private static void RenderSectionBody(
        ColumnDescriptor column,
        PacketPdfLayoutSection section,
        IReadOnlyDictionary<string, byte[]> images)
    {
        column.Spacing(4f);

        column.Item().Row(row =>
        {
            row.Spacing(6f);
            row.RelativeItem().Text(section.Heading).SemiBold().FontSize(11f);

            if (section.AiGeneratedTag)
            {
                row.ConstantItem(96f)
                    .AlignRight()
                    .Border(1f)
                    .PaddingHorizontal(4f)
                    .PaddingVertical(1f)
                    .Text("AI-GENERATED")
                    .FontSize(7f)
                    .SemiBold();
            }
        });

        column.Item().PaddingBottom(2f).LineHorizontal(1f);

        // Body before rows: the customer name leads, then the contact rows — matching
        // PacketHtmlRenderer.
        if (section.Body is not null)
        {
            column.Item().Text(section.Body);
        }

        foreach (var line in section.Rows)
        {
            column.Item().Text(text =>
            {
                text.Span($"{line.Label}: ").SemiBold();
                text.Span(line.Value);
            });
        }

        if (section.Verbatim is not null)
        {
            column.Item().Border(1f).Padding(8f).Text(section.Verbatim);
        }

        if (section.DiagnosticsEmpty)
        {
            column.Item().Text("No diagnostic questions were answered.").Italic();
        }

        foreach (var entry in section.Diagnostics)
        {
            column.Item().PaddingTop(4f).Text(entry.Question).SemiBold();

            if (entry.Answers.Count == 0)
            {
                column.Item().PaddingLeft(10f).Text("(no answer given)").Italic();
            }

            foreach (var answer in entry.Answers)
            {
                column.Item().PaddingLeft(10f).BorderLeft(2f).PaddingLeft(6f).Text(answer);
            }
        }

        if (section.Photos.Count > 0)
        {
            RenderPhotos(column, section, images);
        }

        if (section.Link is not null)
        {
            if (section.Link.Active)
            {
                column.Item().Hyperlink(section.Link.Url).Text(text => text.Span(section.Link.Url).Underline());
            }
            else
            {
                column.Item().Text(section.Link.Url);
            }
        }
    }

    private static void RenderPhotos(
        ColumnDescriptor column,
        PacketPdfLayoutSection section,
        IReadOnlyDictionary<string, byte[]> images)
    {
        var firstPage = section.Photos.Take(section.PhotosOnFirstPage).ToList();
        var appendix = section.Photos.Skip(section.PhotosOnFirstPage).ToList();

        column.Item().Element(container => RenderPhotoGrid(container, firstPage, images));

        if (appendix.Count > 0)
        {
            // Spec B-2 item 8: up to six thumbnails on page one, the rest on an appendix page.
            column.Item().PageBreak();
            column.Item().PaddingBottom(4f).Text("Photos (continued)").SemiBold().FontSize(11f);
            column.Item().Element(container => RenderPhotoGrid(container, appendix, images));
        }
    }

    private static void RenderPhotoGrid(
        IContainer container,
        IReadOnlyList<PacketPhoto> photos,
        IReadOnlyDictionary<string, byte[]> images)
    {
        container.Column(grid =>
        {
            grid.Spacing(8f);

            foreach (var pair in photos.Chunk(2))
            {
                grid.Item().Row(row =>
                {
                    row.Spacing(8f);

                    foreach (var photo in pair)
                    {
                        row.RelativeItem().Column(cell =>
                        {
                            cell.Spacing(2f);

                            if (images.TryGetValue(photo.Url, out var bytes) && IsDecodableRaster(bytes))
                            {
                                cell.Item().Image(bytes).FitWidth();
                            }
                            else
                            {
                                // No bytes, or a format QuestPDF's decoder cannot read
                                // (iPhone HEIC/HEIF is the common case — issue #492 item 8).
                                // A labelled placeholder keeps the packet laying out
                                // instead of a blank cell or a failed render.
                                cell.Item().Border(1f).Padding(12f).AlignCenter()
                                    .Text(photo.FileName).FontSize(9f);
                            }

                            var caption = string.IsNullOrWhiteSpace(photo.Caption)
                                ? photo.FileName
                                : $"{photo.FileName} — {photo.Caption}";
                            cell.Item().Text(caption).FontSize(9f);
                        });
                    }

                    // Keep a lone photo on an odd final row at half width.
                    if (pair.Length == 1)
                    {
                        row.RelativeItem();
                    }
                });
            }
        });
    }

    /// <summary>
    /// <c>true</c> when <paramref name="bytes"/> begins with the signature of a raster
    /// format QuestPDF's image decoder (SkiaSharp) can read: JPEG, PNG, GIF, BMP, or WebP.
    /// Everything else — most importantly Apple HEIC/HEIF from an iPhone, but also empty or
    /// corrupt payloads — returns <c>false</c> so the caller renders a placeholder rather
    /// than a blank cell or an exception during <c>GeneratePdf()</c> (issue #492 item 8).
    /// Proper HEIC support means transcoding to JPEG upstream; tracked as a follow-up.
    /// </summary>
    private static bool IsDecodableRaster(byte[]? bytes)
    {
        if (bytes is not { Length: >= 4 })
        {
            return false;
        }

        var b = bytes.AsSpan();

        return
            // JPEG: FF D8 FF
            (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            // PNG: 89 50 4E 47
            || (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            // GIF: "GIF8"
            || (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38)
            // BMP: "BM"
            || (b[0] == 0x42 && b[1] == 0x4D)
            // WebP: "RIFF" .... "WEBP"
            || (b.Length >= 12
                && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
                && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50);
    }
}
