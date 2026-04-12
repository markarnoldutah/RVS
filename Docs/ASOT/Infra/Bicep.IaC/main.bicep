// ──────────────────────────────────────────────────────────────
// Main Orchestration – RVS Azure Infrastructure
// ──────────────────────────────────────────────────────────────
// Deploys at subscription scope to manage resource groups and
// all Azure resources for the RVS platform:
//
//   • App Service (API) with Managed Identity and Always On
//   • Cosmos DB (10 containers with index policies)
//   • Blob Storage (rvs-attachments container with CORS)
//   • Key Vault (RBAC-enabled, API MI has get + list)
//   • Application Insights + Log Analytics
//   • Azure OpenAI (GPT-4o + Whisper)
//   • Azure Communication Services (Email + SMS)
//   • Static Web Apps (Intake + Manager)
//   • DNS Zone + CNAME records
//
// Resource Groups:
//   • rg-rvs-{env}-westus3   — Primary (API, Cosmos, Storage, KV, etc.)
//   • rg-rvs-{env}-ncus      — Whisper OpenAI (northcentralus)
//   • rg-rvs-{env}-westus2   — Static Web Apps
// ──────────────────────────────────────────────────────────────
targetScope = 'subscription'

// ── Parameters ────────────────────────────────────────────────

@description('The Azure region for primary resources.')
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

@description('Name of the primary resource group.')
param primaryResourceGroupName string = 'rg-rvs-${environmentName}-westus3'

@description('Name of the Whisper resource group (northcentralus).')
param whisperResourceGroupName string = 'rg-rvs-${environmentName}-ncus'

@description('GPT-4o deployment capacity in thousands of tokens per minute (K TPM).')
@minValue(1)
param openAiCapacity int = 1

@description('Whisper deployment capacity in thousands of tokens per minute (K TPM).')
@minValue(1)
param whisperCapacity int = 1

@description('Name of the model deployment used for text workloads. Defaults to the standard gpt-4o deployment name.')
param textDeploymentName string = 'gpt-4o'

// ── Storage Parameters ────────────────────────────────────────

@description('When true, deploys a general-purpose v2 storage account.')
param deployStorageAccount bool = false

@description('Override the storage account name. Leave empty to use the computed default.')
@maxLength(24)
param storageAccountNameOverride string = ''

@description('Override CORS origins for browser-based SAS uploads. Leave empty to use environment defaults.')
param storageCorsAllowedOrigins string[] = []

// ── ACS Parameters ────────────────────────────────────────────

@description('When true, deploys an Azure Communication Services resource.')
param deployAcs bool = false

@description('ACS data residency location.')
param acsDataLocation string = 'United States'

// ── SWA Parameters ────────────────────────────────────────────

@description('When true, deploys Azure Static Web App resources for Intake and Manager.')
param deploySwa bool = false

@description('Azure region for Static Web Apps.')
param swaLocation string = 'westus2'

@description('Name of the dedicated resource group for Static Web App resources.')
param swaResourceGroupName string = 'rg-rvs-${environmentName}-westus2'

@description('SWA SKU tier.')
@allowed([
  'Free'
  'Standard'
])
param swaSkuName string = 'Free'

// ── DNS Parameters ────────────────────────────────────────────

@description('When true, provisions the Azure DNS zone and SWA CNAME records.')
param deployDns bool = false

@description('Public DNS zone apex domain.')
param domainName string = 'rvserviceflow.com'

@description('Resource group that owns the DNS zone.')
param dnsResourceGroupName string = 'rg-rvs-prod-westus3'

@description('Subdomain prefix for the Intake SWA CNAME record.')
param intakeDnsPrefix string = environmentName == 'prod' ? 'intake' : 'intake-${environmentName}'

@description('Subdomain prefix for the Manager SWA CNAME record.')
param managerDnsPrefix string = environmentName == 'prod' ? 'manager' : 'manager-${environmentName}'

// ── App Service Parameters ────────────────────────────────────

