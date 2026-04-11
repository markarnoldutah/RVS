// ──────────────────────────────────────────────────────────────
// Main Orchestration – RVS Azure Infrastructure
// ──────────────────────────────────────────────────────────────
// Deploys at subscription scope to manage two resource groups:
//   • rg-rvs-{env}-westus3   — GPT-4o OpenAI, ACS, Storage, Key Vault
//   • rg-rvs-{env}-ncus      — Whisper OpenAI (northcentralus)
// ──────────────────────────────────────────────────────────────
targetScope = 'subscription'

// ── Parameters ────────────────────────────────────────────────

@description('The Azure region for the primary OpenAI resources (GPT-4o).')
param location string = 'westus3'

@description('The Azure region for Whisper STT. Whisper 001 Standard is not available in all regions.')
param whisperLocation string = 'northcentralus'

@description('The target environment (dev, staging, or prod).')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string = 'dev'

@description('Name of the primary resource group (GPT-4o + Key Vault).')
param primaryResourceGroupName string = 'rg-rvs-${environmentName}-westus3'

@description('Name of the Whisper resource group (northcentralus).')
param whisperResourceGroupName string = 'rg-rvs-${environmentName}-ncus'

@description('GPT-4o deployment capacity in thousands of tokens per minute (K TPM). Dev = 1, Staging = 10, Prod = 30+.')
@minValue(1)
param openAiCapacity int = 1

@description('Whisper deployment capacity in thousands of tokens per minute (K TPM). Dev = 1.')
@minValue(1)
param whisperCapacity int = 1

@description('Optional. Name of an existing Key Vault in the primary resource group to store OpenAI secrets. Leave empty to skip secret creation.')
param keyVaultName string = ''

@description('Optional. Name of the model deployment used for text workloads (issue text refinement, category suggestion). Defaults to the standard gpt-4o deployment name when both workloads share the same deployment.')
param textDeploymentName string = 'gpt-4o'

@description('When true, deploys a general-purpose v2 storage account (Standard_LRS) into the primary resource group. Set to false for production until a production storage design is finalized.')
param deployStorageAccount bool = false

@description('Override the storage account name. Leave empty to use the computed default (strvs<env>wus3001). Must be 3-24 lowercase alphanumeric characters and globally unique.')
@maxLength(24)
param storageAccountNameOverride string = ''

@description('Principal ID (object ID) of the App Service / Container App managed identity that needs blob access. Leave empty to skip role assignments.')
param storageBlobAccessPrincipalId string = ''

@description('Override CORS origins for browser-based SAS uploads to Blob Storage. Leave empty to use environment defaults (dev = localhost ports, prod = portal.rvserviceflow.com).')
param storageCorsAllowedOrigins string[] = []

@description('When true, deploys an Azure Communication Services resource with Email and SMS capabilities into the primary resource group.')
param deployAcs bool = false

@description('ACS data residency location. Must be a valid ACS data location.')
param acsDataLocation string = 'United States'

@description('When true, deploys Azure Static Web App resources for RVS.Blazor.Intake and RVS.Blazor.Manager.')
param deploySwa bool = false

@description('Azure region for Static Web Apps. Must be an SWA-supported region: westus2, eastus2, centralus, westeurope, or eastasia.')
param swaLocation string = 'westus2'

@description('Name of the dedicated resource group for Static Web App resources.')
param swaResourceGroupName string = 'rg-rvs-${environmentName}-westus2'

@description('SWA SKU tier. Free for dev/test; Standard for staging/production.')
@allowed([
  'Free'
  'Standard'
])
param swaSkuName string = 'Free'

@description('When true, provisions the Azure DNS zone and SWA CNAME records in the DNS resource group. Requires deploySwa = true.')
param deployDns bool = false

@description('Public DNS zone apex domain.')
param domainName string = 'rvserviceflow.com'

