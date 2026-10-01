namespace RVS.Domain.Validation;

/// <summary>
/// Controlled vocabulary for the distribution channel a customer reached intake through
/// (<c>Spec A-13</c>, issue #599). Every distribution path routes through the
/// <c>go.rvintake.com</c> redirect, which carries the channel in a <c>src</c> query parameter.
///
/// The list is open on purpose. A new channel can be printed on a sticker or pasted into a
/// campaign long before it is added here, and the redirect must never fail on a value it does
/// not recognise — so <see cref="Normalize"/> keeps any well-formed value and only coerces the
/// malformed ones. <see cref="IsKnown"/> is the narrow test, for reporting that wants to
/// separate the tabled channels from everything else.
/// </summary>
public static class IntakeSourceVocabulary
{
    /// <summary>Sent via long-press on a Recents entry plus the Text Replacement keyboard snippet.</summary>
    public const string TextReplacement = "textrepl";

    /// <summary>Sent via iOS "Respond with Text" / Android Quick Response, from an incoming call.</summary>
    public const string QuickReply = "quickreply";

    /// <summary>Scanned from the QR sticker or NFC tag.</summary>
    public const string Qr = "qr";

    /// <summary>
    /// Printed material — business cards, invoices, counter signage. Printed URLs cannot carry a
    /// query string, so this is the value assumed when <c>src</c> is absent rather than one
    /// anybody ever supplies explicitly.
    /// </summary>
    public const string Print = "print";

    /// <summary>
    /// An advisor-sent intake invite (<c>Spec A-14</c>, issue #663): the single-use link an
    /// advisor texts to a caller from the manager app, or opens for themselves with
    /// <i>Fill it in myself</i>.
    /// </summary>
    public const string Advisor = "advisor";

    /// <summary>
    /// The location's link copied out of the manager app's <i>Send intake link</i> dialog
    /// (issue #756) and pasted wherever the advisor sends it — their own text, a chat, an email.
    /// Not <see cref="TextReplacement"/>: that is a tech texting the link back from their phone.
    /// Not <see cref="Advisor"/> either: this is the shared location link, not a single-use invite.
    /// </summary>
    public const string ManagerApp = "mgrapp";

    /// <summary>
    /// Bucket for a supplied <c>src</c> that is not a usable token — too long, or carrying
    /// characters that have no business in a channel tag. Never fails the redirect; the hit is
    /// simply recorded here.
    /// </summary>
    public const string Other = "other";

    /// <summary>Maximum length of a stored source value.</summary>
    public const int MaxLength = 32;

    /// <summary>
    /// The tabled channels, in <c>Spec A-13</c> order, then A-14's advisor invite, then the link
    /// copied from the manager app.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownValues = [TextReplacement, QuickReply, Qr, Print, Advisor, ManagerApp];

    /// <summary>
    /// Canonicalises a raw <c>src</c> query value into a storable channel tag: trimmed and
    /// lower-cased, <see cref="Print"/> when absent, <see cref="Other"/> when malformed.
    /// Never throws — an unusable value costs the hit its channel, never the redirect.
    /// </summary>
    /// <param name="value">The raw <c>src</c> query parameter, or <c>null</c> when not supplied.</param>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Print;
        }

        var candidate = value.Trim().ToLowerInvariant();

        return IsWellFormed(candidate) ? candidate : Other;
    }

    /// <summary>
    /// Whether <paramref name="value"/> is one of the <see cref="KnownValues"/> (case-insensitive,
    /// surrounding whitespace ignored). An unknown-but-accepted channel returns <c>false</c>.
    /// </summary>
    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && KnownValues.Contains(value.Trim().ToLowerInvariant());

    /// <summary>
    /// A usable channel tag: 1–<see cref="MaxLength"/> characters of lowercase letters, digits,
    /// hyphen and underscore, starting with a letter or digit. Deliberately strict — the value is
    /// echoed into a redirect URL, stored as part of a Table Storage row, and grouped on in
    /// reporting.
    /// </summary>
    private static bool IsWellFormed(string candidate)
    {
        if (candidate.Length is 0 or > MaxLength)
        {
            return false;
        }

        if (!char.IsAsciiLetterLower(candidate[0]) && !char.IsAsciiDigit(candidate[0]))
        {
            return false;
        }

        foreach (var c in candidate)
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
