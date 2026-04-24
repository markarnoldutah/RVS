// ──────────────────────────────────────────────────────────────
// Main Orchestration – RVS Azure Infrastructure
// ──────────────────────────────────────────────────────────────
// Deploys at subscription scope to manage resource groups per
// environment (staging / prod). Each environment is deployed
// independently via its own parameter file.
//
// Resource Groups:
//   • rg-rvs-{env}-westus3  — API, Cosmos DB, Storage, Key Vault,
//                              OpenAI GPT-4o, ACS, Log Analytics,
//                              App Insights
//   • rg-rvs-{env}-ncus     — Whisper OpenAI (northcentralus)
//   • rg-rvs-{env}-westus2  — Static Web Apps (Intake + Manager)
// ──────────────────────────────────────────────────────────────
targetScope = 'subscription'

// ── Parameters ────────────────────────────────────────────────

@description('The Azure region for the primary resources.')
param location string = 'westus3'

@description('The Azure region for Whisper STT. Whisper 001 Standard is not available in all regions.')
param whisperLocation string = 'northcentralus'

@description('The target environment (staging or prod).')
@allowed([
  'staging'
  'prod'
])
param environmentName string = 'staging'

@description('Name of the primary resource group.')
param primaryResourceGroupName string = 'rg-rvs-${environmentName}-westus3'

@description('Name of the Whisper resource group (northcentralus).')
param whisperResourceGroupName string = 'rg-rvs-${environmentName}-ncus'

@description('GPT-4o deployment capacity in thousands of tokens per minute (K TPM). Staging = 10, Prod = 30+.')
@minValue(1)
param openAiCapacity int = 1

@description('Whisper deployment capacity in thousands of tokens per minute (K TPM). Dev = 1.')
@minValue(1)
param whisperCapacity int = 1

@description('Optional. Name of the model deployment used for text workloads. Defaults to gpt-4o.')
param textDeploymentName string = 'gpt-4o'

// ── Storage Parameters ────────────────────────────────────────

@description('When true, deploys a general-purpose v2 storage account (Standard_LRS) with the rvs-attachments blob container.')
param deployStorageAccount bool = false

@description('Override the storage account name. Leave empty to use the computed default (strvs<env>wus3001). Must be 3-24 lowercase alphanumeric characters and globally unique.')
@maxLength(24)
param storageAccountNameOverride string = ''

@description('Allowed CORS origins for the Storage Account blob service (SAS direct-upload from SPAs).')
param storageCorsOrigins string[] = []

// ── ACS Parameters ────────────────────────────────────────────

@description('When true, deploys an Azure Communication Services resource with Email and SMS capabilities.')
param deployAcs bool = false

@description('ACS data residency location.')
param acsDataLocation string = 'United States'

// ── Static Web App Parameters ─────────────────────────────────

@description('When true, deploys Azure Static Web App resources for Blazor.Intake and Blazor.Manager.')
param deploySwa bool = false

@description('Azure region for Static Web Apps. Must be an SWA-supported region.')
param swaLocation string = 'westus2'

@description('Name of the dedicated resource group for Static Web App resources.')
param swaResourceGroupName string = 'rg-rvs-${environmentName}-westus2'

@description('SWA SKU tier. Free for test; Standard for staging/production (required for custom auth and custom domains).')
@allowed([
  'Free'
  'Standard'
])
param swaSkuName string = 'Free'

// ── DNS Parameters ────────────────────────────────────────────

@description('When true, provisions Azure DNS records for the SWA custom domains. Requires deploySwa = true.')
param deployDns bool = false

@description('Resource group that owns the DNS zones. Apex zones are shared across environments and owned by the prod RG.')
param dnsResourceGroupName string = 'rg-rvs-prod-westus3'

@description('DNS zone for the Manager SWA (CNAME subdomain in every env).')
param managerZoneName string = 'rvserviceflow.com'

@description('DNS zone for the Intake SWA (apex in prod, subdomain CNAME in non-prod envs).')
param intakeZoneName string = 'rvintake.com'

@description('Subdomain prefix for the Manager SWA CNAME record.')
param managerDnsPrefix string = environmentName == 'prod' ? 'manager' : 'manager-${environmentName}'

@description('Subdomain prefix for the Intake SWA CNAME record in non-prod envs (e.g. "staging" -> staging.rvintake.com). Ignored in prod where Intake binds to the apex.')
param intakeDnsPrefix string = environmentName == 'prod' ? '' : environmentName

