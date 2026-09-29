using QuestPDF.Drawing.Exceptions;
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

    /// <summary>Height of each photo's image box, in points. Three rows of two — six photos —
    /// fit one page with their captions (<c>Spec B-2</c> item 8, issue #775); at full
    /// half-page width a portrait phone photo ran ~120 mm tall and only four fit.</summary>
    private const float PhotoHeightPt = 175f;

    /// <summary>Space between the photo grid's two columns and between its rows, in points.</summary>
    private const float PhotoGutterPt = 8f;

    /// <summary>Width of each photo cell, in points: half the content width, less the gutter.</summary>
    private const float PhotoCellWidthPt = ((PageWidthMm - 2 * PageMarginMm) / 25.4f * 72f - PhotoGutterPt) / 2f;

    /// <summary>
    /// Every photo is cropped to the cell's shape so the grid is uniform (issue #736); fitted
    /// whole, a portrait phone photo was a narrow strip beside a wide one.
    /// </summary>
    private const double PhotoCellAspect = PhotoCellWidthPt / PhotoHeightPt;

    /// <summary>Widest a cropped photo is kept, in pixels — about 250 dpi across the cell.</summary>
    private const uint PhotoMaxWidthPx = 900;

    /// <summary>Sections 1–3 (<c>Spec B-2</c>) are painted together as the IDS-style
    /// masthead — a logo, the letterhead over a top-right tracking number, then a three-column
    /// Customer / Location / Unit band — rather than as three stacked blocks.</summary>
    private static readonly string[] MastheadSectionIds = ["unit", "customer", "origin"];

    private static readonly IReadOnlyDictionary<string, byte[]> NoImages =
        new Dictionary<string, byte[]>();

    /// <summary>Height of the footer's "Powered by" mark, in points; the width follows the lockup's aspect.</summary>
    private const float PoweredByMarkHeightPt = 14f;

    /// <summary>The logo kit's horizontal lockup (390.4 x 108), for the footer's "Powered by" mark.</summary>
    private const float PoweredByMarkAspect = 390.4f / 108f;

    /// <summary>
    /// The RV Intake horizontal lockup as SVG markup (issue #470), embedded from
    /// <c>RVS.UI.Shared/wwwroot/brand/logo-horizontal.svg</c> so the PDF footer draws the same file
    /// the apps do. Its text is outlined, so no font is needed to render it.
    /// </summary>
    internal static readonly string PoweredByMarkSvg = LoadPoweredByMarkSvg();

    private static string LoadPoweredByMarkSvg()
    {
        using var stream = typeof(PacketPdfRenderer).Assembly.GetManifestResourceStream("RVS.API.Packets.logo-horizontal.svg")
            ?? throw new InvalidOperationException("The embedded RV Intake mark (logo-horizontal.svg) is missing from RVS.API.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

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
    /// <param name="logoImage">
    /// The dealer logo's bytes (issue #470), fetched by the caller from
    /// <see cref="PacketBranding.LogoUrl"/>. Drawn top left only when the packet's branding has a
    /// logo and the bytes are a PNG or JPEG; otherwise the masthead renders without one.
    /// </param>
    public static byte[] Render(
        ServicePacket packet,
        IReadOnlyDictionary<string, byte[]>? photoImages = null,
        byte[]? logoImage = null)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return RenderDocument([packet], photoImages, logoImage, $"Service Packet {packet.Origin.ReferenceCode}");
    }

    /// <summary>
    /// Renders every packet of a multi-problem submission (<c>Spec A-17</c>, issue #806) to one
    /// PDF, attached to the submission's single packet email (<c>Spec B-4</c>). Each packet is laid
    /// out exactly as <see cref="Render"/> lays it out and starts its own sheet, in submission
    /// order. A single packet renders byte-for-byte as <see cref="Render"/> would.
    /// </summary>
    /// <param name="packets">The submission's packets, in the order the customer reported the problems.</param>
    /// <param name="photoImages">
    /// Image bytes keyed by <see cref="PacketPhoto.Url"/> across every packet — read URLs are
    /// unique, so one map serves them all. Photos with no entry render as a labelled placeholder.
    /// </param>
    /// <param name="logoImage">The dealer logo's bytes; the packets share a location, so they share it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="packets"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="packets"/> is empty.</exception>
    public static byte[] RenderCombined(
        IReadOnlyList<ServicePacket> packets,
        IReadOnlyDictionary<string, byte[]>? photoImages = null,
        byte[]? logoImage = null)
    {
        ArgumentNullException.ThrowIfNull(packets);
        if (packets.Count == 0)
        {
            throw new ArgumentException("At least one packet is required.", nameof(packets));
        }

        var title = packets.Count == 1
            ? $"Service Packet {packets[0].Origin.ReferenceCode}"
            : $"Service Packets {packets[0].Origin.ReferenceCode}";

        return RenderDocument(packets, photoImages, logoImage, title);
    }

    private static byte[] RenderDocument(
        IReadOnlyList<ServicePacket> packets,
        IReadOnlyDictionary<string, byte[]>? photoImages,
        byte[]? logoImage,
        string title)
    {
        var images = photoImages ?? NoImages;
        var first = packets[0];
        // Document metadata dates stay UTC by PDF spec — deliberately not the location-local
        // Received line (issue #506). Converting them would make the bytes depend on the host's
        // time-zone database and break the determinism test below.
        var submitted = first.Origin.SubmittedAtUtc.UtcDateTime;

        return Document.Create(container =>
            {
                // One page block per packet: QuestPDF starts each on a fresh sheet, so every
                // problem in a combined document prints as the packet it would be on its own.
                foreach (var packet in packets)
                {
                    ComposePacketPages(container, packet, images, logoImage);
                }
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = title,
                Author = first.Branding.BrandName,
                Subject = "RV service intake packet",
                // Pinned to the packet so the same packet renders byte-for-byte identically.
                CreationDate = submitted,
                ModifiedDate = submitted,
            })
            .GeneratePdf();
    }

    private static void ComposePacketPages(
        IDocumentContainer container,
        ServicePacket packet,
        IReadOnlyDictionary<string, byte[]> images,
        byte[]? logoImage)
    {
        var layout = PacketPdfLayout.Build(packet);

        var masthead = MastheadSectionIds
            .Select(id => layout.Sections.First(s => s.Id == id))
            .ToArray();
        var bodySections = layout.Sections.Where(s => !MastheadSectionIds.Contains(s.Id));
        var logo = packet.Branding.HasLogo ? TryDecodeLogo(logoImage) : null;

        container.Page(page =>
        {
            page.Size(PageWidthMm, PageHeightMm, Unit.Millimetre);
            page.Margin(PageMarginMm, Unit.Millimetre);
            page.DefaultTextStyle(text => text.FontSize(10.5f).FontColor(Colors.Black));

            page.Content().Column(column =>
            {
                column.Spacing(18f);

                column.Item().Element(e => RenderMasthead(
                    e, packet, layout, logo, unit: masthead[0], customer: masthead[1], origin: masthead[2]));

                foreach (var section in bodySections)
                {
                    RenderSection(column, section, images);
                }

                // The end-of-flow footer, matching the HTML's static one: "Powered by" the
                // RV Intake mark, centred (issue #470), then the one AI disclosure
                // (issue #780). Once, after the last section — not on every page.
                column.Item().BorderTop(1f).PaddingTop(4f).Column(foot =>
                {
                    foot.Spacing(3f);
                    foot.Item().AlignCenter().Element(RenderPoweredBy);

                    if (layout.AiDisclaimer is not null)
                    {
                        foot.Item().Text(layout.AiDisclaimer).Italic().FontSize(8f);
                    }
                });
            });
        });
    }

    // ── Masthead: sections 1–3 as an IDS-style band ──────────────────────

    private static void RenderMasthead(
        IContainer container,
        ServicePacket packet,
        PacketPdfLayout layout,
        Image? logo,
        PacketPdfLayoutSection unit,
        PacketPdfLayoutSection customer,
        PacketPdfLayoutSection origin)
    {
        container.Column(col =>
        {
            col.Spacing(3f);

            // The dealer's logo, when the location sets one (Spec A-16, issue #470), top left;
            // the letterhead above the tracking number, right (issue #800), mirroring IDS. The
            // letterhead names the document, not the brand (issue #794).
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(brand =>
                {
                    if (logo is not null)
                    {
                        brand.AlignLeft().Width(LogoWidthPt(logo)).Height(LogoHeightPt).Image(logo).FitArea();
                    }
                });

                row.ConstantItem(170f).Column(right =>
                {
                    right.Item().AlignRight().Text("Service Intake Packet").Bold().FontSize(13f);
                    right.Item().AlignRight().Text(t =>
                    {
                        t.Span($"{PacketPdfLayout.ReferenceLabel}: ").FontSize(11f);
                        t.Span(packet.Origin.ReferenceCode).Bold().FontSize(12f);
                    });
                    // Full timestamp in the location's own zone, UTC when it has none
                    // (issue #506) — the one Received line on the packet.
                    right.Item().AlignRight().Text($"Received: {layout.ReceivedDisplay}").FontSize(9f);
                });
            });

            // Two lines of whitespace below the logo row (issue #794), 10 mm as in the HTML.
            if (logo is not null)
            {
                col.Item().Height(LogoGapPt);
            }

            // Title line: customer name (family-name-first) and unit descriptor on one line,
            // same size, bold (issue #580) — e.g. "Gribble, Dale : 2021 Winnebago View".
            var titleLine = layout.CustomerHeadline is null
                ? unit.Heading
                : $"{layout.CustomerHeadline} : {unit.Heading}";
            col.Item().PaddingTop(2f).Text(titleLine).Bold().FontSize(13f);

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

    /// <summary>The dealer logo's height in the masthead, in points — 12 mm, as in the HTML.</summary>
    private const float LogoHeightPt = 34f;

    /// <summary>The widest a dealer logo may run, in points — 60 mm, as in the HTML.</summary>
    private const float LogoMaxWidthPt = 170f;

    /// <summary>The whitespace below the logo row, in points — 10 mm, two lines of body text, as in the HTML.</summary>
    private const float LogoGapPt = 28f;

    /// <summary>
    /// Decodes the dealer logo, or returns <c>null</c> — and the masthead renders without one —
    /// when there are no bytes, they are not a PNG or JPEG, or they do not decode.
    /// </summary>
    private static Image? TryDecodeLogo(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 } || !PacketImageSignature.IsPngOrJpeg(bytes))
        {
            return null;
        }

        try
        {
            var image = Image.FromBinaryData(bytes);
            return image.Size.Width > 0 && image.Size.Height > 0 ? image : null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DocumentComposeException)
        {
            return null;
        }
    }

    /// <summary>The logo's width at <see cref="LogoHeightPt"/>, keeping its aspect, capped at <see cref="LogoMaxWidthPt"/>.</summary>
    private static float LogoWidthPt(Image logo) =>
        Math.Min(LogoMaxWidthPt, LogoHeightPt * logo.Size.Width / logo.Size.Height);

    /// <summary>"Powered by" and the RV Intake mark on one centred line (issue #470).</summary>
    private static void RenderPoweredBy(IContainer container)
    {
        container.Row(row =>
        {
            row.Spacing(4f);
            row.AutoItem().AlignMiddle().Text("Powered by").FontSize(8f);
            row.ConstantItem(PoweredByMarkHeightPt * PoweredByMarkAspect)
                .Height(PoweredByMarkHeightPt)
                .AlignMiddle()
                .Svg(PoweredByMarkSvg);
        });
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

            var rows = section.Rows.Where(r => r.Label != PacketPdfLayout.ReferenceLabel).ToList();
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
        column.Item().Column(inner => RenderSectionBody(inner, section, images));
    }

    private static void RenderSectionBody(
        ColumnDescriptor column,
        PacketPdfLayoutSection section,
        IReadOnlyDictionary<string, byte[]> images)
    {
        column.Spacing(4f);

        column.Item().Text(section.Heading).Bold().FontSize(11f);

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

        foreach (var list in section.Lists)
        {
            column.Item().PaddingTop(2f).Text(list.Label).SemiBold();
            for (var i = 0; i < list.Items.Count; i++)
            {
                var marker = list.Numbered ? $"{i + 1}." : "•";
                var item = list.Items[i];
                column.Item().PaddingLeft(10f).Row(row =>
                {
                    row.ConstantItem(14f).Text(marker);
                    row.RelativeItem().Text(item);
                });
            }
        }

        if (section.Note is not null)
        {
            column.Item().PaddingTop(2f).Text(section.Note).Italic().FontSize(8.5f);
        }

        if (section.Verbatim is not null)
        {
            if (section.Framed)
            {
                column.Item().Border(1f).Padding(8f).Text(section.Verbatim);
            }
            else
            {
                column.Item().Text(section.Verbatim);
            }
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
            column.Item().Text("Photos (continued)").Bold().FontSize(11f);
            column.Item().PaddingBottom(2f).LineHorizontal(1f);
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
            grid.Spacing(PhotoGutterPt);

            foreach (var pair in photos.Chunk(2))
            {
                grid.Item().Row(row =>
                {
                    row.Spacing(PhotoGutterPt);

                    foreach (var photo in pair)
                    {
                        row.RelativeItem().Column(cell =>
                        {
                            cell.Spacing(2f);

                            // Every cell is the same box, whatever fills it (issue #736).
                            var box = cell.Item().Height(PhotoHeightPt);
                            var cropped = images.TryGetValue(photo.Url, out var bytes) && IsDecodableRaster(bytes)
                                ? PacketPhotoCrop.ToAspect(bytes, PhotoCellAspect, PhotoMaxWidthPx)
                                : null;

                            if (cropped is not null)
                            {
                                box.Image(cropped).FitArea();
                            }
                            else if (photo.IsVideo)
                            {
                                // Videos never render as a raster thumbnail — the tile links to
                                // the resolved read URL so the file is still reachable from the
                                // PDF (issue #583).
                                box.Hyperlink(photo.Url).Element(tile => RenderPhotoTile(tile, "Video", "Open video", underline: true));
                            }
                            else
                            {
                                // No bytes, or a format QuestPDF's decoder cannot read
                                // (iPhone HEIC/HEIF is the common case — issue #492 item 8).
                                // A labelled tile keeps the packet laying out instead of a
                                // blank cell or a failed render.
                                RenderPhotoTile(box, "Photo", "Preview unavailable", underline: false);
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

    /// <summary>A photo cell with no picture in it: a bordered tile carrying a label.</summary>
    private static void RenderPhotoTile(IContainer container, string label, string detail, bool underline)
    {
        container.Border(1f).Background(Colors.Grey.Lighten4).AlignCenter().AlignMiddle().Column(tile =>
        {
            tile.Item().AlignCenter().Text(label).SemiBold().FontSize(10f);
            tile.Item().AlignCenter().Text(text =>
            {
                var span = text.Span(detail).FontSize(9f);
                if (underline)
                {
                    span.Underline();
                }
            });
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
