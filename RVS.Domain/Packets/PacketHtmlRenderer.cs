using System.Globalization;
using System.Net;
using System.Text;

namespace RVS.Domain.Packets;

/// <summary>
/// Renders a <see cref="ServicePacket"/> to a single self-contained HTML document with an
/// embedded print stylesheet (<c>Spec B-3</c>, issue <c>#431</c>). This HTML is the
/// primary packet artifact and is also used verbatim as the delivery email body.
///
/// It is a pure transform: string in, string out. It reads the composed
/// <see cref="ServicePacket"/> in <c>Spec B-2</c> order and never inspects a
/// <c>ServiceRequest</c>. It performs no I/O and embeds no assets — photos are referenced
/// by the time-limited URLs already resolved on the packet, never base64.
///
/// Design constraints, all verified structurally by the renderer's tests:
/// <list type="bullet">
///   <item>Prints cleanly at Letter and A4 — <c>@page</c> declares margins only and never
///   pins a paper size, so the printer's own paper selection wins.</item>
///   <item>Legible in greyscale — structure is carried by borders, weight, and textual
///   labels, never by colour alone.</item>
///   <item>The diagnostic Q&amp;A block is the visually dominant one; it is the block that
///   must read as expert.</item>
/// </list>
/// </summary>
public static class PacketHtmlRenderer
{
    private const int PhotosOnFirstPage = 6;

    /// <summary>
    /// Renders <paramref name="packet"/> to a complete HTML5 document string.
    /// </summary>
    public static string Render(ServicePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var sb = new StringBuilder(4096);

        sb.Append("<!DOCTYPE html>\n");
        sb.Append("<html lang=\"en\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>Service Packet ").Append(Text(packet.Origin.ReferenceCode)).Append("</title>\n");
        sb.Append("<style>\n").Append(Stylesheet).Append("\n</style>\n");
        sb.Append("</head>\n<body>\n");
        sb.Append("<main class=\"packet\">\n");

        AppendUnit(sb, packet.Unit);
        AppendCustomer(sb, packet.Customer);
        AppendOrigin(sb, packet.Origin);
        AppendCategory(sb, packet.IssueCategory);
        AppendDescription(sb, packet.IssueDescription);
        AppendDiagnostics(sb, packet.Diagnostics);
        AppendAiSummary(sb, packet.AiSummary);
        AppendPhotos(sb, packet.Photos);
        AppendPasteBlock(sb, packet.PasteBlock);
        AppendStatusLink(sb, packet.StatusLink);

        sb.Append("</main>\n</body>\n</html>\n");

        return sb.ToString();
    }

    // ── 1. Unit header ─────────────────────────────────────────────────────