@description('IPv4 addresses for the Intake apex A record (prod only). SWA does not support Azure DNS alias targeting — Microsoft advertises the regional anycast IPs via the portal after the SWA custom-domain registration is accepted. Leave empty for non-prod; required for prod DNS to resolve to Intake.')
param intakeApexIpv4Addresses array = []

@description('TXT record values for the Intake apex domain-ownership validation (prod only). Provided by Azure after the SWA customDomains registration request; typically a single-entry list. Leave empty for non-prod.')
param intakeApexValidationValues array = []

// ── App Service Parameters ────────────────────────────────────

@description('When true, deploys an App Service Plan and Web App for the RVS API with Managed Identity.')
param deployAppService bool = false

@description('App Service Plan SKU. F1 = Free (staging, 60 CPU-min/day). B1 = Basic (MVP prod). S1 = Standard (upgrade: Always On, slots).')
@allowed([
  'F1'
  'B1'
  'S1'
])
param appServiceSkuName string = 'F1'

// ── Cosmos DB Parameters ──────────────────────────────────────

@description('When true, deploys a Cosmos DB account in the specified capacity mode with all RVS containers.')
param deployCosmosDb bool = false

@description('Cosmos DB capacity mode. Serverless = pay-per-request (MVP). Provisioned = autoscale throughput (upgrade path).')
@allowed([
  'Serverless'
  'Provisioned'
])
param cosmosCapacityMode string = 'Serverless'

@description('Maximum autoscale throughput (RU/s) when cosmosCapacityMode is Provisioned. Ignored for Serverless.')
@minValue(1000)
@maxValue(1000000)
param cosmosAutoscaleMaxThroughput int = 4000

// ── Key Vault Parameters ──────────────────────────────────────

@description('When true, deploys a Key Vault with RBAC access model. The API managed identity is granted get + list on secrets.')
param deployKeyVault bool = false

@description('Override the Key Vault name (must be 3-24 chars, globally unique). Leave empty to use the computed default.')
@maxLength(24)
param keyVaultNameOverride string = ''

// ── Auth0 Parameters (external identity provider) ─────────────

@secure()
@description('Auth0 tenant domain URL (e.g. https://rvs-dev.us.auth0.com/). Required when deployKeyVault = true.')
param auth0Domain string = ''

@secure()
@description('Auth0 API audience identifier (e.g. https://api.rvserviceflow.com). Required when deployKeyVault = true.')
param auth0Audience string = ''

@secure()
@description('Auth0 application client ID. Required when deployKeyVault = true.')
param auth0ClientId string = ''

@secure()
@description('Auth0 application client secret. Required when deployKeyVault = true.')
param auth0ClientSecret string = ''

// ── Observability Parameters ──────────────────────────────────

@description('When true, deploys a Log Analytics workspace and Application Insights resource.')
param deployObservability bool = false

@description('When true and deployObservability + deployAppService are both true, creates a standard availability test on the API /health endpoint.')
param deployAvailabilityTest bool = false

// ── Variables ─────────────────────────────────────────────────

// Storage account names must be 3-24 lowercase alphanumeric characters with no hyphens.
var defaultStorageAccountName = 'strvs${environmentName}wus3001'
var resolvedStorageAccountName = empty(storageAccountNameOverride)
  ? defaultStorageAccountName
  : storageAccountNameOverride

// Environment-aware default CORS origins for browser-based SAS uploads.
var defaultCorsOrigins = environmentName == 'prod'
  ? ['https://rvintake.com', 'https://manager.rvserviceflow.com']
  : environmentName == 'staging'
      ? ['https://staging.rvintake.com', 'https://manager-staging.rvserviceflow.com']
      : [
          'https://localhost:7008'
          'https://localhost:7116'
          'https://localhost:7200'
          'https://localhost:7300'
        ]

var resolvedCorsOrigins = !empty(storageCorsOrigins) ? storageCorsOrigins : defaultCorsOrigins

// SWA resource names
var swaIntakeName = 'stapp-rvs-intake-${environmentName}'
var swaManagerName = 'stapp-rvs-manager-${environmentName}'

// Key Vault name (max 24 chars)
var defaultKeyVaultName = 'kv-rvs-${environmentName}-wus3'
var resolvedKeyVaultName = empty(keyVaultNameOverride) ? defaultKeyVaultName : keyVaultNameOverride

// App Service naming
var appServicePlanName = 'asp-rvs-api-${environmentName}-wus3'
var appServiceName = 'app-rvs-api-${environmentName}-wus3'