@description('When true, deploys the App Service Plan and App Service (API).')
param deployAppService bool = false

@description('App Service Plan SKU. B1 for dev, P1v3 for staging, P2v3 for prod.')
@allowed([
  'B1'
  'P1v3'
  'P2v3'
  'P3v3'
])
param appServicePlanSku string = 'B1'

@description('Enable Always On for the App Service. Requires Basic tier or higher.')
param appServiceAlwaysOn bool = false

@description('CORS origins for the App Service API.')
param appServiceCorsOrigins string[] = []

// ── Cosmos DB Parameters ──────────────────────────────────────

@description('When true, deploys a Cosmos DB account with all containers.')
param deployCosmosDb bool = false

@description('Shared database-level autoscale max throughput in RU/s. Use 1000 for dev, 4000+ for prod.')
param cosmosDbSharedThroughput int = 1000

@description('Continuous backup tier.')
@allowed([
  'Continuous7Days'
  'Continuous30Days'
])
param cosmosDbBackupTier string = 'Continuous7Days'

// ── Key Vault Parameters ──────────────────────────────────────

@description('When true, deploys a new Key Vault.')
param deployKeyVault bool = false

@description('Optional. Name of an existing Key Vault to store OpenAI/ACS secrets. Used when deployKeyVault is false. Leave empty to skip secret creation.')
param keyVaultName string = ''

@description('Enable purge protection on Key Vault (recommended for prod).')
param keyVaultPurgeProtection bool = false

// ── Observability Parameters ──────────────────────────────────

@description('When true, deploys Log Analytics Workspace and Application Insights.')
param deployObservability bool = false

@description('Log Analytics data retention in days.')
param logRetentionDays int = 90

@description('Log Analytics daily ingestion cap in GB.')
param logDailyQuotaGb int = 5

@description('When true, creates an availability test for the API health endpoint. Requires deployAppService and deployObservability.')
param enableAvailabilityTest bool = false

// ── Variables ─────────────────────────────────────────────────

var defaultStorageAccountName = 'strvs${environmentName}wus3001'
var resolvedStorageAccountName = empty(storageAccountNameOverride)
  ? defaultStorageAccountName
  : storageAccountNameOverride

var defaultCorsOrigins = environmentName == 'prod'
  ? ['https://portal.rvserviceflow.com']
  : [
      'https://localhost:7008'
      'https://localhost:7116'
      'https://localhost:7200'
      'https://localhost:7300'
    ]

var resolvedCorsOrigins = !empty(storageCorsAllowedOrigins) ? storageCorsAllowedOrigins : defaultCorsOrigins

var swaIntakeName = 'stapp-rvs-intake-${environmentName}'
var swaManagerName = 'stapp-rvs-manager-${environmentName}'

var sharedTags = {
  Application: 'rvs'
  Environment: environmentName
  ManagedBy: 'Bicep'
}

// App Service CORS: use provided origins or fall back to localhost
var defaultAppServiceCorsOrigins = environmentName == 'prod'
  ? ['https://intake.rvserviceflow.com', 'https://manager.rvserviceflow.com']
  : [
      'https://localhost:7008'
      'https://localhost:7116'
      'https://localhost:7200'
      'https://localhost:7300'
    ]

var resolvedAppServiceCorsOrigins = !empty(appServiceCorsOrigins) ? appServiceCorsOrigins : defaultAppServiceCorsOrigins

// Built-in role definition IDs for RBAC assignments
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var cosmosDbAccountReaderRoleId = 'fbdf93bf-df7d-467e-a4d2-9458aa1360c8'

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
// OBSERVABILITY — Log Analytics + Application Insights
// ══════════════════════════════════════════════════════════════

module logAnalyticsNaming 'modules/naming-tags.bicep' = if (deployObservability) {
  name: 'deploy-law-naming-${environmentName}'
  scope: rgPrimary
  params: {
    resourceTypePrefix: 'law'
    appName: 'rvs'
    workload: 'obs'
    environmentName: environmentName
    location: location
  }
}

