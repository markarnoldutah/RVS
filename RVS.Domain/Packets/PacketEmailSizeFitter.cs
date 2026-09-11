using System.Text;
using RVS.Domain.Integrations;

namespace RVS.Domain.Packets;

/// <summary>
/// Trims a packet email's attachment set to what Azure Communication Services will actually
/// accept, so an oversized submission delivers a smaller packet instead of no packet
/// (<c>Spec B-4</c>, issue #521).
///
/// <para><b>Why this exists.</b> ACS caps the <i>whole</i> send request — bodies, headers and
/// attachments together — at <see cref="AcsMaxRequestBytes"/>, and attachments travel base64
/// encoded, which inflates them by about a third. Microsoft's own guidance puts the realistic
/// payload of raw attachment bytes at roughly 7.5 MB. <c>Spec A-6</c> lets a customer upload
/// ten 25 MB files, so six full-resolution phone photos attached as originals exceed the cap on
/// their own — six real 12 MP photos measured about 14.9 MB before base64. Before this, ACS
/// rejected the send, all three delivery attempts failed the same way, and the shop got a service
/// request with no packet. The packet PDF is not the problem: QuestPDF resamples embedded images
/// to their placed size, so the PDF is roughly 1.5–3 MB for six to ten detailed photos regardless
/// of source resolution (measured with real photos: 1.60 MB for six, 2.83 MB for ten).</para>
///
/// <para><b>What gets dropped, in order.</b> The PDF outranks every photo — it is the printable
/// artifact and the reason the email exists, so it is decided first. Photos then fill what is
/// left as a <i>contiguous prefix</i> of the order they arrive in, which is the same order the
/// packet renders them in, so the photos on page one of the packet are the photos that ship. A
/// PDF that cannot fit is dropped and the photos still fill the budget. That should be
/// impossible — even a ten-photo PDF is about 3.8 MB on the wire, and
/// <c>PacketEmailBudgetValidator</c> keeps the budget at 5 MB or more — so it signals a
/// regression, and the photos that fit are still worth more to the reader than a bare
/// email.</para>
///
/// <para><b>A dropped photo is still reachable.</b> The packet HTML body lists every photo by
/// file name (issue <c>#580</c>) but no longer embeds any of them, so a dropped attachment is
/// no longer visible inline the way it was before that change. When a photo is dropped, the
/// caller re-renders the body with a note pointing the reader at the Manager app, where the
/// packet's stored photos remain reachable; the PDF remains downloadable there too.</para>
///
/// <para>A pure transform: it reads only its arguments, mutates nothing, and returns the same
/// result for the same input. An oversized input is the case it exists to handle, so it is not
/// an error — only a malformed call throws.</para>
/// </summary>
public static class PacketEmailSizeFitter
{
    /// <summary>
    /// ACS's hard cap on one email request including base64-encoded attachments: 10 MB.
    /// Exceeding it fails the send outright, which is what issue #521 observed.
    /// </summary>
    public const long AcsMaxRequestBytes = 10_000_000;

    /// <summary>
    /// Default ceiling this fitter aims at — <see cref="AcsMaxRequestBytes"/> less a 500 KB
    /// margin. The margin absorbs what cannot be measured exactly here: JSON escaping of the
    /// HTML body, MIME headers, and the request envelope ACS adds around our content. Override
    /// it per environment through the <c>PacketEmail</c> configuration section.
    /// </summary>
    public const long DefaultMaxRequestBytes = 9_500_000;

    /// <summary>
    /// Bytes reserved for the parts of the request this transform cannot see: the subject,
    /// recipient list, sender address, and JSON scaffolding.
    /// </summary>
    private const long EnvelopeOverheadBytes = 4 * 1024;

    /// <summary>
    /// Bytes charged per attachment on top of its encoded content — the file name, content
    /// type, and the JSON object around them.
    /// </summary>
    private const long PerAttachmentOverheadBytes = 256;

    /// <summary>
    /// Chooses which of <paramref name="candidates"/> can be attached within
    /// <paramref name="maxRequestBytes"/>.
    /// </summary>
    /// <param name="candidates">
    /// The attachments the location's configuration asked for — the PDF and/or the original
    /// photos, in the order the packet renders them. <c>null</c> is treated as none.
    /// </param>
    /// <param name="htmlBody">The rendered packet HTML that will be the email's HTML body.</param>
    /// <param name="plainTextBody">The paste-block plain-text alternative.</param>
    /// <param name="maxRequestBytes">
    /// The total request budget, base64 included. Defaults to <see cref="DefaultMaxRequestBytes"/>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="htmlBody"/> or <paramref name="plainTextBody"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRequestBytes"/> is not positive.</exception>
    public static PacketEmailFitResult Fit(
        IReadOnlyList<PacketEmailAttachment>? candidates,
        string htmlBody,
        string plainTextBody,
        long maxRequestBytes = DefaultMaxRequestBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(plainTextBody);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRequestBytes);