// Cosmos DB naming
var cosmosAccountName = 'cosmos-rvs-data-${environmentName}-wus3'

// Log Analytics and App Insights naming
var logAnalyticsName = 'law-rvs-obs-${environmentName}-wus3'
var appInsightsName = 'appi-rvs-api-${environmentName}-wus3'

// Health check URL (computed from known app name; avoids circular dependency)
var healthCheckUrl = 'https://${appServiceName}.azurewebsites.net/health'

// Staging slot: automatically created when SKU supports deployment slots (Standard+)
var deployStagingSlot = appServiceSkuName == 'S1'

// Shared tags
var sharedTags = {
  Application: 'rvs'
  Environment: environmentName
  ManagedBy: 'Bicep'
}

// ── Resource Groups ───────────────────────────────────────────

resource rgPrimary 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: primaryResourceGroupName
  location: location
  tags: sharedTags
}

resource rgWhisper 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: whisperResourceGroupName
  location: whisperLocation
  tags: sharedTags
}

resource rgSwa 'Microsoft.Resources/resourceGroups@2024-07-01' = if (deploySwa) {
  name: swaResourceGroupName
  location: swaLocation
  tags: sharedTags
}

// ══════════════════════════════════════════════════════════════
// Modules
// ══════════════════════════════════════════════════════════════

// ── App Service (API) — deployed first; no cross-resource refs ──

module appService 'modules/app-service.bicep' = if (deployAppService) {
  name: 'deploy-app-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    appServicePlanName: appServicePlanName
    appName: appServiceName
    tags: sharedTags
    skuName: appServiceSkuName
    deployStagingSlot: deployStagingSlot
  }
}

// ── Observability (Log Analytics + App Insights) ──────────────

module logAnalytics 'modules/log-analytics.bicep' = if (deployObservability) {
  name: 'deploy-law-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    workspaceName: logAnalyticsName
    tags: sharedTags
  }
}

module appInsights 'modules/app-insights.bicep' = if (deployObservability) {
  name: 'deploy-appi-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    appInsightsName: appInsightsName
    tags: sharedTags
    #disable-next-line BCP318
    logAnalyticsWorkspaceId: deployObservability ? logAnalytics.outputs.resourceId : ''
    deployAvailabilityTest: deployAvailabilityTest && deployAppService
    healthCheckUrl: (deployAvailabilityTest && deployAppService) ? healthCheckUrl : ''
  }
}

// ── Key Vault ─────────────────────────────────────────────────

module keyVault 'modules/key-vault.bicep' = if (deployKeyVault) {
  name: 'deploy-kv-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    keyVaultName: resolvedKeyVaultName
    tags: sharedTags
    #disable-next-line BCP318
    apiPrincipalId: (deployKeyVault && deployAppService) ? appService.outputs.principalId : ''
    #disable-next-line BCP318
    stagingSlotPrincipalId: (deployKeyVault && deployAppService && deployStagingSlot)
      ? appService.outputs.stagingSlotPrincipalId
      : ''
  }
}

// ── App Service Configuration (post-deploy settings) ──────────

module appServiceConfig 'modules/app-service-config.bicep' = if (deployAppService) {
  name: 'deploy-app-config-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    appName: deployAppService ? appService.outputs.name : ''
    environmentName: environmentName
    #disable-next-line BCP318
    appInsightsConnectionString: (deployAppService && deployObservability) ? appInsights.outputs.connectionString : ''
    #disable-next-line BCP318
    keyVaultUri: (deployAppService && deployKeyVault) ? keyVault.outputs.vaultUri : ''
    configureStagingSlot: deployStagingSlot
  }
}

// ── Cosmos DB ─────────────────────────────────────────────────

module cosmosDb 'modules/cosmos-db.bicep' = if (deployCosmosDb) {
  name: 'deploy-cosmos-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    accountName: cosmosAccountName
    tags: sharedTags
    capacityMode: cosmosCapacityMode
    autoscaleMaxThroughput: cosmosAutoscaleMaxThroughput
  }
}

// ── Storage Account ───────────────────────────────────────────

module storage 'modules/storage-account.bicep' = if (deployStorageAccount) {
  name: 'deploy-storage-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    storageAccountName: resolvedStorageAccountName
    sku: 'Standard_LRS'
    #disable-next-line BCP318
    blobAccessPrincipalId: (deployStorageAccount && deployAppService) ? appService.outputs.principalId : ''
    #disable-next-line BCP318
    stagingSlotBlobAccessPrincipalId: (deployStorageAccount && deployAppService && deployStagingSlot)
      ? appService.outputs.stagingSlotPrincipalId
      : ''
    corsAllowedOrigins: resolvedCorsOrigins
    tags: sharedTags
  }
}

