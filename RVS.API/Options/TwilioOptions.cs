namespace RVS.API.Options;

/// <summary>
/// Twilio Programmable Messaging configuration. Bound from <c>Twilio</c>. Each environment is its
/// own Twilio subaccount with one toll-free number in one Messaging Service. In Azure, the SIDs,
/// key secret and auth token come from Key Vault (<c>Twilio--*</c>) and
/// <see cref="MessagingServiceSid"/> / <see cref="WebhookBaseUrl"/> from Bicep app settings.
/// </summary>
public sealed class TwilioOptions
{
    /// <summary>Configuration section this binds from.</summary>
    public const string SectionName = "Twilio";

    /// <summary>The subaccount SID (<c>AC…</c>). Part of every REST path.</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>The API key SID (<c>SK…</c>) RVS sends with. Basic-auth user name.</summary>
    public string ApiKeySid { get; set; } = string.Empty;

    /// <summary>The API key secret. Basic-auth password.</summary>
    public string ApiKeySecret { get; set; } = string.Empty;

    /// <summary>
    /// The subaccount's auth token. Used only to verify <c>X-Twilio-Signature</c> on inbound
    /// webhooks: Twilio signs with the auth token, never with an API key. With no token, the
    /// webhook refuses every request.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// The Messaging Service (<c>MG…</c>) whose sender pool holds the number. Sending through it
    /// is what applies Advanced Opt-Out, which answers STOP, START and HELP.
    /// </summary>
    public string MessagingServiceSid { get; set; } = string.Empty;

    /// <summary>
    /// The public origin Twilio calls back on, e.g. <c>https://api.rvserviceflow.com</c>. It is
    /// configured rather than read from the request because App Service terminates TLS in front
    /// of the app, and Twilio signs the exact URL it called. Also builds each send's
    /// <c>StatusCallback</c>.
    /// </summary>
    public string WebhookBaseUrl { get; set; } = string.Empty;

    /// <summary>Whether the credentials needed to send are all present.</summary>
    public bool CanSend =>
        !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(ApiKeySid)
        && !string.IsNullOrWhiteSpace(ApiKeySecret);
}
