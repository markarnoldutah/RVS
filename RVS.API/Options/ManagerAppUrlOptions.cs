namespace RVS.API.Options;

/// <summary>
/// Configuration for the authenticated Manager app base URL.
/// Bound from the <c>ManagerApp</c> section of <c>appsettings.json</c>.
/// Used to build deep links back into the Manager SPA from server-rendered content, such as
/// the packet email's note pointing a service manager at a photo the email could not attach
/// (<c>PacketEmailSizeFitter</c>, issue <c>#580</c>).
/// </summary>
public sealed class ManagerAppUrlOptions
{
    /// <summary>
    /// Origin (scheme + host) of the Manager app, e.g. <c>https://manager.rvserviceflow.com</c>.
    /// Trailing slash is tolerated — callers should trim before composing paths.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
