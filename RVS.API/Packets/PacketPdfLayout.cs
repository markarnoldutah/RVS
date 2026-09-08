using System.Globalization;
using RVS.Domain.Packets;

namespace RVS.API.Packets;

/// <summary>
/// The ordered, degradation-resolved content model for the PDF packet — the single place
/// that decides <em>what</em> appears and in <em>what</em> order (<c>Spec B-2</c>).
///
/// It is a pure transform of a <see cref="ServicePacket"/>: no I/O, no QuestPDF types, no
/// page geometry. <see cref="PacketPdfRenderer"/> paints this model; keeping the two apart
/// lets the content be unit-tested as plain data and cross-checked against
/// <see cref="PacketHtmlRenderer"/> so the HTML and PDF renderings cannot diverge
/// (issue <c>#432</c>).
///
/// The section set, ordering and degradation rules deliberately match
/// <see cref="PacketHtmlRenderer"/> line for line.
/// </summary>
internal sealed record PacketPdfLayout
{
    /// <summary>Photo thumbnails on the first page; the rest go to an appendix page.</summary>
    private const int PhotosOnFirstPageLimit = 6;

    public required IReadOnlyList<PacketPdfLayoutSection> Sections { get; init; }

    public static PacketPdfLayout Build(ServicePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var sections = new List<PacketPdfLayoutSection>();

        sections.Add(BuildUnit(packet.Unit));
        sections.Add(BuildCustomer(packet.Customer));
        sections.Add(BuildOrigin(packet.Origin));
        sections.Add(BuildCategory(packet.IssueCategory));

        // The AI assessment sits above the verbatim complaint so the service manager reads
        // the concise problem recreation first (matches PacketHtmlRenderer; Spec B-2).
        var aiSummary = BuildAiSummary(packet.AiSummary);
        if (aiSummary is not null)
        {
            sections.Add(aiSummary);
        }

        sections.Add(BuildDescription(packet.IssueDescription));
        sections.Add(BuildDiagnostics(packet.Diagnostics));

        var photos = BuildPhotos(packet.Photos);
        if (photos is not null)
        {
            sections.Add(photos);
        }

        var pasteBlock = BuildPasteBlock(packet.PasteBlock);
        if (pasteBlock is not null)
        {
            sections.Add(pasteBlock);
        }

        var statusLink = BuildStatusLink(packet.StatusLink);
        if (statusLink is not null)
        {
            sections.Add(statusLink);
        }

        return new PacketPdfLayout { Sections = sections };
    }

    // ── 1. Unit header ────────────────────────────────────────────────────