// ── OpenAI GPT-4o (primary region: westus3) ───────────────────

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

// ── Whisper STT (dedicated region: northcentralus) ────────────

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

// ── Key Vault Secrets (OpenAI) ────────────────────────────────

module keyVaultSecrets 'modules/openai-keyvault-secrets.bicep' = if (deployKeyVault) {
  name: 'deploy-openai-kv-secrets-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    keyVaultName: deployKeyVault ? keyVault.outputs.name : 'unused'
    openAiName: openAi.outputs.name
    openAiDeploymentName: openAi.outputs.deploymentName
    openAiTextDeploymentName: textDeploymentName
    whisperOpenAiName: whisper.outputs.name
    whisperOpenAiResourceGroup: rgWhisper.name
    openAiWhisperDeploymentName: whisper.outputs.whisperDeploymentName
  }
}

// ── Azure Communication Services ──────────────────────────────

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

module acsKeyVaultSecrets 'modules/acs-keyvault-secrets.bicep' = if (deployAcs && deployKeyVault) {
  name: 'deploy-acs-kv-secrets-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    keyVaultName: deployKeyVault ? keyVault.outputs.name : 'unused'
    #disable-next-line BCP318
    acsName: deployAcs ? communicationServices.outputs.name : 'unused'
  }
}

// ── Key Vault Secrets (Cosmos DB) ─────────────────────────────

module cosmosKeyVaultSecrets 'modules/cosmos-keyvault-secrets.bicep' = if (deployCosmosDb && deployKeyVault) {
  name: 'deploy-cosmos-kv-secrets-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    keyVaultName: deployKeyVault ? keyVault.outputs.name : 'unused'
    #disable-next-line BCP318
    cosmosAccountName: deployCosmosDb ? cosmosDb.outputs.name : 'unused'
    #disable-next-line BCP318
    databaseName: deployCosmosDb ? cosmosDb.outputs.databaseName : 'rvs-db'
  }
}

// ── Key Vault Secrets (Storage — Blob + Tables) ───────────────

module storageKeyVaultSecrets 'modules/storage-keyvault-secrets.bicep' = if (deployStorageAccount && deployKeyVault) {
  name: 'deploy-storage-kv-secrets-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    keyVaultName: deployKeyVault ? keyVault.outputs.name : 'unused'
    #disable-next-line BCP318
    storageAccountName: deployStorageAccount ? storage.outputs.name : 'unused'
  }
}

// ── Key Vault Secrets (Auth0) ─────────────────────────────────

module auth0KeyVaultSecrets 'modules/auth0-keyvault-secrets.bicep' = if (deployKeyVault && !empty(auth0Domain)) {
  name: 'deploy-auth0-kv-secrets-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    keyVaultName: deployKeyVault ? keyVault.outputs.name : 'unused'
    auth0Domain: auth0Domain
    auth0Audience: auth0Audience
    auth0ClientId: auth0ClientId
    auth0ClientSecret: auth0ClientSecret
    auth0TokenUrl: '${auth0Domain}oauth/token'
    auth0AuthorizationUrl: '${auth0Domain}authorize'
  }
}

// ── Key Vault Secrets (Application Insights) ──────────────────

module appInsightsKeyVaultSecrets 'modules/appinsights-keyvault-secrets.bicep' = if (deployKeyVault && deployObservability) {
  name: 'deploy-appi-kv-secrets-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    keyVaultName: deployKeyVault ? keyVault.outputs.name : 'unused'
    #disable-next-line BCP318
    appInsightsName: deployObservability ? appInsights.outputs.name : 'unused'
  }
}

// ── Static Web Apps (Intake + Manager) ────────────────────────

var managerCustomDomains = deployDns ? [
  {
    hostname: '${managerDnsPrefix}.${managerZoneName}'
    validationMethod: 'cname-delegation'
  }
] : []

// Intake apex (prod) is two-phase: only bind the custom domain once intakeApexValidationValues
// has been populated (after Azure generates the SWA ownership token on first deploy).
var intakeCustomDomains = deployDns && environmentName == 'prod' && !empty(intakeApexValidationValues) ? [
  {
    hostname: intakeZoneName
    validationMethod: 'dns-txt-token'
  }
] : deployDns && environmentName != 'prod' ? [
  {
    hostname: '${intakeDnsPrefix}.${intakeZoneName}'
    validationMethod: 'cname-delegation'
  }
] : []