@description('Resource group that owns the DNS zone. The apex zone is shared across environments — only the subdomain prefix changes per env. Defaults to the prod primary resource group.')
param dnsResourceGroupName string = 'rg-rvs-prod-westus3'

@description('Subdomain prefix for the Intake SWA CNAME record. Defaults to "intake" for prod and "intake-<env>" for all other environments.')
param intakeDnsPrefix string = environmentName == 'prod' ? 'intake' : 'intake-${environmentName}'

@description('Subdomain prefix for the Manager SWA CNAME record. Defaults to "manager" for prod and "manager-<env>" for all other environments.')
param managerDnsPrefix string = environmentName == 'prod' ? 'manager' : 'manager-${environmentName}'

// ── Variables ─────────────────────────────────────────────────

// Storage account names must be 3-24 lowercase alphanumeric characters with no hyphens.
// Default follows the pattern: st + rvs + <env> + wus3 + 001 (e.g. strvsdevwus3001).
var defaultStorageAccountName = 'strvs${environmentName}wus3001'
var resolvedStorageAccountName = empty(storageAccountNameOverride)
  ? defaultStorageAccountName
  : storageAccountNameOverride

// Environment-aware default CORS origins for browser-based SAS uploads.
// Dev/staging use local Blazor WASM ports; prod uses the live portal URL.
var defaultCorsOrigins = environmentName == 'prod'
  ? ['https://portal.rvserviceflow.com']
  : [
      'https://localhost:7008'
      'https://localhost:7116'
      'https://localhost:7200'
      'https://localhost:7300'
    ]

var resolvedCorsOrigins = !empty(storageCorsAllowedOrigins) ? storageCorsAllowedOrigins : defaultCorsOrigins

// SWA resource names follow the pattern from PR #280: stapp-rvs-{app}-{env}
var swaIntakeName = 'stapp-rvs-intake-${environmentName}'
var swaManagerName = 'stapp-rvs-manager-${environmentName}'

// Shared tags for SWA resources.
var swaTags = {
  Application: 'rvs'
  Environment: environmentName
  ManagedBy: 'Bicep'
}

// ── Resource Groups

resource rgPrimary 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: primaryResourceGroupName
  location: location
  tags: {
    Application: 'rvs'
    Environment: environmentName
    ManagedBy: 'Bicep'
  }
}

resource rgWhisper 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: whisperResourceGroupName
  location: whisperLocation
  tags: {
    Application: 'rvs'
    Environment: environmentName
    ManagedBy: 'Bicep'
  }
}

resource rgSwa 'Microsoft.Resources/resourceGroups@2024-07-01' = if (deploySwa) {
  name: swaResourceGroupName
  location: swaLocation
  tags: {
    Application: 'rvs'
    Environment: environmentName
    ManagedBy: 'Bicep'
  }
}

// ── Modules ───────────────────────────────────────────────────

// -- GPT-4o (primary region: westus3) --

module openAiNaming 'modules/naming-tags.bicep' = {
  name: 'deploy-openai-naming-${environmentName}'
  scope: rgPrimary
  params: {
    resourceTypePrefix: 'oai'
    appName: 'rvs'
    workload: 'ai'
    environmentName: environmentName
    location: location
  }
}

module openAi 'modules/openai.bicep' = {
  name: 'deploy-openai-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    environmentName: environmentName
    tags: openAiNaming.outputs.tags
    deploymentCapacity: openAiCapacity
    resourceName: openAiNaming.outputs.resourceName
  }
}

// -- Whisper STT (dedicated region: northcentralus) --

module whisperNaming 'modules/naming-tags.bicep' = {
  name: 'deploy-whisper-naming-${environmentName}'
  scope: rgWhisper
  params: {
    resourceTypePrefix: 'oai'
    appName: 'rvs'
    workload: 'whisper'
    environmentName: environmentName
    location: whisperLocation
  }
}

