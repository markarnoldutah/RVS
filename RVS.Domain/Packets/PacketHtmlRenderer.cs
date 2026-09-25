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
/// <c>ServiceRequest</c>. It performs no I/O. Images are neither embedded nor named — they
/// travel as the email's own attachments, which mail clients already show as clickable
/// thumbnails (issues <c>#580</c>, <c>#735</c>); only videos, which are never attached, get a
/// link. The only embedded asset is the optional masthead logo, supplied pre-encoded as a
/// <c>data:</c> URI on <see cref="PacketBranding"/>.
///
/// Layout follows the Integrated Dealer Systems (IDS) work-order idiom so a service
/// manager reads it on daily muscle memory: a right-aligned tracking number in the
/// masthead (<c>Intake #</c>, mirroring IDS <c>W/O #</c>), a three-column Customer / Location
/// / Unit band, then the curated <c>Issue</c> and the AI <c>Preliminary assessment</c> above
/// <c>Reported issue</c> (the verbatim customer text, pre-curation) so the concise problem
/// recreation is read first and can be checked against the customer's own words, and a running
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
///   <item>Email-client-safe layout: this HTML is used verbatim as the packet delivery
///   email body, and Gmail/Outlook silently drop <c>display:flex</c> and
///   <c>display:grid</c>. Every horizontal band (masthead top, the Customer / Location /
///   Unit identity band, the static footer) is therefore a presentational
///   <c>&lt;table&gt;</c> with its column geometry carried in <c>style=</c> attributes so
///   it still reads as columns when the <c>&lt;style&gt;</c> block is stripped.</item>
/// </list>
/// </summary>
public static class PacketHtmlRenderer
{
    /// <summary>
    /// Renders <paramref name="packet"/> to a complete HTML5 document string.
    /// </summary>
    /// <param name="packet">The composed packet.</param>
    /// <param name="managerAppServiceRequestUrl">
    /// When non-blank, a deep link into the Manager app for this service request, shown as a
    /// note under the photo list: some photos could not be attached to the email (the ACS size
    /// budget, <c>PacketEmailSizeFitter</c>, issue <c>#521</c>) and are visible only there.
    /// <c>null</c> — the default — omits the note; pass it only when the caller has already
    /// determined that at least one photo attachment was dropped (issue <c>#580</c>).
    /// </param>
    public static string Render(ServicePacket packet, string? managerAppServiceRequestUrl = null)
    {
        ArgumentNullException.ThrowIfNull(packet);

        // One string, three surfaces: the masthead, the @page running footer, and the static
        // end-of-flow footer. Derived on the packet (issue #506) so the PDF renderer reads the
        // very same value rather than a second copy of the same expression.
        var received = packet.Origin.ReceivedDisplay;
        var brandName = packet.Branding.BrandName;

        var sb = new StringBuilder(4096);

        sb.Append("<!DOCTYPE html>\n");
        sb.Append("<html lang=\"en\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>Service Packet ").Append(Text(packet.Origin.ReferenceCode)).Append("</title>\n");
        sb.Append("<style>\n").Append(BuildStylesheet(RunningFooterText(packet.Origin.ReferenceCode, received, brandName)))
            .Append("\n</style>\n");
        sb.Append("</head>\n<body>\n");
        sb.Append("<main class=\"packet\">\n");

        AppendManagerActions(sb, packet.ManagerLinks);
        AppendMasthead(sb, packet, received);
        AppendCategory(sb, packet.IssueCategory);
        // The curated issue and the AI assessment sit above the verbatim complaint: a service
        // manager should see the concise recreation of the problem first, then the customer's
        // own words, then the diagnostic detail (issue #431 follow-up, #601; Spec B-2).
        AppendCuratedIssue(sb, packet.CuratedIssue);
        AppendAiSummary(sb, packet.AiSummary);
        AppendDescription(sb, packet.IssueDescription);
        AppendDiagnostics(sb, packet.Diagnostics);
        AppendPhotos(sb, packet.Photos, managerAppServiceRequestUrl);
        AppendPasteBlock(sb, packet.PasteBlock);
        AppendStatusLink(sb, packet.StatusLink);
        AppendFooter(sb, packet.Origin.ReferenceCode, received, brandName);

        sb.Append("</main>\n</body>\n</html>\n");

        return sb.ToString();
    }