    private static void AppendUnit(StringBuilder sb, PacketUnitHeader unit)
    {
        sb.Append("<!-- section:unit -->\n");
        sb.Append("<header class=\"unit\">\n");

        var descriptor = string.Join(
            ' ',
            new[] { unit.Year?.ToString(CultureInfo.InvariantCulture), unit.Make, unit.Model }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        if (string.IsNullOrWhiteSpace(descriptor))
        {
            sb.Append("<h1>Unit details not provided</h1>\n");
        }
        else
        {
            sb.Append("<h1>").Append(Text(descriptor)).Append("</h1>\n");
        }

        if (unit.HasVin)
        {
            sb.Append("<p class=\"vin\">VIN: <span>").Append(Text(unit.Vin!)).Append("</span></p>\n");
        }

        sb.Append("</header>\n");
    }

    // ── 2. Customer ────────────────────────────────────────────────────────

    private static void AppendCustomer(StringBuilder sb, PacketCustomer customer)
    {
        sb.Append("<!-- section:customer -->\n");
        sb.Append("<section class=\"customer\">\n");
        sb.Append("<h2>Customer</h2>\n");
        sb.Append("<p class=\"name\">").Append(Text(customer.FullName)).Append("</p>\n");
        AppendRow(sb, "Phone", customer.Phone);
        AppendRow(sb, "Email", customer.Email);
        AppendRow(sb, "Preferred contact", customer.PreferredContact);
        sb.Append("</section>\n");
    }

    // ── 3. Origin ──────────────────────────────────────────────────────────

    private static void AppendOrigin(StringBuilder sb, PacketOrigin origin)
    {
        sb.Append("<!-- section:origin -->\n");
        sb.Append("<section class=\"origin\">\n");
        sb.Append("<h2>Where &amp; when</h2>\n");
        AppendRow(sb, "Location", origin.LocationName);
        AppendRow(sb, "Location phone", origin.LocationPhone);

        var submitted = origin.SubmittedAtUtc
            .ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        AppendRow(sb, "Submitted", submitted + " UTC");

        sb.Append("<p class=\"reference\">Reference: <strong>")
            .Append(Text(origin.ReferenceCode))
            .Append("</strong></p>\n");
        sb.Append("</section>\n");
    }

    // ── 4. Category ────────────────────────────────────────────────────────

    private static void AppendCategory(StringBuilder sb, string? category)
    {
        sb.Append("<!-- section:category -->\n");
        sb.Append("<section class=\"category\">\n");
        sb.Append("<h2>Issue category</h2>\n");
        sb.Append("<p>")
            .Append(string.IsNullOrWhiteSpace(category) ? "Uncategorized" : Text(category))
            .Append("</p>\n");
        sb.Append("</section>\n");
    }

    // ── 5. Description, verbatim ──────────────────────────────────────────

    private static void AppendDescription(StringBuilder sb, string description)
    {
        sb.Append("<!-- section:description -->\n");
        sb.Append("<section class=\"description\">\n");
        sb.Append("<h2>In the customer's words</h2>\n");
        sb.Append("<pre class=\"verbatim\">").Append(Text(description)).Append("</pre>\n");
        sb.Append("</section>\n");
    }

    // ── 6. Diagnostic Q&A — the expert block ─────────────────────────────

    private static void AppendDiagnostics(StringBuilder sb, IReadOnlyList<PacketDiagnosticEntry> diagnostics)
    {
        sb.Append("<!-- section:diagnostics -->\n");
        sb.Append("<section class=\"diagnostics\">\n");
        sb.Append("<h2>Diagnostic questions &amp; answers</h2>\n");

        if (diagnostics.Count == 0)
        {
            sb.Append("<p class=\"empty\">No diagnostic questions were answered.</p>\n");
        }
        else
        {
            sb.Append("<dl>\n");
            foreach (var entry in diagnostics)
            {
                sb.Append("<dt>").Append(Text(entry.Question)).Append("</dt>\n");
                if (entry.Answers.Count == 0)
                {
                    sb.Append("<dd class=\"no-answer\">(no answer given)</dd>\n");
                    continue;
                }

                foreach (var answer in entry.Answers)
                {
                    sb.Append("<dd>").Append(Text(answer)).Append("</dd>\n");
                }
            }

            sb.Append("</dl>\n");
        }

        sb.Append("</section>\n");
    }

    // ── 7. AI summary ─────────────────────────────────────────────────────

    private static void AppendAiSummary(StringBuilder sb, PacketAiSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        sb.Append("<!-- section:ai-summary -->\n");
        sb.Append("<section class=\"ai-summary\">\n");
        sb.Append("<h2>Summary <span class=\"tag\">AI-generated</span></h2>\n");
        sb.Append("<p>").Append(Text(summary.Text)).Append("</p>\n");
        sb.Append("</section>\n");
    }

    // ── 8. Photos ─────────────────────────────────────────────────────────

    private static void AppendPhotos(StringBuilder sb, IReadOnlyList<PacketPhoto> photos)
    {
        var renderable = photos.Where(p => IsHttpUrl(p.Url)).ToList();
        if (renderable.Count == 0)
        {
            return;
        }

        sb.Append("<!-- section:photos -->\n");
        sb.Append("<section class=\"photos\">\n");
        sb.Append("<h2>Photos</h2>\n");
        sb.Append("<div class=\"photo-grid\">\n");
        foreach (var photo in renderable)
        {
            sb.Append("<figure>\n");
            sb.Append("<img src=\"").Append(Attr(photo.Url)).Append("\" alt=\"")
                .Append(Attr(photo.FileName)).Append("\">\n");
            sb.Append("<figcaption>").Append(Text(photo.FileName));
            if (!string.IsNullOrWhiteSpace(photo.Caption))
            {
                sb.Append(" — ").Append(Text(photo.Caption));
            }

            sb.Append("</figcaption>\n</figure>\n");
        }

        sb.Append("</div>\n</section>\n");
    }

    // ── 9. Paste block ───────────────────────────────────────────────────

    private static void AppendPasteBlock(StringBuilder sb, string? pasteBlock)
    {
        if (string.IsNullOrWhiteSpace(pasteBlock))
        {
            return;
        }

        sb.Append("<!-- section:paste-block -->\n");
        sb.Append("<section class=\"paste-block\">\n");
        sb.Append("<h2>Copy &amp; paste into your DMS</h2>\n");
        sb.Append("<pre class=\"dms-text\">").Append(Text(pasteBlock)).Append("</pre>\n");
        sb.Append("</section>\n");
    }

    // ── 10. Status link ─────────────────────────────────────────────────

    private static void AppendStatusLink(StringBuilder sb, PacketStatusLink? statusLink)
    {
        if (statusLink is null || string.IsNullOrWhiteSpace(statusLink.Url))
        {
            return;
        }

        sb.Append("<!-- section:status-link -->\n");
        sb.Append("<section class=\"status-link\">\n");
        sb.Append("<h2>Customer status page</h2>\n");

        if (IsHttpUrl(statusLink.Url))
        {
            sb.Append("<p><a href=\"").Append(Attr(statusLink.Url)).Append("\">")
                .Append(Text(statusLink.Url)).Append("</a></p>\n");
        }
        else
        {
            sb.Append("<p>").Append(Text(statusLink.Url)).Append("</p>\n");
        }

        sb.Append("</section>\n");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void AppendRow(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        sb.Append("<p class=\"row\">").Append(label).Append(": <span>")
            .Append(Text(value)).Append("</span></p>\n");
    }

    /// <summary>HTML-encodes text for element content.</summary>
    private static string Text(string value) => WebUtility.HtmlEncode(value);

    /// <summary>HTML-encodes a value for a double-quoted attribute (e.g. the <c>&amp;</c> in a SAS URL).</summary>
    private static string Attr(string value) => WebUtility.HtmlEncode(value);

    private static bool IsHttpUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The embedded print stylesheet. Greyscale-safe: every distinction is a border,
    /// a weight, or a label — no information is carried by colour. <c>@page</c> sets
    /// margins only and never a paper size, so a single document prints cleanly on both
    /// US Letter and A4.
    /// </summary>
    private const string Stylesheet = """
        @page { margin: 14mm; }

        * { box-sizing: border-box; }

        body {
          margin: 0;
          color: #000;
          background: #fff;
          font: 10.5pt/1.4 "Helvetica Neue", Arial, "Liberation Sans", sans-serif;
          -webkit-print-color-adjust: economy;
          print-color-adjust: economy;
        }

        .packet {
          /* A4 printable width (210mm - 2x14mm) is the narrower target; Letter is wider,
             so sizing to A4 prints cleanly on both. */
          max-width: 182mm;
          margin: 0 auto;
          padding: 8mm;
        }

        h1 { font-size: 16pt; margin: 0 0 2mm; }
        h2 {
          font-size: 11pt;
          text-transform: uppercase;
          letter-spacing: 0.04em;
          margin: 0 0 2mm;
          padding-bottom: 1mm;
          border-bottom: 1px solid #000;
        }

        section, header { margin: 0 0 6mm; break-inside: avoid; }

        .unit .vin { font-size: 10pt; margin: 0; }
        .row, .name, .reference { margin: 0 0 1mm; }
        .reference strong { font-size: 11pt; }

        .description .verbatim,
        pre.dms-text {
          white-space: pre-wrap;
          word-wrap: break-word;
          font: 10pt/1.45 "Courier New", "Liberation Mono", monospace;
          border: 1px solid #000;
          padding: 3mm;
          margin: 0;
        }

        /* The diagnostic Q&A is the block that must read as expert: the heaviest frame
           on the page, extra padding, bold questions. Emphasis survives greyscale. */
        .diagnostics {
          border: 3px solid #000;
          padding: 4mm 5mm;
          margin-bottom: 8mm;
        }
        .diagnostics h2 { border-bottom-width: 2px; }
        .diagnostics dl { margin: 0; }
        .diagnostics dt {
          font-weight: 700;
          margin: 3mm 0 1mm;
        }
        .diagnostics dt:first-child { margin-top: 0; }
        .diagnostics dd {
          margin: 0 0 0 5mm;
          padding-left: 3mm;
          border-left: 2px solid #000;
        }
        .diagnostics .empty { margin: 0; font-style: italic; }
        .diagnostics .no-answer { font-style: italic; }

        .ai-summary .tag {
          font-size: 8pt;
          font-weight: 700;
          text-transform: uppercase;
          border: 1px solid #000;
          padding: 0 1mm;
          vertical-align: middle;
        }

        .photo-grid { display: block; }
        .photo-grid figure {
          display: inline-block;
          width: 48%;
          margin: 0 1% 4mm;
          vertical-align: top;
          break-inside: avoid;
        }
        .photo-grid img { border: 1px solid #000; width: 100%; height: auto; }
        .photo-grid figcaption { font-size: 9pt; margin-top: 1mm; }
        /* Spec B-2 item 8: up to 6 photos on page one, the rest on an appendix page. */
        .photo-grid figure:nth-child(n + 7) { break-before: page; }

        .status-link a { color: #000; }

        @media print {
          body { font-size: 10pt; }
          a { text-decoration: underline; }
          .packet { padding: 0; }
        }
        """;
}
