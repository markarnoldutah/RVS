using RVS.Domain.Security;
using RVS.Domain.Validation;

namespace RVS.Domain.Links;

/// <summary>
/// Builds the customer-facing links for a location (<c>Spec A-13</c>, issue #599).
///
/// One place, on purpose. Every distribution path is supposed to route through
/// <c>go.rvintake.com</c> so that the hit is logged and the channel observed — a link composed
/// anywhere else is a channel that silently stops being measured. Keeping composition here is
/// what makes "did we repoint everything?" a question with an answer.
/// </summary>
public static class IntakeLinkBuilder
{
    /// <summary>
    /// The short link to hand to a customer: <c>{redirectBaseUrl}/{slug}</c>, with
    /// <c>?src={source}</c> when the channel can carry one.
    ///
    /// A <paramref name="source"/> of <c>print</c> — or none at all — produces a bare URL with
    /// no query string. That is not a shortcut: printed material cannot carry a query string in
    /// the first place, which is exactly why an absent <c>src</c> is recorded as print. Keeping
    /// the printed URL short also keeps it typeable off a business card.
    /// </summary>
    /// <param name="redirectBaseUrl">Origin of the redirect host, e.g. <c>https://go.rvintake.com</c>.</param>
    /// <param name="slug">The location's intake slug.</param>
    /// <param name="source">Channel tag, or <c>null</c> for print.</param>
    /// <param name="invite">
    /// An A-14 invite token (<c>Spec A-14</c>, issue #663), appended as <c>inv</c>. Must be
    /// well-formed: this link is RVS's own composition, so a malformed token is a bug.
    /// </param>
    public static string ShortLink(string redirectBaseUrl, string slug, string? source = null, string? invite = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        if (invite is not null && !InviteToken.IsWellFormed(invite))
        {
            throw new ArgumentException("Not a well-formed invite token.", nameof(invite));
        }

        var normalized = IntakeSourceVocabulary.Normalize(source);
        var bare = $"{redirectBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(slug.Trim().ToLowerInvariant())}";

        var query = new List<string>(2);
        if (normalized != IntakeSourceVocabulary.Print)
        {
            query.Add($"src={Uri.EscapeDataString(normalized)}");
        }

        if (invite is not null)
        {
            query.Add($"inv={invite}");
        }

        return query.Count == 0 ? bare : $"{bare}?{string.Join('&', query)}";
    }

    /// <summary>
    /// The intake-app URL a redirect sends the customer to:
    /// <c>{intakeBaseUrl}/{slug}?src={source}</c>. The channel is always spelled out here —
    /// including <c>print</c> — because the intake app forwards it back on submission, and an
    /// absent parameter would be indistinguishable from a link that never passed the redirect.
    /// </summary>
    /// <param name="intakeBaseUrl">Origin of the Intake app, e.g. <c>https://rvintake.com</c>.</param>
    /// <param name="slug">The location's intake slug.</param>
    /// <param name="source">Channel tag; normalised, so <c>null</c> becomes print.</param>
    /// <param name="invite">
    /// An A-14 invite token from the short link's <c>inv</c> (<c>Spec A-14</c>, issue #663),
    /// passed through as <c>inv</c>. A missing or malformed value is dropped, never an error:
    /// the redirect never fails, and a mangled invite lands on a blank intake form.
    /// </param>
    public static string IntakeUrl(string intakeBaseUrl, string slug, string? source, string? invite = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intakeBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalized = IntakeSourceVocabulary.Normalize(source);
        var url = $"{intakeBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(slug.Trim().ToLowerInvariant())}?src={Uri.EscapeDataString(normalized)}";

        return InviteToken.IsWellFormed(invite) ? $"{url}&inv={invite}" : url;
    }
}
