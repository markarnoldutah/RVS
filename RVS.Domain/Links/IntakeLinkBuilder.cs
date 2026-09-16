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
    public static string ShortLink(string redirectBaseUrl, string slug, string? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalized = IntakeSourceVocabulary.Normalize(source);
        var bare = $"{redirectBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(slug.Trim().ToLowerInvariant())}";

        return normalized == IntakeSourceVocabulary.Print
            ? bare
            : $"{bare}?src={Uri.EscapeDataString(normalized)}";
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
    public static string IntakeUrl(string intakeBaseUrl, string slug, string? source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intakeBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalized = IntakeSourceVocabulary.Normalize(source);

        return $"{intakeBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(slug.Trim().ToLowerInvariant())}?src={Uri.EscapeDataString(normalized)}";
    }
}