module whisper 'modules/openai-whisper.bicep' = {
  name: 'deploy-whisper-${environmentName}'
  scope: rgWhisper
  params: {
    location: whisperLocation
    environmentName: environmentName
    tags: whisperNaming.outputs.tags
    whisperCapacity: whisperCapacity
    resourceName: whisperNaming.outputs.resourceName
  }
}

// -- Key Vault secrets (primary RG where KV lives, cross-RG reference for Whisper) --

module keyVaultSecrets 'modules/openai-keyvault-secrets.bicep' = if (!empty(keyVaultName)) {
  name: 'deploy-openai-kv-secrets-${environmentName}'
  scope: rgPrimary
  params: {
    keyVaultName: keyVaultName
    openAiName: openAi.outputs.name
    openAiDeploymentName: openAi.outputs.deploymentName
    openAiTextDeploymentName: textDeploymentName
    whisperOpenAiName: whisper.outputs.name
    whisperOpenAiResourceGroup: rgWhisper.name
    openAiWhisperDeploymentName: whisper.outputs.whisperDeploymentName
  }
}

// -- Storage Account (primary region: westus3, dev-only by default) --

module storage 'modules/storage-account.bicep' = if (deployStorageAccount) {
  name: 'deploy-storage-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    storageAccountName: resolvedStorageAccountName
    sku: 'Standard_LRS'
    blobAccessPrincipalId: storageBlobAccessPrincipalId
    corsAllowedOrigins: resolvedCorsOrigins
    tags: {
      Application: 'rvs'
      Environment: environmentName
      ManagedBy: 'Bicep'
    }
  }
}

// -- Azure Communication Services (Email + SMS, global resource) --

module acsNaming 'modules/naming-tags.bicep' = if (deployAcs) {
  name: 'deploy-acs-naming-${environmentName}'
  scope: rgPrimary
  params: {
    resourceTypePrefix: 'acs'
    appName: 'rvs'
    workload: 'notify'
    environmentName: environmentName
    location: location
  }
}

module communicationServices 'modules/communication-services.bicep' = if (deployAcs) {
  name: 'deploy-acs-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    resourceName: deployAcs ? acsNaming.outputs.resourceName : 'unused'
    #disable-next-line BCP318
    tags: deployAcs ? acsNaming.outputs.tags : {}
    dataLocation: acsDataLocation
  }
}

// -- ACS Key Vault secrets (optional, same RG as KV) --

module acsKeyVaultSecrets 'modules/acs-keyvault-secrets.bicep' = if (deployAcs && !empty(keyVaultName)) {
  name: 'deploy-acs-kv-secrets-${environmentName}'
  scope: rgPrimary
  params: {
    keyVaultName: keyVaultName
    #disable-next-line BCP318
    acsName: deployAcs ? communicationServices.outputs.name : 'unused'
  }
}

// -- Static Web Apps (Intake + Manager, dedicated rg-rvs-{env}-westus2) --

module swaIntake 'modules/static-web-app.bicep' = if (deploySwa) {
  name: 'deploy-swa-intake-${environmentName}'
  scope: rgSwa
  params: {
    location: swaLocation
    resourceName: swaIntakeName
    skuName: swaSkuName
    tags: swaTags
  }
}

module swaManager 'modules/static-web-app.bicep' = if (deploySwa) {
  name: 'deploy-swa-manager-${environmentName}'
  scope: rgSwa
  params: {
    location: swaLocation
    resourceName: swaManagerName
    skuName: swaSkuName
    tags: swaTags
  }
}

// -- DNS Zone + SWA CNAME records (scoped to prod primary RG; shared apex zone, env-specific prefixes) --

