namespace RVS.Domain.Packets;

/// <summary>
/// The one place the packet email's link into the authenticated manager app is shaped
/// (<c>Spec C-7</c>, issues #498, #743). The API uses <see cref="Build"/> to put it in the email.
///
/// <para>Route shape: <c>{base}/sr/{id}</c> opens the request, where every C-3 status is one tap
/// away. The link carries no token and is a plain navigation into a signed-in SPA — a write only
/// happens when a signed-in manager taps, so a mail-security scanner that fetches every link
/// changes no state and there is no anonymous status-write surface.</para>
///
/// <para>The email used to carry one link per status as well (<c>?action=…</c>). Issue #743
/// dropped them: the request page offers every status, so they only duplicated it.</para>
/// </summary>
public static class ManagerDeepLinks
{
    /// <summary>
    /// Builds the manager-app link for one service request.
    /// </summary>
    /// <param name="managerAppBaseUrl">Origin of the manager app, e.g. <c>https://manager.rvintake.com</c>.</param>
    /// <param name="serviceRequestId">The service request id.</param>
    /// <returns>The link, or <c>null</c> when the base URL is blank or not http(s) — the email then goes out without it.</returns>
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

        return new PacketManagerLinks
        {
            RequestUrl = $"{baseUrl}/sr/{Uri.EscapeDataString(serviceRequestId)}",
        };
    }
}