module swaIntake 'modules/static-web-app.bicep' = if (deploySwa) {
  name: 'deploy-swa-intake-${environmentName}'
  scope: rgSwa
  params: {
    location: swaLocation
    resourceName: swaIntakeName
    skuName: swaSkuName
    tags: sharedTags
    customDomains: intakeCustomDomains
  }
}

module swaManager 'modules/static-web-app.bicep' = if (deploySwa) {
  name: 'deploy-swa-manager-${environmentName}'
  scope: rgSwa
  params: {
    location: swaLocation
    resourceName: swaManagerName
    skuName: swaSkuName
    tags: sharedTags
    customDomains: managerCustomDomains
  }
}

// ── DNS: Manager zone (rvserviceflow.com) — CNAME subdomain ────
// Every env maps a subdomain of rvserviceflow.com to the Manager SWA.

module dnsManager 'modules/dns.bicep' = if (deploySwa && deployDns) {
  name: 'deploy-dns-manager-${environmentName}'
  scope: resourceGroup(dnsResourceGroupName)
  params: {
    zoneName: managerZoneName
    cnameRecords: [
      {
        name: managerDnsPrefix
        #disable-next-line BCP318
        target: swaManager.outputs.defaultHostname
      }
    ]
  }
}

// ── DNS: Intake zone (rvintake.com) ────────────────────────────
// Prod:    apex A-record + TXT validation (CNAME at apex is invalid per RFC 1034).
//          intakeApexIpv4Addresses + intakeApexValidationValues must be set before
//          the apex resolves — they come from Azure after the SWA customDomains
//          registration is accepted (two-phase deploy).
// Non-prod: subdomain CNAME (e.g. staging.rvintake.com → default SWA hostname).

module dnsIntake 'modules/dns.bicep' = if (deploySwa && deployDns) {
  name: 'deploy-dns-intake-${environmentName}'
  scope: resourceGroup(dnsResourceGroupName)
  params: {
    zoneName: intakeZoneName
    cnameRecords: environmentName == 'prod' ? [] : [
      {
        name: intakeDnsPrefix
        #disable-next-line BCP318
        target: swaIntake.outputs.defaultHostname
      }
    ]
    aRecords: (environmentName == 'prod' && !empty(intakeApexIpv4Addresses)) ? [
      {
        name: '@'
        ipv4Addresses: intakeApexIpv4Addresses
      }
    ] : []
    txtRecords: (environmentName == 'prod' && !empty(intakeApexValidationValues)) ? [
      {
        name: '@'
        values: intakeApexValidationValues
      }
    ] : []
  }
}

// ══════════════════════════════════════════════════════════════
// Outputs
// ══════════════════════════════════════════════════════════════

@description('The primary resource group name.')
output primaryResourceGroup string = rgPrimary.name

@description('The Whisper resource group name.')
output whisperResourceGroup string = rgWhisper.name

