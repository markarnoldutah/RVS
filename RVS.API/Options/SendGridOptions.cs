namespace RVS.API.Options;

/// <summary>
/// SendGrid credentials. Bound from <c>SendGrid</c>. In Azure the key comes from Key Vault
/// (<c>SendGrid--ApiKey</c>); locally from <c>dotnet user-secrets</c>. With no key, email
/// registration falls back to <c>NoOpNotificationService</c>.
/// </summary>
public sealed class SendGridOptions
{
    /// <summary>Configuration section this binds from.</summary>
    public const string SectionName = "SendGrid";

    /// <summary>A restricted API key with the Mail Send permission only.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
