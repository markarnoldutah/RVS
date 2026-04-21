namespace RVS.API.Options;

/// <summary>
/// Configuration for the public Intake app base URL.
/// Bound from the <c>Intake</c> section of <c>appsettings.json</c>.
/// Used to build location QR-code intake URLs and any other server-rendered
/// links that point the customer at the Intake SPA.
/// </summary>
public sealed class IntakeUrlOptions
{
    /// <summary>
    /// Origin (scheme + host) of the Intake app, e.g. <c>https://rvintake.com</c>.
    /// Trailing slash is tolerated — callers should trim before composing paths.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