module logAnalytics 'modules/log-analytics.bicep' = if (deployObservability) {
  name: 'deploy-law-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    name: deployObservability ? logAnalyticsNaming.outputs.resourceName : 'unused'
    location: location
    retentionInDays: logRetentionDays
    dailyQuotaGb: logDailyQuotaGb
    #disable-next-line BCP318
    tags: deployObservability ? logAnalyticsNaming.outputs.tags : {}
  }
}

module appInsightsNaming 'modules/naming-tags.bicep' = if (deployObservability) {
  name: 'deploy-appi-naming-${environmentName}'
  scope: rgPrimary
  params: {
    resourceTypePrefix: 'appi'
    appName: 'rvs'
    workload: 'api'
    environmentName: environmentName
    location: location
  }
}

module appInsights 'modules/app-insights.bicep' = if (deployObservability) {
  name: 'deploy-appi-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    name: deployObservability ? appInsightsNaming.outputs.resourceName : 'unused'
    location: location
    #disable-next-line BCP318
    workspaceResourceId: deployObservability ? logAnalytics.outputs.resourceId : ''
    #disable-next-line BCP318
    tags: deployObservability ? appInsightsNaming.outputs.tags : {}
    enableAvailabilityTest: false
  }
}

// Availability test is deployed separately to avoid a circular dependency
// (appInsights <-> appServiceModule). It requires both resources to exist.
module availabilityTest 'modules/app-insights.bicep' = if (enableAvailabilityTest && deployObservability && deployAppService) {
  name: 'deploy-avail-test-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    name: (enableAvailabilityTest && deployObservability) ? appInsightsNaming.outputs.resourceName : 'unused'
    location: location
    #disable-next-line BCP318
    workspaceResourceId: (enableAvailabilityTest && deployObservability) ? logAnalytics.outputs.resourceId : ''
    #disable-next-line BCP318
    tags: (enableAvailabilityTest && deployObservability) ? appInsightsNaming.outputs.tags : {}
    enableAvailabilityTest: true
    #disable-next-line BCP318
    availabilityTestUrl: (enableAvailabilityTest && deployAppService)
      ? 'https://${appServiceModule.outputs.defaultHostname}/health/live'
      : ''
  }
}

// ══════════════════════════════════════════════════════════════
// KEY VAULT
// ══════════════════════════════════════════════════════════════

module keyVaultNaming 'modules/naming-tags.bicep' = if (deployKeyVault) {
  name: 'deploy-kv-naming-${environmentName}'
  scope: rgPrimary
  params: {
    resourceTypePrefix: 'kv'
    appName: 'rvs'
    workload: 'shared'
    environmentName: environmentName
    location: location
  }
}

module keyVaultModule 'modules/key-vault.bicep' = if (deployKeyVault) {
  name: 'deploy-kv-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    name: deployKeyVault ? keyVaultNaming.outputs.resourceName : 'unused-kv'
    location: location
    #disable-next-line BCP318
    tags: deployKeyVault ? keyVaultNaming.outputs.tags : {}
    enablePurgeProtection: keyVaultPurgeProtection
  }
}

// ══════════════════════════════════════════════════════════════
// DATA TIER — Cosmos DB
// ══════════════════════════════════════════════════════════════

module cosmosNaming 'modules/naming-tags.bicep' = if (deployCosmosDb) {
  name: 'deploy-cosmos-naming-${environmentName}'
  scope: rgPrimary
  params: {
    resourceTypePrefix: 'cosmos'
    appName: 'rvs'
    workload: 'data'
    environmentName: environmentName
    location: location
  }
}

module cosmosDb 'modules/cosmos-db.bicep' = if (deployCosmosDb) {
  name: 'deploy-cosmos-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    name: deployCosmosDb ? cosmosNaming.outputs.resourceName : 'unused'
    location: location
    #disable-next-line BCP318
    tags: deployCosmosDb ? cosmosNaming.outputs.tags : {}
    sharedThroughput: cosmosDbSharedThroughput
    backupTier: cosmosDbBackupTier
    environmentName: environmentName
  }
}

