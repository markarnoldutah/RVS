using System.Diagnostics.CodeAnalysis;

namespace RVS.Domain.Packets;

/// <summary>
/// The one place the packet email's deep links into the authenticated manager app are shaped
/// and parsed (<c>Spec C-7</c>, issue #498). The API uses <see cref="Build"/> to put them in the
/// email; the manager app uses <see cref="TryFindAction"/> to read the <c>action</c> query
/// parameter back, so the two sides cannot drift apart.
///
/// <para>Route shape: <c>{base}/sr/{id}</c> opens the request; <c>{base}/sr/{id}?action={slug}</c>
/// lands on a one-tap confirm for that status. The links carry no token and are plain
/// navigations into a signed-in SPA — the write only happens when a signed-in manager taps
/// confirm, so a mail-security scanner that fetches every link changes no state and there is no
/// anonymous status-write surface.</para>
///
/// <para>Only the forward, non-destructive statuses are offered from email. <c>Cancelled</c>,
/// <c>New</c>, and <c>Waiting on Customer</c> stay in the manager app's full status control.</para>
/// </summary>
public static class ManagerDeepLinks
{
    /// <summary>Query-string parameter that names the status action to pre-open.</summary>
    public const string ActionQueryParameter = "action";

    /// <summary>The status actions the packet email offers, in the order they are shown.</summary>
    public static IReadOnlyList<ManagerDeepLinkAction> Actions { get; } =
    [
        new("in-progress", "InProgress", "In Progress"),
        new("waiting-on-parts", "WaitingOnParts", "Waiting on Parts"),
        new("completed", "Completed", "Completed"),
    ];

    /// <summary>
    /// Builds the manager-app links for one service request.
    /// </summary>
    /// <param name="managerAppBaseUrl">Origin of the manager app, e.g. <c>https://manager.rvserviceflow.com</c>.</param>
    /// <param name="serviceRequestId">The service request id.</param>
    /// <returns>The links, or <c>null</c> when the base URL is blank or not http(s) — the email then goes out without them.</returns>
    /// <exception cref="ArgumentException"><paramref name="serviceRequestId"/> is blank.</exception>
    public static PacketManagerLinks? Build(string? managerAppBaseUrl, string serviceRequestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);

        var baseUrl = managerAppBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        var requestUrl = $"{baseUrl}/sr/{Uri.EscapeDataString(serviceRequestId)}";

        return new PacketManagerLinks
        {
            RequestUrl = requestUrl,
            Actions = Actions
                .Select(a => new PacketManagerActionLink
                {
                    Label = a.Label,
                    Status = a.Status,
                    Url = $"{requestUrl}?{ActionQueryParameter}={a.Slug}",
                })
                .ToList(),
        };
    }

    /// <summary>
    /// Resolves an <c>action</c> query-string value to its status action. Case-insensitive and
    /// whitespace-tolerant; anything outside <see cref="Actions"/> is rejected.
    /// </summary>
    public static bool TryFindAction(string? slug, [NotNullWhen(true)] out ManagerDeepLinkAction? action)
    {
        var trimmed = slug?.Trim();
        action = string.IsNullOrEmpty(trimmed)
            ? null
            : Actions.FirstOrDefault(a => string.Equals(a.Slug, trimmed, StringComparison.OrdinalIgnoreCase));
        return action is not null;
    }
}

/// <summary>One status action offered by a manager-app deep link.</summary>
/// <param name="Slug">The <c>action</c> query-string value, e.g. <c>in-progress</c>.</param>
/// <param name="Status">The stored C-3 status value, e.g. <c>InProgress</c>.</param>
/// <param name="Label">The display label, e.g. <c>In Progress</c>.</param>
public sealed record ManagerDeepLinkAction(string Slug, string Status, string Label);