    // ── Manager-app status actions (Spec C-7, issue #498) ────────────────
    //
    // Delivery chrome, not a Spec B-2 section: it sits above the masthead so a service manager
    // can set status from the email in one tap, and the print stylesheet hides it. The links are
    // plain navigations into the signed-in manager app — they carry no token and write nothing,
    // so a mail-security scanner fetching them changes no state. A presentational <table> with
    // inline styles, never flex/grid, for the same email-client reason as the masthead.
    //
    // The links are styled as brand buttons (Spec THEME-1, issue #735): status actions are
    // filled Rust, primary; "Open Manager" is outlined Rust, secondary, on its own line below.
    // Styles are inline because a mail client may strip the <style> block. The Domain cannot
    // reference RVS.UI.Shared, so the hex values mirror RvsBrand — change them together.

    /// <summary>Text-safe Rust, <c>RvsBrand.Accent</c> — 6.02:1 under white button text.</summary>
    private const string BrandAccent = "#A8431F";

    private const string ButtonBaseStyle =
        "display:inline-block;padding:2mm 4mm;border-radius:4px;font-weight:700;text-decoration:none;";

    private const string PrimaryButtonStyle =
        ButtonBaseStyle + "margin:1mm 2mm 1mm 0;background-color:" + BrandAccent + ";color:#ffffff;border:1px solid " + BrandAccent + ";";

    private const string SecondaryButtonStyle =
        ButtonBaseStyle + "background-color:#ffffff;color:" + BrandAccent + ";border:1px solid " + BrandAccent + ";";

    private static void AppendManagerActions(StringBuilder sb, PacketManagerLinks? links)
    {
        if (links is null || !IsHttpUrl(links.RequestUrl))
        {
            return;
        }

        sb.Append("<!-- section:manager-actions -->\n");
        sb.Append("<table role=\"presentation\" class=\"manager-actions\" width=\"100%\" style=\"width:100%;border-collapse:collapse;margin:0 0 5mm;border:1px solid #000;\">\n<tr>\n");
        sb.Append("<td style=\"padding:2mm 3mm;font-size:9.5pt;\">\n");
        sb.Append("<p style=\"margin:0 0 1mm;font-weight:700;\">Set status</p>\n");
        foreach (var action in links.Actions.Where(a => IsHttpUrl(a.Url)))
        {
            sb.Append("<a href=\"").Append(Attr(action.Url)).Append("\" style=\"").Append(PrimaryButtonStyle).Append("\">")
                .Append(Text(action.Label)).Append("</a>\n");
        }

        sb.Append("<p style=\"margin:2mm 0 0;\"><a href=\"").Append(Attr(links.RequestUrl))
            .Append("\" style=\"").Append(SecondaryButtonStyle).Append("\">Open Manager</a></p>\n");
        sb.Append("</td>\n</tr>\n</table>\n");
    }

    // ── Masthead: sections 1 (unit), 2 (customer), 3 (origin) ──────────────
    //
    // IDS puts the tracking number top-right and identifies the job in a three-column
    // Customer / Dates / Unit band. We mirror that: Intake # + full received timestamp in the
    // refbox, the customer name (Last, First) above the year/make/model headline, then a
    // Customer | Location | Unit band. The band and the refbox row are presentational
    // <table>s, not flex/grid — mail clients drop those and the columns would stack. The
    // Spec B-2 section markers stay in order (unit, customer, origin) so the ordering
    // contract is unchanged.

