namespace RVS.API.Options;

/// <summary>
/// Outbound SMS configuration (issue #661). Bound from <c>AzureCommunicationServices:Sms</c>.
/// In Azure, Bicep injects <c>Enabled</c> and <c>FromPhoneNumber</c> per environment
/// (<c>app-service-config.bicep</c>), the same way it injects email's <c>FromAddress</c>.
/// </summary>
public sealed class SmsOptions
{
    /// <summary>Configuration section this binds from.</summary>
    public const string SectionName = "AzureCommunicationServices:Sms";

    /// <summary>
    /// Master switch. Off by default, and stays off in an environment until its number has
    /// cleared toll-free verification: an unverified number's sends are rejected by the carrier.
    /// While off, <c>NoOpSmsNotificationService</c> is registered and no ACS SMS call is made,
    /// whether or not an ACS endpoint is configured.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The E.164 number the ACS resource owns, e.g. <c>+18662319618</c>. No default, on purpose:
    /// each environment's ACS resource owns a different number, so any hardcoded value is wrong
    /// somewhere. Required when <see cref="Enabled"/> is <c>true</c>.
    /// </summary>
    public string FromPhoneNumber { get; set; } = string.Empty;

    /// <summary>Outbound SMS allowed per tenant in any rolling hour.</summary>
    public int MaxMessagesPerTenantPerHour { get; set; } = 100;
}