module dns 'modules/dns.bicep' = if (deploySwa && deployDns) {
  name: 'deploy-dns-${environmentName}'
  scope: resourceGroup(dnsResourceGroupName)
  params: {
    zoneName: domainName
    intakePrefix: intakeDnsPrefix
    managerPrefix: managerDnsPrefix
    #disable-next-line BCP318
    intakeSwaHostname: deploySwa ? swaIntake.outputs.defaultHostname : ''
    #disable-next-line BCP318
    managerSwaHostname: deploySwa ? swaManager.outputs.defaultHostname : ''
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The primary resource group name (GPT-4o + Key Vault).')
output primaryResourceGroup string = rgPrimary.name

@description('The Whisper resource group name.')
output whisperResourceGroup string = rgWhisper.name

@description('The dedicated Static Web Apps resource group name. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaResourceGroup string = deploySwa ? rgSwa.name : ''

@description('The Azure OpenAI resource endpoint URL (GPT-4o).')
output openAiEndpoint string = openAi.outputs.endpoint

@description('The name of the GPT-4o vision model deployment.')
output openAiDeploymentName string = openAi.outputs.deploymentName

@description('The Whisper Azure OpenAI resource endpoint URL.')
output whisperEndpoint string = whisper.outputs.endpoint

@description('The name of the Whisper speech-to-text model deployment.')
output whisperDeploymentName string = whisper.outputs.whisperDeploymentName

@description('The name of the storage account, if deployed.')
#disable-next-line BCP318
output storageAccountName string = deployStorageAccount ? storage.outputs.name : ''

@description('The primary blob endpoint of the storage account, if deployed.')
#disable-next-line BCP318
output storageBlobEndpoint string = deployStorageAccount ? storage.outputs.blobEndpoint : ''

@description('The ACS resource endpoint URL, if deployed.')
#disable-next-line BCP318
output acsEndpoint string = deployAcs ? communicationServices.outputs.endpoint : ''

@description('The ACS resource name, if deployed.')
#disable-next-line BCP318
output acsName string = deployAcs ? communicationServices.outputs.name : ''

@description('The ACS Email Service name, if deployed.')
#disable-next-line BCP318
output acsEmailServiceName string = deployAcs ? communicationServices.outputs.emailServiceName : ''

@description('The Azure-managed MailFrom sender domain, if deployed.')
#disable-next-line BCP318
output acsMailFromDomain string = deployAcs ? communicationServices.outputs.azureManagedMailFrom : ''

@description('Default hostname for the Intake Static Web App (CNAME target at DNS provider). Empty when deploySwa = false.')
#disable-next-line BCP318
output swaIntakeHostname string = deploySwa ? swaIntake.outputs.defaultHostname : ''

@description('Default hostname for the Manager Static Web App (CNAME target at DNS provider). Empty when deploySwa = false.')
#disable-next-line BCP318
output swaManagerHostname string = deploySwa ? swaManager.outputs.defaultHostname : ''

@secure()
@description('Deployment token for RVS.Blazor.Intake SWA. Store as GitHub Secret SWA_TOKEN_INTAKE_<ENV>. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaIntakeDeploymentToken string = deploySwa ? swaIntake.outputs.deploymentToken : ''

@secure()
@description('Deployment token for RVS.Blazor.Manager SWA. Store as GitHub Secret SWA_TOKEN_MANAGER_<ENV>. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaManagerDeploymentToken string = deploySwa ? swaManager.outputs.deploymentToken : ''

@description('Azure-assigned nameservers for the DNS zone. Point your registrar NS records at these. Empty when deployDns = false.')
#disable-next-line BCP318
output dnsNameServers array = (deploySwa && deployDns) ? dns.outputs.nameServers : []

@description('FQDN for the Intake SWA custom domain (e.g. intake.rvserviceflow.com). Empty when deployDns = false.')
#disable-next-line BCP318
output intakeFqdn string = (deploySwa && deployDns) ? dns.outputs.intakeFqdn : ''

@description('FQDN for the Manager SWA custom domain (e.g. manager.rvserviceflow.com). Empty when deployDns = false.')
#disable-next-line BCP318
output managerFqdn string = (deploySwa && deployDns) ? dns.outputs.managerFqdn : ''