    private static void AppendMasthead(
        StringBuilder sb, ServicePacket packet, string received)
    {
        var unit = packet.Unit;
        var customer = packet.Customer;
        var origin = packet.Origin;
        var branding = packet.Branding;

        sb.Append("<!-- section:unit -->\n");
        sb.Append("<header class=\"masthead\" style=\"border-bottom:2px solid #000;padding-bottom:3mm;\">\n");

        // Masthead top: optional logo + brand letterhead left, tracking number right.
        // A presentational <table>, never flexbox: this HTML is used verbatim as the packet
        // delivery email body, and Gmail/Outlook drop `display:flex`, which would collapse
        // this row into a single stacked column. Column geometry is carried inline so it
        // survives even when the <style> block is stripped.
        sb.Append("<table role=\"presentation\" class=\"masthead-top\" width=\"100%\" style=\"width:100%;border-collapse:collapse;\">\n<tr>\n");
        sb.Append("<td class=\"brand\" style=\"vertical-align:top;\">\n");
        if (branding.HasLogo)
        {
            sb.Append("<img class=\"masthead-logo\" src=\"").Append(Attr(branding.LogoDataUri!))
                .Append("\" alt=\"\" style=\"height:12mm;width:auto;vertical-align:middle;margin-right:4mm;\">\n");
        }

        sb.Append("<div class=\"letterhead\" style=\"font-size:13pt;font-weight:700;\">").Append(Text(branding.BrandName))
            .Append("<span class=\"doctype\" style=\"display:block;font-size:8.5pt;font-weight:400;text-transform:uppercase;letter-spacing:0.06em;\">Service intake packet</span></div>\n");
        sb.Append("</td>\n");
        sb.Append("<td class=\"refbox\" style=\"vertical-align:top;text-align:right;white-space:nowrap;\">\n");
        sb.Append("<p class=\"rvsno\" style=\"margin:0;font-size:12pt;\">Intake #: <strong>").Append(Text(origin.ReferenceCode)).Append("</strong></p>\n");
        // Received line carries the full timestamp — date + time in the dealership's own
        // zone when the location sets one, UTC otherwise (issue #506). It is the one Received
        // line on the packet (the Location column no longer repeats it).
        sb.Append("<p class=\"received\" style=\"margin:0.5mm 0 0;font-size:9pt;\">Received: ").Append(Text(received)).Append("</p>\n");
        sb.Append("</td>\n</tr>\n</table>\n");

        // Title line: customer name (family-name-first) and unit descriptor on one line,
        // same size, bold (issue #580) — e.g. "Gribble, Dale : 2021 Winnebago View".
        var sortableName = customer.SortableName;
        var descriptor = string.Join(
            ' ',
            new[] { unit.Year?.ToString(CultureInfo.InvariantCulture), unit.Make, unit.Model }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        var descriptorText = string.IsNullOrWhiteSpace(descriptor) ? "Unit details not provided" : descriptor;
        var titleLine = string.IsNullOrWhiteSpace(sortableName) ? descriptorText : $"{sortableName} : {descriptorText}";
        sb.Append("<p class=\"packet-title\">").Append(Text(titleLine)).Append("</p>\n");

        // Three-column identity band — a presentational <table>, never CSS grid (same
        // email-client reason as the masthead top). Each column carries its width and top
        // alignment inline so the band still reads as three columns when the <style> block
        // is discarded by a mail client.
        sb.Append("<table role=\"presentation\" class=\"idcols\" width=\"100%\" style=\"width:100%;border-collapse:collapse;margin-top:3mm;\">\n<tr>\n");

        sb.Append("<!-- section:customer -->\n");
        sb.Append("<td class=\"col customer\" style=\"vertical-align:top;width:34%;padding-right:6mm;font-size:9.5pt;\">\n<h2>Customer</h2>\n");
        sb.Append("<p class=\"name\" style=\"font-weight:700;margin:0 0 1mm;\">").Append(Text(customer.FullName)).Append("</p>\n");
        // The customer's phone and email are links so the manager can call or reply in one tap
        // (issue #735). The location's own phone is not: nobody calls their own front desk.
        AppendLinkRow(sb, "Phone", customer.Phone, TelHref(customer.Phone));
        AppendLinkRow(sb, "Email", customer.Email, MailtoHref(customer.Email));
        AppendRow(sb, "Preferred contact", customer.PreferredContact);
        sb.Append("</td>\n");

        sb.Append("<!-- section:origin -->\n");
        sb.Append("<td class=\"col origin\" style=\"vertical-align:top;width:33%;padding-right:6mm;font-size:9.5pt;\">\n<h2>Location</h2>\n");
        AppendRow(sb, "Location", origin.LocationName);
        AppendRow(sb, "Location phone", origin.LocationPhone);
        sb.Append("</td>\n");

        sb.Append("<td class=\"col unit\" style=\"vertical-align:top;width:33%;font-size:9.5pt;\">\n<h2>Unit</h2>\n");
        var hasUnitRow = false;
        hasUnitRow |= AppendRow(sb, "Year", unit.Year?.ToString(CultureInfo.InvariantCulture));
        hasUnitRow |= AppendRow(sb, "Manufacturer", unit.Make);
        hasUnitRow |= AppendRow(sb, "Model", unit.Model);
        if (unit.HasVin)
        {
            sb.Append("<p class=\"row\" style=\"margin:0 0 1mm;\">Serial# (VIN): <span>").Append(Text(unit.Vin!)).Append("</span></p>\n");
            hasUnitRow = true;
        }

        if (!hasUnitRow)
        {
            sb.Append("<p class=\"empty\" style=\"font-style:italic;margin:0 0 1mm;\">Not recorded</p>\n");
        }

        sb.Append("</td>\n");
        sb.Append("</tr>\n</table>\n");
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

    // ── 4b. Issue — the curated restatement of the complaint (issue #601) ──
    //
    // Sits between the category and the assessment: the manager reads the clean version of the
    // problem first and can drop to the verbatim complaint below to check it. Labelled
    // AI-generated for the same reason the assessment is — the packet never passes machine
    // wording off as the customer's.

    private static void AppendCuratedIssue(StringBuilder sb, string? curatedIssue)
    {
        if (curatedIssue is null)
        {
            return;
        }

        sb.Append("<!-- section:curated-issue -->\n");
        sb.Append("<section class=\"curated-issue\">\n");
        sb.Append("<h2>Issue <span class=\"tag\">AI-generated</span></h2>\n");
        sb.Append("<p>").Append(Text(curatedIssue)).Append("</p>\n");
        sb.Append("</section>\n");
    }

    // ── 6. Reported issue — the customer's words, verbatim (IDS "COMPLAINT") ──

    private static void AppendDescription(StringBuilder sb, string description)
    {
        sb.Append("<!-- section:description -->\n");
        sb.Append("<section class=\"description\">\n");
        sb.Append("<h2>Reported issue <span class=\"sub\">— customer's words verbatim</span></h2>\n");
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
        if (summary.Text is not null)
        {
            sb.Append("<p>").Append(Text(summary.Text)).Append("</p>\n");
        }

        if (summary.HasStructuredAssessment)
        {
            AppendRow(sb, "Probable cause", summary.ProbableCause);
            AppendRow(sb, "Confidence", summary.Confidence);
            AppendAssessmentList(sb, "ol", "possible-fixes", "Possible fixes", summary.PossibleFixes);
            AppendAssessmentList(sb, "ul", "likely-parts", "Likely parts", summary.LikelyParts);
            sb.Append("<p class=\"advisory\" style=\"margin:2mm 0 0;font-size:8.5pt;font-style:italic;\">")
                .Append(Text(PacketAiSummary.AdvisoryNote)).Append("</p>\n");
        }

        sb.Append("</section>\n");
    }

    private static void AppendAssessmentList(
        StringBuilder sb, string tag, string cssClass, string label, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        sb.Append("<p class=\"list-label\" style=\"margin:2mm 0 1mm;font-weight:700;\">").Append(label).Append("</p>\n");
        sb.Append('<').Append(tag).Append(" class=\"").Append(cssClass)
            .Append("\" style=\"margin:0 0 1mm;padding-left:6mm;\">\n");
        foreach (var item in items)
        {
            sb.Append("<li>").Append(Text(item)).Append("</li>\n");
        }

        sb.Append("</").Append(tag).Append(">\n");
    }

    // ── 8. Photos ─────────────────────────────────────────────────────────
    //
    // Images are not listed at all: they are sent as email attachments, which the mail client
    // already shows as clickable thumbnails, so neither an <img> (issue #580) nor a line naming
    // the file (issue #735) adds anything. The section survives only for what the thumbnails
    // cannot show — a video, or a note that some images were left off the email.

    private static void AppendPhotos(StringBuilder sb, IReadOnlyList<PacketPhoto> photos, string? managerAppServiceRequestUrl)
    {
        var videos = photos.Where(p => p.IsVideo && IsHttpUrl(p.Url)).ToList();
        var hasDroppedImageNote = !string.IsNullOrWhiteSpace(managerAppServiceRequestUrl);
        if (videos.Count == 0 && !hasDroppedImageNote)
        {
            return;
        }

        sb.Append("<!-- section:photos -->\n");
        sb.Append("<section class=\"photos\">\n");
        sb.Append("<h2>Photos</h2>\n");
        // Videos are never attached to the email (too large) and never render as a thumbnail
        // either, so they get a plain hyperlink to the resolved read URL instead (issue #583).
        foreach (var video in videos)
        {
            sb.Append("<p class=\"photo-line\">video ").Append(Text(video.FileName))
                .Append(" — <a href=\"").Append(Attr(video.Url)).Append("\">view video</a></p>\n");
        }

        // One or more photo attachments did not fit the ACS size budget (PacketEmailSizeFitter,
        // issue #521) and were left off this email — point the reader at the Manager app instead
        // of silently dropping them (issue #580).
        if (hasDroppedImageNote)
        {
            sb.Append("<p class=\"photo-note\">Some images can only be shown in the manager app. <a href=\"")
                .Append(Attr(managerAppServiceRequestUrl!)).Append("\">Click here to view</a>.</p>\n");
        }

        sb.Append("</section>\n");
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
        // No mail client runs script, so a real copy-to-clipboard button cannot work here
        // (issue #735). user-select: all is the next best thing: one click selects the whole
        // block, fences included, ready for Ctrl/Cmd-C. Inline, so a stripped <style> keeps it;
        // a client that ignores it still allows an ordinary click-drag selection.
        sb.Append("<pre class=\"dms-text\" style=\"-webkit-user-select:all;user-select:all;\">")
            .Append(Text(pasteBlock)).Append("</pre>\n");
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

    private static void AppendFooter(StringBuilder sb, string referenceCode, string received, string brandName)
    {
        // A static end-of-flow footer for engines that ignore @page margin boxes
        // (Safari); the @page rule in the stylesheet repeats the same line on every
        // printed page where supported. A presentational <table> so the two ends stay on
        // one line in a mail client (no flexbox).
        sb.Append("<table role=\"presentation\" class=\"packet-foot\" width=\"100%\" style=\"width:100%;border-collapse:collapse;margin-top:8mm;border-top:1px solid #000;font-size:8pt;\">\n<tr>\n");
        sb.Append("<td style=\"padding-top:2mm;vertical-align:top;\">Intake #").Append(Text(referenceCode)).Append(" · ").Append(Text(received)).Append("</td>\n");
        sb.Append("<td style=\"padding-top:2mm;vertical-align:top;text-align:right;\">").Append(Text(brandName)).Append(" — service intake packet</td>\n");
        sb.Append("</tr>\n</table>\n");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Appends a <c>Label: value</c> row; returns <c>false</c> and appends nothing when the value is blank.</summary>
    private static bool AppendRow(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        sb.Append("<p class=\"row\" style=\"margin:0 0 1mm;\">").Append(label).Append(": <span>")
            .Append(Text(value)).Append("</span></p>\n");
        return true;
    }

    /// <summary>
    /// Appends a <c>Label: value</c> row whose value is a link to <paramref name="href"/>, or a
    /// plain row when <paramref name="href"/> is <c>null</c>; appends nothing when the value is blank.
    /// </summary>
    private static void AppendLinkRow(StringBuilder sb, string label, string? value, string? href)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (href is null)
        {
            AppendRow(sb, label, value);
            return;
        }

        sb.Append("<p class=\"row\" style=\"margin:0 0 1mm;\">").Append(label).Append(": <span><a href=\"")
            .Append(Attr(href)).Append("\">").Append(Text(value)).Append("</a></span></p>\n");
    }

    /// <summary>
    /// A <c>tel:</c> URI for <paramref name="phone"/>: its digits, keeping a leading <c>+</c>,
    /// with the display formatting dropped. <c>null</c> when there are no digits to dial.
    /// </summary>
    private static string? TelHref(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        return phone.TrimStart().StartsWith('+') ? $"tel:+{digits}" : $"tel:{digits}";
    }

    /// <summary>A <c>mailto:</c> URI for <paramref name="email"/>, or <c>null</c> when it is blank.</summary>
    private static string? MailtoHref(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : $"mailto:{email.Trim()}";

    /// <summary>
    /// The running-footer text baked into the <c>@page</c> margin box. CSS
    /// <c>content:</c> is a quoted string, so it must be ASCII and carry no <c>"</c> or
    /// <c>\</c>; the reference code and timestamp are already in that alphabet. The
    /// timestamp stays in it because its zone abbreviation comes from
    /// <see cref="Validation.DealershipTimeZones"/> rather than from
    /// <see cref="TimeZoneInfo"/>'s locale-dependent display names (issue #506).
    /// </summary>
    private static string RunningFooterText(string referenceCode, string received, string brandName)
    {
        var raw = $"Intake #{referenceCode} / {received} / {brandName}";
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

        .packet-title { font-size: 13pt; font-weight: 700; margin: 3mm 0 0; }
        h2 {
          font-size: 11pt;
          font-weight: 700;
          text-transform: uppercase;
          letter-spacing: 0.04em;
          margin: 0 0 2mm;
          padding-bottom: 1mm;
          border-bottom: 1px solid #000;
        }
        h2 .sub { text-transform: none; letter-spacing: 0; font-weight: 400; font-size: 9pt; }

        section, header { margin: 0 0 6mm; break-inside: avoid; }

        /* Masthead — IDS-style letterhead + top-right tracking number + 3-column band.
           Bands are presentational <table>s, not flex/grid: the same HTML is used as the
           packet delivery email body and mail clients drop flex/grid layout. */
        .masthead { border-bottom: 2px solid #000; padding-bottom: 3mm; }
        .masthead-top { width: 100%; border-collapse: collapse; }
        .masthead-top .brand { vertical-align: top; }
        .masthead-logo { height: 12mm; width: auto; vertical-align: middle; margin-right: 4mm; }
        .letterhead { font-size: 13pt; font-weight: 700; }
        .letterhead .doctype {
          display: block;
          font-size: 8.5pt;
          font-weight: 400;
          text-transform: uppercase;
          letter-spacing: 0.06em;
        }
        .refbox { text-align: right; white-space: nowrap; vertical-align: top; }
        .refbox .rvsno { margin: 0; font-size: 12pt; }
        .refbox .rvsno strong { font-size: 13pt; }
        .refbox .received { margin: 0.5mm 0 0; font-size: 9pt; }

        .idcols { width: 100%; border-collapse: collapse; margin-top: 3mm; }
        .idcols .col { vertical-align: top; font-size: 9.5pt; }
        .idcols .col.customer, .idcols .col.origin { padding-right: 6mm; }
        .idcols .col h2 { font-size: 8.5pt; margin-bottom: 1.5mm; }
        .idcols .col p { margin: 0 0 1mm; }
        .idcols .col .name { font-weight: 700; }
        .idcols .col .empty { font-style: italic; }

        .row, .name { margin: 0 0 1mm; }

        .description .verbatim,
        pre.dms-text {
          white-space: pre-wrap;
          word-wrap: break-word;
          font: 10pt/1.45 "Courier New", "Liberation Mono", monospace;
          padding: 0;
          margin: 0;
        }

        /* Copy & paste block keeps a frame — it is meant to be lifted into a DMS field,
           and the border marks its boundaries. The reported-issue block (issue #580) does not. */
        pre.dms-text {
          border: 1px solid #000;
          padding: 3mm;
        }

        /* The diagnostic Q&A is the block that must read as expert: bold questions and a
           left-rule accent per answer. Emphasis survives greyscale without a frame. */
        .diagnostics { margin-bottom: 8mm; }
        /* The answers are the customer's own words, so the Q&A shares the verbatim
           description's typewriter face (issue #735). Face only: the pre-wrap of the shared
           rule above would render the newlines between the <dt>/<dd> tags as blank lines. */
        .diagnostics dl {
          margin: 0;
          font-family: "Courier New", "Liberation Mono", monospace;
        }
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

        .ai-summary .tag,
        .curated-issue .tag {
          font-size: 8pt;
          font-weight: 700;
          text-transform: uppercase;
          border: 1px solid #000;
          padding: 0 1mm;
          vertical-align: middle;
        }

        .photo-line { margin: 0 0 1mm; }
        .photo-note { margin: 2mm 0 0; font-style: italic; }
        .photo-note a { color: #000; }

        .status-link a { color: #000; }

        .packet-foot {
          width: 100%;
          border-collapse: collapse;
          margin-top: 8mm;
          border-top: 1px solid #000;
          font-size: 8pt;
        }
        .packet-foot td { padding-top: 2mm; }

        @media print {
          body { font-size: 10pt; }
          a { text-decoration: underline; }
          .packet { padding: 0; }
          /* Manager-app status links are email chrome (issue #498) — never on paper. */
          .manager-actions { display: none; }
        }
        """;
}