        var bodyBytes = Encoding.UTF8.GetByteCount(htmlBody)
            + Encoding.UTF8.GetByteCount(plainTextBody)
            + EnvelopeOverheadBytes;

        if (candidates is null || candidates.Count == 0)
        {
            return new PacketEmailFitResult
            {
                Attachments = [],
                Dropped = [],
                EstimatedRequestBytes = bodyBytes,
                PdfDropped = false,
            };
        }

        // Selection is tracked by index, never by value: two photos of the same size and name
        // are equal as records, and an attachment must not stand in for its twin.
        var keptIndices = new HashSet<int>();
        var used = bodyBytes;

        // The PDF is decided first and independently of where it sits in the input, so input
        // order can never cost the packet its printable artifact.
        var pdfIndex = IndexOfPdf(candidates);
        var pdfKept = false;

        if (pdfIndex >= 0)
        {
            var pdfCost = CostOf(candidates[pdfIndex]);
            if (used + pdfCost <= maxRequestBytes)
            {
                used += pdfCost;
                keptIndices.Add(pdfIndex);
                pdfKept = true;
            }
        }

        // Photos fill whatever the PDF left — or the whole budget when the PDF could not fit. A
        // dropped PDF should be impossible (even a ten-photo PDF is under 4 MB on the wire and startup
        // validation guarantees room for it), but if a regression ever makes it large, the photos
        // that fit are still worth more to the reader than a bare email.
        for (var i = 0; i < candidates.Count; i++)
        {
            if (i == pdfIndex)
            {
                continue;
            }

            var cost = CostOf(candidates[i]);
            if (used + cost > maxRequestBytes)
            {
                // Stop at the first photo that does not fit: survivors stay a contiguous prefix,
                // which is predictable for the reader and matches the packet's own page-one
                // ordering. Packing a later, smaller photo into the gap would not.
                break;
            }

            used += cost;
            keptIndices.Add(i);
        }

        // Preserve the caller's order in both sets — the orchestrator built it to match the
        // packet, and the reader sees attachments in the order we hand them over.
        var kept = new List<PacketEmailAttachment>(keptIndices.Count);
        var dropped = new List<PacketEmailAttachment>(candidates.Count - keptIndices.Count);
        for (var i = 0; i < candidates.Count; i++)
        {
            (keptIndices.Contains(i) ? kept : dropped).Add(candidates[i]);
        }

        return new PacketEmailFitResult
        {
            Attachments = kept,
            Dropped = dropped,
            EstimatedRequestBytes = used,
            PdfDropped = pdfIndex >= 0 && !pdfKept,
        };
    }

    /// <summary>
    /// What one attachment costs the request: its base64-encoded length plus per-attachment
    /// JSON overhead. Base64 emits four characters per three bytes, padded up.
    /// </summary>
    private static long CostOf(PacketEmailAttachment attachment) =>
        (4L * ((attachment.Content.Length + 2) / 3)) + PerAttachmentOverheadBytes;

    /// <summary>
    /// Index of the packet PDF in <paramref name="candidates"/>, or <c>-1</c>. Only the first
    /// PDF is privileged; a second one would be treated as an ordinary attachment.
    /// </summary>
    private static int IndexOfPdf(IReadOnlyList<PacketEmailAttachment> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// The outcome of <see cref="PacketEmailSizeFitter.Fit"/> — what to attach, what was left off,
/// and what the request is expected to weigh.
/// </summary>
public sealed record PacketEmailFitResult
{
    /// <summary>The attachments that fit, in the order they were supplied.</summary>
    public required IReadOnlyList<PacketEmailAttachment> Attachments { get; init; }

    /// <summary>
    /// The attachments left off to stay inside the budget. Each is still reachable: photos are
    /// inline in the HTML body by SAS URL, and the PDF is stored in blob storage.
    /// </summary>
    public required IReadOnlyList<PacketEmailAttachment> Dropped { get; init; }

    /// <summary>
    /// Estimated total request size for <see cref="Attachments"/> plus both bodies, base64
    /// included.
    ///
    /// Within the budget whenever anything could be attached. It can still exceed the budget in
    /// the one case the fitter cannot fix: bodies that overrun it on their own, with every
    /// attachment already dropped. The send is attempted anyway — a packet HTML body that large
    /// is a rendering problem, and failing quietly here would hide it.
    /// </summary>
    public required long EstimatedRequestBytes { get; init; }

    /// <summary>
    /// <c>true</c> when a PDF was offered but could not fit — the packet email goes out without
    /// its printable artifact, though any photos that fit are still attached. Even a ten-photo PDF
    /// is about 3.8 MB on the wire and startup validation keeps the budget at 5 MB or more, so this
    /// points at
    /// a regression — the renderer stopped resampling, or the HTML body grew — rather than at
    /// large photos, and is worth an operator's attention.
    /// </summary>
    public required bool PdfDropped { get; init; }

    /// <summary><c>true</c> when anything was left off the email.</summary>
    public bool AnythingDropped => Dropped.Count > 0;
}