@description('The Static Web Apps resource group name. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaResourceGroup string = deploySwa ? rgSwa.name : ''

// ── OpenAI ────────────────────────────────────────────────────

@description('The Azure OpenAI resource endpoint URL (GPT-4o).')
output openAiEndpoint string = openAi.outputs.endpoint

@description('The name of the GPT-4o model deployment.')
output openAiDeploymentName string = openAi.outputs.deploymentName

@description('The Whisper Azure OpenAI resource endpoint URL.')
output whisperEndpoint string = whisper.outputs.endpoint

@description('The name of the Whisper model deployment.')
output whisperDeploymentName string = whisper.outputs.whisperDeploymentName

// ── App Service ───────────────────────────────────────────────

@description('Default hostname of the API Web App. Empty when deployAppService = false.')
#disable-next-line BCP318
output appServiceHostname string = deployAppService ? appService.outputs.defaultHostname : ''

@description('Principal ID of the API managed identity. Empty when deployAppService = false.')
#disable-next-line BCP318
output appServicePrincipalId string = deployAppService ? appService.outputs.principalId : ''

@description('Staging slot hostname. Empty when no staging slot is created.')
#disable-next-line BCP318
output appServiceStagingSlotHostname string = (deployAppService && deployStagingSlot)
  ? appService.outputs.stagingSlotHostname
  : ''

@description('Staging slot managed identity principal ID. Empty when no staging slot.')
#disable-next-line BCP318
output appServiceStagingSlotPrincipalId string = (deployAppService && deployStagingSlot)
  ? appService.outputs.stagingSlotPrincipalId
  : ''

// ── Cosmos DB ─────────────────────────────────────────────────

@description('Cosmos DB account endpoint. Empty when deployCosmosDb = false.')
#disable-next-line BCP318
output cosmosEndpoint string = deployCosmosDb ? cosmosDb.outputs.endpoint : ''

@description('Cosmos DB database name. Empty when deployCosmosDb = false.')
#disable-next-line BCP318
output cosmosDatabaseName string = deployCosmosDb ? cosmosDb.outputs.databaseName : ''

// ── Storage ───────────────────────────────────────────────────

@description('Name of the storage account. Empty when deployStorageAccount = false.')
#disable-next-line BCP318
output storageAccountName string = deployStorageAccount ? storage.outputs.name : ''

@description('Primary blob endpoint. Empty when deployStorageAccount = false.')
#disable-next-line BCP318
output storageBlobEndpoint string = deployStorageAccount ? storage.outputs.blobEndpoint : ''

// ── Key Vault ─────────────────────────────────────────────────

@description('Key Vault name. Empty when deployKeyVault = false.')
#disable-next-line BCP318
output keyVaultName string = deployKeyVault ? keyVault.outputs.name : ''

@description('Key Vault URI. Empty when deployKeyVault = false.')
#disable-next-line BCP318
output keyVaultUri string = deployKeyVault ? keyVault.outputs.vaultUri : ''

// ── Observability ─────────────────────────────────────────────

@description('Application Insights connection string. Empty when deployObservability = false.')
#disable-next-line BCP318
output appInsightsConnectionString string = deployObservability ? appInsights.outputs.connectionString : ''

@description('Log Analytics workspace name. Empty when deployObservability = false.')
#disable-next-line BCP318
output logAnalyticsWorkspaceName string = deployObservability ? logAnalytics.outputs.name : ''

// ── ACS ───────────────────────────────────────────────────────

@description('ACS resource endpoint URL. Empty when deployAcs = false.')
#disable-next-line BCP318
output acsEndpoint string = deployAcs ? communicationServices.outputs.endpoint : ''

@description('ACS resource name. Empty when deployAcs = false.')
#disable-next-line BCP318
output acsName string = deployAcs ? communicationServices.outputs.name : ''

@description('ACS Email Service name. Empty when deployAcs = false.')
#disable-next-line BCP318
output acsEmailServiceName string = deployAcs ? communicationServices.outputs.emailServiceName : ''

@description('Azure-managed MailFrom sender domain. Empty when deployAcs = false.')
#disable-next-line BCP318
output acsMailFromDomain string = deployAcs ? communicationServices.outputs.azureManagedMailFrom : ''

// ── SWA ───────────────────────────────────────────────────────

@description('Default hostname for the Intake SWA. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaIntakeHostname string = deploySwa ? swaIntake.outputs.defaultHostname : ''

@description('Default hostname for the Manager SWA. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaManagerHostname string = deploySwa ? swaManager.outputs.defaultHostname : ''

@secure()
@description('Deployment token for Blazor.Intake SWA. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaIntakeDeploymentToken string = deploySwa ? swaIntake.outputs.deploymentToken : ''

@secure()
@description('Deployment token for Blazor.Manager SWA. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaManagerDeploymentToken string = deploySwa ? swaManager.outputs.deploymentToken : ''

// ── DNS ───────────────────────────────────────────────────────

@description('Azure-assigned nameservers for the Manager DNS zone (rvserviceflow.com). Empty when deployDns = false.')
#disable-next-line BCP318
output dnsManagerNameServers array = (deploySwa && deployDns) ? dnsManager.outputs.nameServers : []

@description('Azure-assigned nameservers for the Intake DNS zone (rvintake.com). Empty when deployDns = false.')
#disable-next-line BCP318
output dnsIntakeNameServers array = (deploySwa && deployDns) ? dnsIntake.outputs.nameServers : []

@description('FQDN for the Intake SWA custom domain — apex in prod, subdomain elsewhere.')
output intakeFqdn string = environmentName == 'prod' ? intakeZoneName : '${intakeDnsPrefix}.${intakeZoneName}'

@description('FQDN for the Manager SWA custom domain.')
output managerFqdn string = '${managerDnsPrefix}.${managerZoneName}'
