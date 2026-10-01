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

    /// <summary>
    /// Origin of the channel-tagging redirect, e.g. <c>https://go.rvintake.com</c>
    /// (<c>Spec A-13</c>, issue #599). Every link handed to a customer — QR sticker, texted
    /// link, printed card — is built against this so the hit is logged and the channel is
    /// observed before the customer reaches the intake form.
    ///
    /// Falls back to <see cref="BaseUrl"/> when unset, so an environment with no <c>go</c> host
    /// bound still hands out working links; they simply arrive untagged. Use
    /// <see cref="RedirectOrIntakeBaseUrl"/> rather than reading this directly.
    /// </summary>
    public string RedirectBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The origin customer-facing links should be built against: the redirect host when one is
    /// configured, the Intake app itself otherwise. Never has a trailing slash.
    /// </summary>
    public string RedirectOrIntakeBaseUrl =>
        (string.IsNullOrWhiteSpace(RedirectBaseUrl) ? BaseUrl : RedirectBaseUrl).TrimEnd('/');
}
