using Azure.Identity;

namespace RVS.Infra.AzCredentials;

public class CredentialService
{
    public CredentialService()
    {
            
    }

    public DefaultAzureCredential GetDefaultAzCredential()
    {
        DefaultAzureCredentialOptions credentialOptions = new DefaultAzureCredentialOptions();
        credentialOptions.ExcludeEnvironmentCredential = true;
        credentialOptions.ExcludeWorkloadIdentityCredential = true;
        credentialOptions.ExcludeManagedIdentityCredential = true; // TODO cleanup before GA
        credentialOptions.ExcludeVisualStudioCredential = false; // TODO cleanup before GA
        credentialOptions.ExcludeVisualStudioCodeCredential = true;
        credentialOptions.ExcludeAzureCliCredential = true;
        credentialOptions.ExcludeAzurePowerShellCredential = true;
        credentialOptions.ExcludeAzureDeveloperCliCredential = true;
        credentialOptions.ExcludeInteractiveBrowserCredential = true;
        DefaultAzureCredential defaultAzCredential = new DefaultAzureCredential(credentialOptions);

        return defaultAzCredential;
    }
}