    private static PacketPdfLayoutSection BuildUnit(PacketUnitHeader unit)
    {
        var descriptor = string.Join(
            ' ',
            new[] { unit.Year?.ToString(CultureInfo.InvariantCulture), unit.Make, unit.Model }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        var rows = new List<PacketPdfLayoutRow>();
        AddRow(rows, "Year", unit.Year?.ToString(CultureInfo.InvariantCulture));
        AddRow(rows, "Manufacturer", unit.Make);
        AddRow(rows, "Model", unit.Model);
        if (unit.HasVin)
        {
            rows.Add(new PacketPdfLayoutRow("Serial# (VIN)", unit.Vin!));
        }

        return new PacketPdfLayoutSection
        {
            Id = "unit",
            Heading = string.IsNullOrWhiteSpace(descriptor) ? "Unit details not provided" : descriptor,
            Rows = rows,
        };
    }

    // ── 2. Customer ──────────────────────────────────────────────────────

    private static PacketPdfLayoutSection BuildCustomer(PacketCustomer customer)
    {
        var rows = new List<PacketPdfLayoutRow>();
        AddRow(rows, "Phone", customer.Phone);
        AddRow(rows, "Email", customer.Email);
        AddRow(rows, "Preferred contact", customer.PreferredContact);

        return new PacketPdfLayoutSection
        {
            Id = "customer",
            Heading = "Customer",
            Body = customer.FullName,
            Rows = rows,
        };
    }

    // ── 3. Origin ────────────────────────────────────────────────────────

    private static PacketPdfLayoutSection BuildOrigin(PacketOrigin origin)
    {
        var rows = new List<PacketPdfLayoutRow>();
        AddRow(rows, "Location", origin.LocationName);
        AddRow(rows, "Location phone", origin.LocationPhone);

        var submitted = origin.SubmittedAtUtc
            .ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        rows.Add(new PacketPdfLayoutRow("Received", submitted + " UTC"));
        rows.Add(new PacketPdfLayoutRow("RVS #", origin.ReferenceCode));

        return new PacketPdfLayoutSection
        {
            Id = "origin",
            Heading = "Location & received",
            Rows = rows,
        };
    }

    // ── 4. Category ──────────────────────────────────────────────────────

    private static PacketPdfLayoutSection BuildCategory(string? category) => new()
    {
        Id = "category",
        Heading = "Issue category",
        Body = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category,
    };

    // ── 6. Complaint — the customer's words, verbatim ───────────────────

    private static PacketPdfLayoutSection BuildDescription(string description) => new()
    {
        Id = "description",
        Heading = "Complaint",
        Verbatim = description,
    };

    // ── 7. Diagnostic Q&A — the expert block ────────────────────────────

    private static PacketPdfLayoutSection BuildDiagnostics(IReadOnlyList<PacketDiagnosticEntry> diagnostics)
    {
        var entries = diagnostics
            .Select(d => new PacketPdfLayoutDiagnostic(d.Question, d.Answers))
            .ToList();

        return new PacketPdfLayoutSection
        {
            Id = "diagnostics",
            Heading = "Reported symptoms & diagnostic Q&A",
            Emphasised = true,
            Diagnostics = entries,
            DiagnosticsEmpty = entries.Count == 0,
        };
    }

    // ── 5. AI summary — a preliminary assessment, above the complaint ───

    private static PacketPdfLayoutSection? BuildAiSummary(PacketAiSummary? summary)
    {
        if (summary is null)
        {
            return null;
        }

        return new PacketPdfLayoutSection
        {
            Id = "ai-summary",
            Heading = "Preliminary assessment",
            AiGeneratedTag = true,
            Body = summary.Text,
        };
    }

    // ── 8. Photos ───────────────────────────────────────────────────────

    private static PacketPdfLayoutSection? BuildPhotos(IReadOnlyList<PacketPhoto> photos)
    {
        var renderable = photos.Where(p => IsHttpUrl(p.Url)).ToList();
        if (renderable.Count == 0)
        {
            return null;
        }

        return new PacketPdfLayoutSection
        {
            Id = "photos",
            Heading = "Photos",
            Photos = renderable,
            PhotosOnFirstPage = Math.Min(PhotosOnFirstPageLimit, renderable.Count),
        };
    }

    // ── 9. Paste block ─────────────────────────────────────────────────

    private static PacketPdfLayoutSection? BuildPasteBlock(string? pasteBlock)
    {
        if (string.IsNullOrWhiteSpace(pasteBlock))
        {
            return null;
        }

        return new PacketPdfLayoutSection
        {
            Id = "paste-block",
            Heading = "Copy & paste into your DMS",
            Verbatim = pasteBlock,
        };
    }

    // ── 10. Status link ───────────────────────────────────────────────

    private static PacketPdfLayoutSection? BuildStatusLink(PacketStatusLink? statusLink)
    {
        if (statusLink is null || string.IsNullOrWhiteSpace(statusLink.Url))
        {
            return null;
        }

        return new PacketPdfLayoutSection
        {
            Id = "status-link",
            Heading = "Customer status page",
            Link = new PacketPdfLayoutLink(statusLink.Url, IsHttpUrl(statusLink.Url)),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void AddRow(List<PacketPdfLayoutRow> rows, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            rows.Add(new PacketPdfLayoutRow(label, value));
        }
    }

    private static bool IsHttpUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A stable plain-text projection of the layout, in section order. Used by the
    /// renderer's tests to assert content and ordering without parsing PDF bytes, and as
    /// the parity anchor against <see cref="PacketHtmlRenderer"/>.
    /// </summary>
    public string ToPlainText()
    {
        var blocks = Sections.Select(RenderSectionText);
        return string.Join("\n\n", blocks) + "\n";
    }

    private static string RenderSectionText(PacketPdfLayoutSection section)
    {
        var lines = new List<string> { $"[section:{section.Id}]" };

        lines.Add(section.AiGeneratedTag ? $"{section.Heading} [AI-generated]" : section.Heading);

        // Body before rows: for the customer section the name leads, then the contact
        // rows — matching PacketHtmlRenderer (name in <p class="name">, then the rows).
        if (section.Body is not null)
        {
            lines.Add(section.Body);
        }

        foreach (var row in section.Rows)
        {
            lines.Add($"{row.Label}: {row.Value}");
        }

        if (section.Verbatim is not null)
        {
            lines.Add(section.Verbatim);
        }

        if (section.DiagnosticsEmpty)
        {
            lines.Add("No diagnostic questions were answered.");
        }

        foreach (var entry in section.Diagnostics)
        {
            lines.Add(entry.Question);
            if (entry.Answers.Count == 0)
            {
                lines.Add("  - (no answer given)");
            }

            foreach (var answer in entry.Answers)
            {
                lines.Add($"  - {answer}");
            }
        }

        foreach (var photo in section.Photos)
        {
            lines.Add(string.IsNullOrWhiteSpace(photo.Caption)
                ? photo.FileName
                : $"{photo.FileName} — {photo.Caption}");
        }

        if (section.Link is not null)
        {
            lines.Add(section.Link.Url);
        }

        return string.Join("\n", lines);
    }
}

/// <summary>One packet section, resolved for rendering. Payload fields are mutually
/// mostly-exclusive — a section uses whichever of rows / body / verbatim / diagnostics /
/// photos / link applies to it.</summary>
internal sealed record PacketPdfLayoutSection
{
    /// <summary>Stable section id, matching the HTML renderer's <c>section:*</c> markers.</summary>
    public required string Id { get; init; }

    /// <summary>Section heading. For the unit header this is the year/make/model line itself.</summary>
    public required string Heading { get; init; }

    /// <summary>When <c>true</c>, the heading carries an "AI-generated" tag (<c>Spec B-2</c> item 7).</summary>
    public bool AiGeneratedTag { get; init; }

    /// <summary>When <c>true</c>, the section is framed as the visually dominant block (diagnostics).</summary>
    public bool Emphasised { get; init; }

    /// <summary>Label/value rows.</summary>
    public IReadOnlyList<PacketPdfLayoutRow> Rows { get; init; } = [];

    /// <summary>A single free-text paragraph (customer name, category, AI summary).</summary>
    public string? Body { get; init; }

    /// <summary>Verbatim text rendered in a bordered block, whitespace preserved (description, paste block).</summary>
    public string? Verbatim { get; init; }

    /// <summary>Diagnostic question/answer entries.</summary>
    public IReadOnlyList<PacketPdfLayoutDiagnostic> Diagnostics { get; init; } = [];

    /// <summary><c>true</c> when the diagnostics section has no entries and shows a placeholder.</summary>
    public bool DiagnosticsEmpty { get; init; }

    /// <summary>Renderable photos (those with an http(s) URL), in order.</summary>
    public IReadOnlyList<PacketPhoto> Photos { get; init; } = [];

    /// <summary>How many photos render on the first page; the remainder go to an appendix page.</summary>
    public int PhotosOnFirstPage { get; init; }

    /// <summary>The status link, or <c>null</c> when this is not the status-link section.</summary>
    public PacketPdfLayoutLink? Link { get; init; }

    /// <summary><c>true</c> when there are more photos than fit on the first page.</summary>
    public bool HasAppendix => Photos.Count > PhotosOnFirstPage;
}

/// <summary>A label/value line.</summary>
internal sealed record PacketPdfLayoutRow(string Label, string Value);

/// <summary>One diagnostic question and its answer(s).</summary>
internal sealed record PacketPdfLayoutDiagnostic(string Question, IReadOnlyList<string> Answers);

/// <summary>The status link. <see cref="Active"/> is <c>false</c> for a non-http(s) URL,
/// which renders as inert text rather than a clickable link.</summary>
internal sealed record PacketPdfLayoutLink(string Url, bool Active);
