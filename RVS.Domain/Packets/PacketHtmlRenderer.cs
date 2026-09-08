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
/// Layout follows the Integrated Dealer Systems (IDS) work-order idiom so a service
/// manager reads it on daily muscle memory: a right-aligned tracking number in the
/// masthead (<c>RVS #</c>, mirroring IDS <c>W/O #</c>), a three-column Customer / Location
/// / Unit band, then the AI <c>Preliminary assessment</c> above <c>COMPLAINT</c> (the
/// verbatim customer text) so the concise problem recreation is read first, and a running
/// page footer carrying the reference and page count. It deliberately omits everything IDS
/// uses for the repair-authorization contract — pricing, parts/labour tables, signatures,
/// arbitration text — none of which belongs in an intake packet (<c>Spec B-2</c>).
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
    /// <summary>
    /// Renders <paramref name="packet"/> to a complete HTML5 document string.
    /// </summary>
    public static string Render(ServicePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var submittedUtc = packet.Origin.SubmittedAtUtc
            .ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        var submittedDate = packet.Origin.SubmittedAtUtc
            .ToUniversalTime()
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var sb = new StringBuilder(4096);

        sb.Append("<!DOCTYPE html>\n");
        sb.Append("<html lang=\"en\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>Service Packet ").Append(Text(packet.Origin.ReferenceCode)).Append("</title>\n");
        sb.Append("<style>\n").Append(BuildStylesheet(RunningFooterText(packet.Origin.ReferenceCode, submittedUtc)))
            .Append("\n</style>\n");
        sb.Append("</head>\n<body>\n");
        sb.Append("<main class=\"packet\">\n");

        AppendMasthead(sb, packet, submittedUtc, submittedDate);
        AppendCategory(sb, packet.IssueCategory);
        // The AI assessment sits above the verbatim complaint: a service manager should see
        // the concise recreation of the problem first, then the customer's own words, then
        // the diagnostic detail (issue #431 follow-up; Spec B-2).
        AppendAiSummary(sb, packet.AiSummary);
        AppendDescription(sb, packet.IssueDescription);
        AppendDiagnostics(sb, packet.Diagnostics);
        AppendPhotos(sb, packet.Photos);
        AppendPasteBlock(sb, packet.PasteBlock);
        AppendStatusLink(sb, packet.StatusLink);
        AppendFooter(sb, packet.Origin.ReferenceCode, submittedUtc);

        sb.Append("</main>\n</body>\n</html>\n");

        return sb.ToString();
    }

    // ── Masthead: sections 1 (unit), 2 (customer), 3 (origin) ──────────────
    //
    // IDS puts the tracking number top-right and identifies the job in a three-column
    // Customer / Dates / Unit band. We mirror that: RVS # + received date in the refbox,
    // then a Customer | Location & received | Unit grid. The Spec B-2 section markers stay
    // in order (unit, customer, origin) so the ordering contract is unchanged.

    private static void AppendMasthead(
        StringBuilder sb, ServicePacket packet, string submittedUtc, string submittedDate)
    {
        var unit = packet.Unit;
        var customer = packet.Customer;
        var origin = packet.Origin;

        sb.Append("<!-- section:unit -->\n");
        sb.Append("<header class=\"masthead\">\n");

        // Masthead top: letterhead left, tracking number right.
        sb.Append("<div class=\"masthead-top\">\n");
        sb.Append("<div class=\"letterhead\">RV ServiceFlow")
            .Append("<span class=\"doctype\">Service intake packet</span></div>\n");
        sb.Append("<div class=\"refbox\">\n");
        sb.Append("<p class=\"rvsno\">RVS #: <strong>").Append(Text(origin.ReferenceCode)).Append("</strong></p>\n");
        sb.Append("<p class=\"received\">Received: ").Append(Text(submittedDate)).Append("</p>\n");
        sb.Append("</div>\n</div>\n");

        // Unit descriptor headline — useful in a cold inbox; IDS omits it only because the
        // DMS screen already shows the unit.
        var descriptor = string.Join(
            ' ',
            new[] { unit.Year?.ToString(CultureInfo.InvariantCulture), unit.Make, unit.Model }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        sb.Append("<h1>")
            .Append(string.IsNullOrWhiteSpace(descriptor) ? "Unit details not provided" : Text(descriptor))
            .Append("</h1>\n");

        // Three-column identity band.
        sb.Append("<div class=\"idcols\">\n");

        sb.Append("<!-- section:customer -->\n");
        sb.Append("<section class=\"col customer\">\n<h2>Customer</h2>\n");
        sb.Append("<p class=\"name\">").Append(Text(customer.FullName)).Append("</p>\n");
        AppendRow(sb, "Phone", customer.Phone);
        AppendRow(sb, "Email", customer.Email);
        AppendRow(sb, "Preferred contact", customer.PreferredContact);
        sb.Append("</section>\n");

        sb.Append("<!-- section:origin -->\n");
        sb.Append("<section class=\"col origin\">\n<h2>Location &amp; received</h2>\n");
        AppendRow(sb, "Location", origin.LocationName);
        AppendRow(sb, "Location phone", origin.LocationPhone);
        AppendRow(sb, "Received", submittedUtc);
        sb.Append("</section>\n");

        sb.Append("<section class=\"col unit\">\n<h2>Unit</h2>\n");
        var hasUnitRow = false;
        hasUnitRow |= AppendRow(sb, "Year", unit.Year?.ToString(CultureInfo.InvariantCulture));
        hasUnitRow |= AppendRow(sb, "Manufacturer", unit.Make);
        hasUnitRow |= AppendRow(sb, "Model", unit.Model);
        if (unit.HasVin)
        {
            sb.Append("<p class=\"row\">Serial# (VIN): <span>").Append(Text(unit.Vin!)).Append("</span></p>\n");
            hasUnitRow = true;
        }

        if (!hasUnitRow)
        {
            sb.Append("<p class=\"empty\">Not recorded</p>\n");
        }

        sb.Append("</section>\n");
        sb.Append("</div>\n");
        sb.Append("</header>\n");
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

    // ── 6. Complaint — the customer's words, verbatim (IDS "COMPLAINT") ────

    private static void AppendDescription(StringBuilder sb, string description)
    {
        sb.Append("<!-- section:description -->\n");
        sb.Append("<section class=\"description\">\n");
        sb.Append("<h2>Complaint <span class=\"sub\">— customer's words, verbatim</span></h2>\n");
        sb.Append("<pre class=\"verbatim\">").Append(Text(description)).Append("</pre>\n");
        sb.Append("</section>\n");
    }

    // ── 7. Diagnostic Q&A — the expert block ─────────────────────────────

    private static void AppendDiagnostics(StringBuilder sb, IReadOnlyList<PacketDiagnosticEntry> diagnostics)
    {
        sb.Append("<!-- section:diagnostics -->\n");
        sb.Append("<section class=\"diagnostics\">\n");
        sb.Append("<h2>Reported symptoms &amp; diagnostic Q&amp;A</h2>\n");

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

    // ── 5. AI summary — a preliminary assessment, labelled AI-generated ───
    //     Positioned above the complaint (see Render): the manager reads the concise
    //     problem recreation first.

    private static void AppendAiSummary(StringBuilder sb, PacketAiSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        sb.Append("<!-- section:ai-summary -->\n");
        sb.Append("<section class=\"ai-summary\">\n");
        sb.Append("<h2>Preliminary assessment <span class=\"tag\">AI-generated</span></h2>\n");
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

    // ── Running footer (mirrors IDS "Printed On … © … Page N of N") ───────

    private static void AppendFooter(StringBuilder sb, string referenceCode, string submittedUtc)
    {
        // A static end-of-flow footer for engines that ignore @page margin boxes
        // (Safari); the @page rule in the stylesheet repeats the same line on every
        // printed page where supported.
        sb.Append("<footer class=\"packet-foot\">\n");
        sb.Append("<span>RVS #").Append(Text(referenceCode)).Append(" · ").Append(Text(submittedUtc)).Append("</span>\n");
        sb.Append("<span>RV ServiceFlow — service intake packet</span>\n");
        sb.Append("</footer>\n");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Appends a <c>Label: value</c> row; returns <c>false</c> and appends nothing when the value is blank.</summary>
    private static bool AppendRow(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        sb.Append("<p class=\"row\">").Append(label).Append(": <span>")
            .Append(Text(value)).Append("</span></p>\n");
        return true;
    }

    /// <summary>
    /// The running-footer text baked into the <c>@page</c> margin box. CSS
    /// <c>content:</c> is a quoted string, so it must be ASCII and carry no <c>"</c> or
    /// <c>\</c>; the reference code and timestamp are already in that alphabet.
    /// </summary>
    private static string RunningFooterText(string referenceCode, string submittedUtc)
    {
        var raw = $"RVS #{referenceCode} / {submittedUtc} / RV ServiceFlow";
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is not ('"' or '\\') && !char.IsControl(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
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
    private static string BuildStylesheet(string runningFooterText) => $$"""
        @page {
          margin: 14mm 14mm 18mm;
          @bottom-left { content: "{{runningFooterText}}"; font-size: 8pt; color: #000; }
          @bottom-right { content: "Page " counter(page) " of " counter(pages); font-size: 8pt; color: #000; }
        }

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

        h1 { font-size: 15pt; margin: 3mm 0 0; }
        h2 {
          font-size: 11pt;
          text-transform: uppercase;
          letter-spacing: 0.04em;
          margin: 0 0 2mm;
          padding-bottom: 1mm;
          border-bottom: 1px solid #000;
        }
        h2 .sub { text-transform: none; letter-spacing: 0; font-weight: 400; font-size: 9pt; }

        section, header { margin: 0 0 6mm; break-inside: avoid; }

        /* Masthead — IDS-style letterhead + top-right tracking number + 3-column band. */
        .masthead { border-bottom: 2px solid #000; padding-bottom: 3mm; }
        .masthead-top { display: flex; justify-content: space-between; align-items: flex-start; gap: 8mm; }
        .letterhead { font-size: 13pt; font-weight: 700; }
        .letterhead .doctype {
          display: block;
          font-size: 8.5pt;
          font-weight: 400;
          text-transform: uppercase;
          letter-spacing: 0.06em;
        }
        .refbox { text-align: right; white-space: nowrap; }
        .refbox .rvsno { margin: 0; font-size: 12pt; }
        .refbox .rvsno strong { font-size: 13pt; }
        .refbox .received { margin: 0.5mm 0 0; font-size: 9pt; }

        .idcols { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 6mm; margin-top: 3mm; }
        .idcols .customer { grid-column: 1; }
        .idcols .origin { grid-column: 2; }
        .idcols .unit { grid-column: 3; }
        .idcols .col { margin: 0; }
        .idcols .col h2 { font-size: 8.5pt; margin-bottom: 1.5mm; }
        .idcols .col p { margin: 0 0 1mm; font-size: 9.5pt; }
        .idcols .col .name { font-weight: 700; }
        .idcols .col .empty { font-style: italic; }

        .row, .name { margin: 0 0 1mm; }

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

        .packet-foot {
          margin-top: 8mm;
          padding-top: 2mm;
          border-top: 1px solid #000;
          font-size: 8pt;
          display: flex;
          justify-content: space-between;
          gap: 6mm;
        }

        @media print {
          body { font-size: 10pt; }
          a { text-decoration: underline; }
          .packet { padding: 0; }
        }
        """;
}
