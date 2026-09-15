namespace RVS.API.Options;

/// <summary>
/// Credentials for the "RVS API Provisioner" Auth0 Machine-to-Machine application, which the
/// platform-admin tool uses to create users (Spec P-2 / P-7, issue #563). Bound from the
/// <c>Auth0Provisioner</c> section; in Azure the secret lives only in Key Vault
/// (<c>Auth0Provisioner--Domain</c>, <c>--ClientId</c>, <c>--ClientSecret</c>).
/// <para>
/// This is deliberately <b>not</b> the <c>Auth0Mgmt</c> section. <c>Auth0Mgmt--*</c> secrets in the
/// staging vault belong to the <c>rvs-config-automation</c> application used by the
/// <c>Infra/Auth0</c> scripts, and the API loads every secret in its vault into configuration.
/// Sharing the name would hand the API that application's broader configuration scopes.
/// </para>
/// </summary>
public sealed class Auth0ProvisionerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auth0Provisioner";

    /// <summary>The Auth0 database connection new users are created on.</summary>
    public const string DefaultConnection = "Username-Password-Authentication";

    /// <summary>Auth0 tenant domain, with or without scheme, e.g. <c>dev-2jhzz8xmjggh26pm.us.auth0.com</c>.</summary>
    public string Domain { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Database connection for new users.</summary>
    public string Connection { get; set; } = DefaultConnection;

    /// <summary>True when the domain, client id and client secret are all set.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Domain)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>The tenant origin with a trailing slash, e.g. <c>https://tenant.us.auth0.com/</c>.</summary>
    public Uri BaseUri
    {
        get
        {
            var domain = Domain.Trim();
            if (!domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                domain = "https://" + domain;
            }

            return new Uri(domain.TrimEnd('/') + "/");
        }
    }
}
