namespace RVS.API.Options;

/// <summary>
/// Outbound email identity. Bound from <c>Email</c>. In Azure, Bicep injects both values per
/// environment (<c>app-service-config.bicep</c>); locally they come from
/// <c>appsettings.Development.json</c>.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>Configuration section this binds from.</summary>
    public const string SectionName = "Email";

    /// <summary>
    /// The sender address, e.g. <c>DoNotReply@mail.rvintake.com</c>. No default, on purpose: the
    /// sending domain is per environment and must be authenticated in SendGrid, so any hardcoded
    /// value is wrong somewhere.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// The display name on the From line, which the service advisor reads. Staging overrides it to
    /// <c>RV Intake [Staging]</c> through Bicep (#828); prod keeps this default.
    /// </summary>
    public string SenderDisplayName { get; set; } = "RV Intake";
}