// ══════════════════════════════════════════════════════════════
// STORAGE — Blob Storage with rvs-attachments container
// ══════════════════════════════════════════════════════════════

module storage 'modules/storage-account.bicep' = if (deployStorageAccount) {
  name: 'deploy-storage-${environmentName}'
  scope: rgPrimary
  dependsOn: deployAppService ? [appServiceModule] : []
  params: {
    location: location
    storageAccountName: resolvedStorageAccountName
    sku: 'Standard_LRS'
    #disable-next-line BCP318
    blobAccessPrincipalId: deployAppService ? appServiceModule.outputs.systemAssignedMIPrincipalId : ''
    corsAllowedOrigins: resolvedCorsOrigins
    tags: sharedTags
  }
}

// ══════════════════════════════════════════════════════════════
// COMPUTE — App Service Plan + App Service (API)
// ══════════════════════════════════════════════════════════════

module appServicePlanNaming 'modules/naming-tags.bicep' = if (deployAppService) {
  name: 'deploy-plan-naming-${environmentName}'
  scope: rgPrimary
  params: {
    resourceTypePrefix: 'plan'
    appName: 'rvs'
    workload: 'api'
    environmentName: environmentName
    location: location
  }
}

module appServicePlan 'modules/app-service-plan.bicep' = if (deployAppService) {
  name: 'deploy-plan-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    name: deployAppService ? appServicePlanNaming.outputs.resourceName : 'unused'
    location: location
    #disable-next-line BCP318
    tags: deployAppService ? appServicePlanNaming.outputs.tags : {}
    skuName: appServicePlanSku
  }
}

module appServiceNaming 'modules/naming-tags.bicep' = if (deployAppService) {
  name: 'deploy-app-naming-${environmentName}'
  scope: rgPrimary
  params: {
    resourceTypePrefix: 'app'
    appName: 'rvs'
    workload: 'api'
    environmentName: environmentName
    location: location
  }
}

module appServiceModule 'modules/app-service.bicep' = if (deployAppService) {
  name: 'deploy-app-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    name: deployAppService ? appServiceNaming.outputs.resourceName : 'unused'
    location: location
    #disable-next-line BCP318
    serverFarmResourceId: deployAppService ? appServicePlan.outputs.resourceId : ''
    #disable-next-line BCP318
    tags: deployAppService ? appServiceNaming.outputs.tags : {}
    alwaysOn: appServiceAlwaysOn
    corsAllowedOrigins: resolvedAppServiceCorsOrigins
    #disable-next-line BCP318
    appInsightsConnectionString: (deployAppService && deployObservability) ? appInsights.outputs.connectionString : ''
    #disable-next-line BCP318
    cosmosDbEndpoint: (deployAppService && deployCosmosDb) ? cosmosDb.outputs.endpoint : ''
    storageAccountName: deployStorageAccount ? resolvedStorageAccountName : ''
    #disable-next-line BCP318
    keyVaultUri: (deployAppService && deployKeyVault) ? keyVaultModule.outputs.uri : ''
  }
}

// ══════════════════════════════════════════════════════════════
// RBAC — App Service MI → Key Vault, Cosmos DB
// ══════════════════════════════════════════════════════════════

// Key Vault Secrets User — API MI can read secrets
module kvRbac 'modules/rbac-key-vault.bicep' = if (deployAppService && deployKeyVault) {
  name: 'deploy-rbac-kv-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    keyVaultName: (deployAppService && deployKeyVault) ? keyVaultModule.outputs.name : 'unused'
    #disable-next-line BCP318
    principalId: (deployAppService && deployKeyVault) ? appServiceModule.outputs.systemAssignedMIPrincipalId : ''
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

// Cosmos DB Account Reader Role — API MI can read Cosmos DB
module cosmosRbac 'modules/rbac-cosmos-db.bicep' = if (deployAppService && deployCosmosDb) {
  name: 'deploy-rbac-cosmos-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    cosmosDbAccountName: (deployAppService && deployCosmosDb) ? cosmosDb.outputs.name : 'unused'
    #disable-next-line BCP318
    principalId: (deployAppService && deployCosmosDb) ? appServiceModule.outputs.systemAssignedMIPrincipalId : ''
    roleDefinitionId: cosmosDbAccountReaderRoleId
  }
}

// Storage RBAC is handled inline by storage-account.bicep via blobAccessPrincipalId

// ══════════════════════════════════════════════════════════════
// AI — Azure OpenAI (GPT-4o + Whisper)
// ══════════════════════════════════════════════════════════════

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

// OpenAI Key Vault secrets — when deploying a new KV
module keyVaultSecretsNewKv 'modules/openai-keyvault-secrets.bicep' = if (deployKeyVault) {
  name: 'deploy-openai-kv-secrets-new-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    keyVaultName: deployKeyVault ? keyVaultModule.outputs.name : 'unused'
    openAiName: openAi.outputs.name
    openAiDeploymentName: openAi.outputs.deploymentName
    openAiTextDeploymentName: textDeploymentName
    whisperOpenAiName: whisper.outputs.name
    whisperOpenAiResourceGroup: rgWhisper.name
    openAiWhisperDeploymentName: whisper.outputs.whisperDeploymentName
  }
}

// OpenAI Key Vault secrets — when referencing an existing KV (not deploying a new one)
module keyVaultSecretsExistingKv 'modules/openai-keyvault-secrets.bicep' = if (!deployKeyVault && !empty(keyVaultName)) {
  name: 'deploy-openai-kv-secrets-existing-${environmentName}'
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

// ══════════════════════════════════════════════════════════════
// COMMUNICATION — Azure Communication Services (Email + SMS)
// ══════════════════════════════════════════════════════════════

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

// ACS secrets → newly deployed Key Vault
module acsKeyVaultSecretsNewKv 'modules/acs-keyvault-secrets.bicep' = if (deployAcs && deployKeyVault) {
  name: 'deploy-acs-kv-secrets-new-${environmentName}'
  scope: rgPrimary
  params: {
    #disable-next-line BCP318
    keyVaultName: deployKeyVault ? keyVaultModule.outputs.name : 'unused'
    #disable-next-line BCP318
    acsName: deployAcs ? communicationServices.outputs.name : 'unused'
  }
}

// ACS secrets → existing Key Vault
module acsKeyVaultSecretsExistingKv 'modules/acs-keyvault-secrets.bicep' = if (deployAcs && !deployKeyVault && !empty(keyVaultName)) {
  name: 'deploy-acs-kv-secrets-existing-${environmentName}'
  scope: rgPrimary
  params: {
    keyVaultName: keyVaultName
    #disable-next-line BCP318
    acsName: deployAcs ? communicationServices.outputs.name : 'unused'
  }
}

// ══════════════════════════════════════════════════════════════
// FRONTENDS — Static Web Apps (Intake + Manager)
// ══════════════════════════════════════════════════════════════

module swaIntake 'modules/static-web-app.bicep' = if (deploySwa) {
  name: 'deploy-swa-intake-${environmentName}'
  scope: rgSwa
  params: {
    location: swaLocation
    resourceName: swaIntakeName
    skuName: swaSkuName
    tags: sharedTags
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
  }
}

// ══════════════════════════════════════════════════════════════
// DNS — Zone + SWA CNAME records
// ══════════════════════════════════════════════════════════════

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

// ══════════════════════════════════════════════════════════════
// OUTPUTS
// ══════════════════════════════════════════════════════════════

@description('The primary resource group name.')
output primaryResourceGroup string = rgPrimary.name

@description('The Whisper resource group name.')
output whisperResourceGroup string = rgWhisper.name

@description('The SWA resource group name. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaResourceGroup string = deploySwa ? rgSwa.name : ''

// -- OpenAI outputs --
@description('The Azure OpenAI resource endpoint URL (GPT-4o).')
output openAiEndpoint string = openAi.outputs.endpoint

@description('The name of the GPT-4o vision model deployment.')
output openAiDeploymentName string = openAi.outputs.deploymentName

@description('The Whisper Azure OpenAI resource endpoint URL.')
output whisperEndpoint string = whisper.outputs.endpoint

@description('The name of the Whisper speech-to-text model deployment.')
output whisperDeploymentName string = whisper.outputs.whisperDeploymentName

// -- Storage outputs --
@description('The name of the storage account, if deployed.')
#disable-next-line BCP318
output storageAccountName string = deployStorageAccount ? storage.outputs.name : ''

@description('The primary blob endpoint of the storage account, if deployed.')
#disable-next-line BCP318
output storageBlobEndpoint string = deployStorageAccount ? storage.outputs.blobEndpoint : ''

// -- ACS outputs --
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

// -- SWA outputs --
@description('Default hostname for the Intake Static Web App. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaIntakeHostname string = deploySwa ? swaIntake.outputs.defaultHostname : ''

@description('Default hostname for the Manager Static Web App. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaManagerHostname string = deploySwa ? swaManager.outputs.defaultHostname : ''

@secure()
@description('Deployment token for RVS.Blazor.Intake SWA. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaIntakeDeploymentToken string = deploySwa ? swaIntake.outputs.deploymentToken : ''

@secure()
@description('Deployment token for RVS.Blazor.Manager SWA. Empty when deploySwa = false.')
#disable-next-line BCP318
output swaManagerDeploymentToken string = deploySwa ? swaManager.outputs.deploymentToken : ''

// -- DNS outputs --
@description('Azure-assigned nameservers for the DNS zone. Empty when deployDns = false.')
#disable-next-line BCP318
output dnsNameServers array = (deploySwa && deployDns) ? dns.outputs.nameServers : []

@description('FQDN for the Intake SWA custom domain. Empty when deployDns = false.')
#disable-next-line BCP318
output intakeFqdn string = (deploySwa && deployDns) ? dns.outputs.intakeFqdn : ''

@description('FQDN for the Manager SWA custom domain. Empty when deployDns = false.')
#disable-next-line BCP318
output managerFqdn string = (deploySwa && deployDns) ? dns.outputs.managerFqdn : ''

// -- Key Vault outputs --
@description('The Key Vault name, if deployed.')
#disable-next-line BCP318
output deployedKeyVaultName string = deployKeyVault ? keyVaultModule.outputs.name : ''

@description('The Key Vault URI, if deployed.')
#disable-next-line BCP318
output deployedKeyVaultUri string = deployKeyVault ? keyVaultModule.outputs.uri : ''

// -- Cosmos DB outputs --
@description('The Cosmos DB account endpoint, if deployed.')
#disable-next-line BCP318
output cosmosDbEndpoint string = deployCosmosDb ? cosmosDb.outputs.endpoint : ''

@description('The Cosmos DB account name, if deployed.')
#disable-next-line BCP318
output cosmosDbName string = deployCosmosDb ? cosmosDb.outputs.name : ''

// -- App Service outputs --
@description('The App Service default hostname, if deployed.')
#disable-next-line BCP318
output appServiceHostname string = deployAppService ? appServiceModule.outputs.defaultHostname : ''

@description('The App Service name, if deployed.')
#disable-next-line BCP318
output appServiceName string = deployAppService ? appServiceModule.outputs.name : ''

// -- App Insights outputs --
@description('The Application Insights connection string, if deployed.')
#disable-next-line BCP318
output appInsightsConnectionString string = deployObservability ? appInsights.outputs.connectionString : ''

@description('The Application Insights name, if deployed.')
#disable-next-line BCP318
output appInsightsName string = deployObservability ? appInsights.outputs.name : ''

@description('The Log Analytics Workspace name, if deployed.')
#disable-next-line BCP318
output logAnalyticsWorkspaceName string = deployObservability ? logAnalytics.outputs.name : ''
