// ──────────────────────────────────────────────────────────────
// Main Orchestration – RVS Azure Infrastructure
// ──────────────────────────────────────────────────────────────
// Deploys at subscription scope to manage resource groups per
// environment (staging / prod). Each environment is deployed
// independently via its own parameter file.
//
// Resource Groups:
//   • rg-rvs-{env}-westus3  — API, Cosmos DB, Storage, Key Vault,
//                              OpenAI GPT-4o, Log Analytics,
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

@description('Optional. Model catalog name for an additional single-purpose deployment used only by the packet preliminary assessment (e.g. "gpt-5"), independent of textDeploymentName. Empty = not deployed; the app falls back to textDeploymentName. This is the intended way to try a stronger model for the assessment call alone, and to revert to gpt-4o (blank this and redeploy) without touching application code.')
param assessmentModelName string = ''

@description('Model version for the assessment deployment (only used when assessmentModelName is set).')
param assessmentModelVersion string = '2025-08-07'

@description('SKU for the assessment deployment. Some newer model families (e.g. gpt-5) are not offered under the regional "Standard" SKU textDeploymentName uses — GlobalStandard routes to wherever Microsoft has capacity, DataZoneStandard keeps inference within the US, matching the residency textDeploymentName already has today.')
@allowed([
  'GlobalStandard'
  'DataZoneStandard'
])
param assessmentDeploymentSkuName string = 'DataZoneStandard'

@description('Assessment deployment capacity in K TPM. Staging = 30, Prod = 40 (set in parameters/*.bicepparam) — a packet call with photos (#772) reserves ~7.6-9.3K, and an intake\'s step-6 question call ~4K when questionsUseAssessmentDeployment is on (#783). Confirm against remaining model quota before raising.')
@minValue(1)
param assessmentDeploymentCapacity int = 1

@description('When true, intake step-6 diagnostic question generation also runs on the assessment deployment (gpt-5, #783); category suggestion and text cleanup stay on textDeploymentName. False (or assessmentModelName blank) keeps questions on gpt-4o — the revert path, no application-code change.')
param questionsUseAssessmentDeployment bool = false

@description('reasoning_effort for step-6 question generation on the assessment deployment. The call is on the customer\'s path, so only the two fastest settings are allowed.')
@allowed([
  'minimal'
  'low'
])
param questionsReasoningEffort string = 'minimal'

// ── Storage Parameters ────────────────────────────────────────

@description('When true, deploys a general-purpose v2 storage account (Standard_LRS) with the rvs-attachments blob container.')
param deployStorageAccount bool = false

@description('Override the storage account name. Leave empty to use the computed default (strvs<env>wus3001). Must be 3-24 lowercase alphanumeric characters and globally unique.')
@maxLength(24)
param storageAccountNameOverride string = ''

@description('Allowed CORS origins for the Storage Account blob service (SAS direct-upload from SPAs).')
param storageCorsOrigins string[] = []

@description('Allow storage account key (shared key) access. Set false in staging/prod to force Entra ID + user-delegation SAS only.')
param storageAllowSharedKeyAccess bool = true

@description('Optional. Object ID of an Entra ID group granted blob data access on the storage account for developer / manual operations (local runs via AzureCliCredential, ops inspection). Set only in non-production parameter files. Empty = no such grant.')
param devBlobAccessPrincipalId string = ''

// ── Messaging Parameters (SendGrid email, Twilio SMS) ─────────
// Neither provider is an Azure resource: accounts, numbers and keys live in the
// SendGrid and Twilio consoles, secrets in Key Vault (SendGrid--ApiKey,
// Twilio--*), set by hand. Bicep owns only the DNS and the app settings.

@description('Sending subdomain for all RVS email (e.g. mail.rvintake.com), authenticated in SendGrid. Set in staging (mail-staging.rvintake.com) and prod (mail.rvintake.com) params. Must be a single-label subdomain of intakeZoneName so Bicep can write its records. Empty = no From address; the API then refuses to build the email sender.')
param mailSendingDomain string = ''

@description('Display name on the email From line, injected as Email__SenderDisplayName. Empty = the API default, "RV Intake" (prod). Staging sets "RV Intake [Staging]" so its mail is distinguishable in an inbox (#828).')
param emailSenderDisplayName string = ''

@description('CNAME records SendGrid domain authentication asks for, copied from the SendGrid console (Settings → Sender Authentication): [{ name: \'em1234.mail\', target: \'u1234.wl.sendgrid.net\' }, { name: \'s1._domainkey.mail\', ... }, { name: \'s2._domainkey.mail\', ... }]. Names are zone-relative to intakeZoneName. Empty until the domain has been added in SendGrid — see the mailSendingDomainAction output.')
param sendGridDnsRecords array = []

@description('Mailbox that receives DMARC aggregate reports (rua=) for the sending domain and, in prod, the rvintake.com apex. Required when mailSendingDomain is set; must be a monitored mailbox or a DMARC-processor address. If it is outside rvintake.com, the reporting domain must publish an RFC 7489 §7.1 authorization record per policy domain — see the dmarcReportAuthorizationAction output. (#532, #608)')
param dmarcReportingAddress string = ''

@description('E.164 toll-free number in this environment\'s Twilio Messaging Service sender pool, injected as Sms__FromPhoneNumber (#661). Bought in the Twilio console, not by Bicep. Empty = no sending number; the API then cannot enable SMS.')
param smsFromPhoneNumber string = ''

@description('Turns outbound SMS on, injected as Sms__Enabled (#661). Leave false until smsFromPhoneNumber has cleared Twilio toll-free verification: unverified toll-free traffic is blocked, and the API refuses to start with SMS enabled and no number.')
param smsEnabled bool = false

@description('The Twilio Messaging Service SID (MG…) whose sender pool holds smsFromPhoneNumber, injected as Twilio__MessagingServiceSid. Not a secret. Sending through it is what applies Advanced Opt-Out, which answers STOP, START and HELP — set it before smsEnabled.')
param twilioMessagingServiceSid string = ''

// ── Static Web App Parameters ─────────────────────────────────

@description('When true, deploys Azure Static Web App resources for Blazor.Intake and Blazor.Manager.')
param deploySwa bool = false

@description('Azure region for Static Web Apps. Must be an SWA-supported region.')
param swaLocation string = 'westus2'

@description('Name of the dedicated resource group for Static Web App resources.')
param swaResourceGroupName string = 'rg-rvs-${environmentName}-westus2'

@description('SWA SKU tier, set per environment in parameters/*.bicepparam. Free supports two custom domains per app, which covers both apps. Standard adds the SLA, SWA-managed custom auth (unused: the Manager signs in with Auth0 from WASM) and a larger app size limit.')
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

@description('Corporate DNS zone. Holds the API origin host (#633) and a no-mail posture (null MX, SPF -all, DMARC reject); no customer-facing hostname lives here. Formerly managerZoneName — the Manager SWA moved to the intake zone in #632.')
param apiZoneName string = 'rvserviceflow.com'

@description('DNS zone for every customer-facing host: Intake (apex in prod, subdomain CNAME in non-prod envs), Manager, the channel-tagging redirect, the email sending domain and the Auth0 login host.')
param intakeZoneName string = 'rvintake.com'

@description('The Intake SWA apex validation token, minted once when the apex was registered out of band. Read it from the zone (`az network dns record-set txt show -z rvintake.com -n @`), not from `az staticwebapp hostname show`, whose validationToken reads blank once the apex is Ready. Prod only. It shares the apex TXT record-set with the SPF string (#652), and a deploy PUTs that whole set, so Bicep has to carry the token or it deletes it. Empty = the apex TXT record-set is not declared at all: no SPF, and nothing already in the zone is removed.')
param intakeApexValidationToken string = ''

@description('Subdomain prefix for the Manager SWA CNAME record, in the INTAKE zone (#632) — "manager" in prod, "manager-<env>" elsewhere. The literal "staging" in the non-prod label is load-bearing: RVS.Blazor.Manager/wwwroot/js/blazor-start.js selects its environment by matching that substring against the browser hostname, so a label without it would boot Production config against staging.')
param managerDnsPrefix string = environmentName == 'prod' ? 'manager' : 'manager-${environmentName}'

@description('Subdomain prefix for the Intake SWA CNAME record in non-prod envs (e.g. "staging" -> staging.rvintake.com). Ignored in prod where Intake binds to the apex.')
param intakeDnsPrefix string = environmentName == 'prod' ? '' : environmentName

@description('Subdomain prefix for the channel-tagging redirect host that fronts the API (Spec A-13, #599) — "go" in prod (go.rvintake.com), "go-<env>" elsewhere (go-staging.rvintake.com). A sibling label rather than a child of the Intake host, so each environment\'s redirect is independent and a single-label wildcard certificate is never needed.')
param redirectDnsPrefix string = environmentName == 'prod' ? 'go' : 'go-${environmentName}'

@description('Subdomain prefix for the API origin host in the corporate zone (#633) — "api" in prod (api.rvserviceflow.com), "api-<env>" elsewhere (api-staging.rvserviceflow.com). This is the origin the browser apps call; it is not customer-facing, which is why it stays on rvserviceflow.com while every host a human reads moved to rvintake.com. Note the Auth0 resource-server identifier is the same string, but that is an opaque audience value and unrelated — do not couple them.')
param apiDnsPrefix string = environmentName == 'prod' ? 'api' : 'api-${environmentName}'

@description('Auth0 Universal Login host in the INTAKE zone (Auth0 checklist §6, #627). Deliberately NOT environment-suffixed, unlike every other prefix above: the Free plan includes exactly one custom domain and dev/staging/prod share one Auth0 tenant (#610), so there is a single login host serving all three. A subdomain is also forced — Auth0 does not support an apex custom domain, and the rvintake.com apex is the Intake SWA.')
param auth0LoginDnsPrefix string = 'login'

@description('CNAME target Auth0 mints for the custom domain, shown once on Branding -> Custom Domains after the domain is added (shaped like <tenant>-cd-<hash>.edge.tenants.us.auth0.com). EMPTY UNTIL THE DOMAIN IS CREATED IN THE PORTAL — the record is then a no-op and login stays on the canonical tenant domain. Tenant-wide and environment-independent, so both environments upsert the identical record and this is not env-guarded; that is also why it is a hand-entered string rather than something the template can derive. Same shape of out-of-band token as the SendGrid domain-authentication CNAMEs. Fill it in as Auth0 checklist §6.3, then deploy the DNS resource group.')
param auth0CustomDomainCnameTarget string = 'dev-2jhzz8xmjggh26pm-cd-mdl9vngn46azp32y.edge.tenants.us.auth0.com'

@description('Object IDs of principals (e.g. the staging GitHub Actions service principal) that need DNS Zone Contributor on the shared zones. Granted at zone scope so they cannot touch other prod resources. Set this in prod params, not staging.')
param dnsZoneContributorPrincipalIds string[] = []

// ── App Service Parameters ────────────────────────────────────

@description('When true, deploys an App Service Plan and Web App for the RVS API with Managed Identity.')
param deployAppService bool = false

@description('App Service Plan SKU, set per environment in parameters/*.bicepparam. F1 = Free (60 CPU-min/day, no custom hostnames or certificates). B1 = Basic. S1 = Standard (adds Always On and slots).')
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

@secure()
@description('Auth0 tenant domain for the "RVS API Provisioner" M2M app used by the platform-admin tool (issue #563). Optional; the Auth0Provisioner--* secrets are written only when domain, client ID and secret are all set.')
param auth0ProvisionerDomain string = ''

@secure()
@description('Client ID of the "RVS API Provisioner" M2M app (issue #563). Optional.')
param auth0ProvisionerClientId string = ''

@secure()
@description('Client secret of the "RVS API Provisioner" M2M app (issue #563). Optional.')
param auth0ProvisionerClientSecret string = ''

@secure()
@description('Auth0 user id (sub) allowed to use the platform-admin tool, written as Admin--AllowedUserIds--0 (issue #563). Optional.')
param adminAllowedUserId string = ''

// ── Observability Parameters ──────────────────────────────────

@description('When true, deploys a Log Analytics workspace and Application Insights resource.')
param deployObservability bool = false

@description('When true and deployObservability + deployAppService are both true, creates a standard availability test on the API /health endpoint.')
param deployAvailabilityTest bool = false

@description('Seconds between availability test runs, per location. 900 = every 15 minutes (the cheap setting), 300 = every 5. Only used when the test is deployed.')
@allowed([
  300
  900
])
param availabilityTestFrequencySeconds int = 900

@description('Availability test location IDs. Each location is billed per run: one location at 900 s is ~2.9K runs/month, three at 300 s ~26K. Add locations as traffic grows; the availability alert waits for all but one to fail. Only used when the test is deployed.')
@minLength(1)
param availabilityTestLocations array = [
  'us-ca-sjc-azr'
]

@description('Log Analytics workspace retention in days. 30 is the floor the PerGB2018 SKU accepts, and the first 31 days are included in the ingestion price, so values below 31 do not lower the bill. Ingestion volume is what drives cost.')
@minValue(30)
@maxValue(730)
param logAnalyticsRetentionInDays int = 30

@description('Log Analytics daily ingestion cap in GB, as a decimal string (Bicep has no float type). \'-1\' = no cap. Workspace-based App Insights ingests into this workspace, so this caps API telemetry too. When the cap is hit, ingestion stops until the daily reset, and the packet-pipeline alerts (#494) go blind with it.')
param logAnalyticsDailyCapGb string = '-1'

@description('Email receivers for the ops action group that packet-pipeline critical alerts route to (#494). Each item: { name: string, email: string }. Committed as a real default in both param files (#639) — the Action Groups resource provider does a full-replace PUT, so an empty array here deletes any receiver added by hand in the portal on the next deploy; leaving it empty is not a safe way to defer setting a receiver. Only used when deployObservability = true.')
param opsAlertEmailReceivers array = []

// ── Variables ─────────────────────────────────────────────────

// Storage account names must be 3-24 lowercase alphanumeric characters with no hyphens.
var defaultStorageAccountName = 'strvs${environmentName}wus3001'
var resolvedStorageAccountName = empty(storageAccountNameOverride)
  ? defaultStorageAccountName
  : storageAccountNameOverride

// Environment-aware default CORS origins for browser-based SAS uploads.
// Two branches only: environmentName is constrained to staging|prod above, so a third
// (localhost) arm was unreachable — and it listed a port 7008 no project has ever served.
// Local development does not deploy this template; it uses Cors:AllowedOrigins in
// RVS.API/appsettings.Development.json.
var defaultCorsOrigins = environmentName == 'prod'
  ? ['https://rvintake.com', 'https://manager.rvintake.com']
  : ['https://staging.rvintake.com', 'https://manager-staging.rvintake.com']

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
    retentionInDays: logAnalyticsRetentionInDays
    dailyQuotaGb: logAnalyticsDailyCapGb
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
    availabilityTestFrequencySeconds: availabilityTestFrequencySeconds
    availabilityTestLocations: availabilityTestLocations
  }
}

// ── Monitor Alerts (packet-pipeline critical events #494; availability + telemetry-dark #602) ──

module monitorAlerts 'modules/monitor-alerts.bicep' = if (deployObservability) {
  name: 'deploy-alerts-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    #disable-next-line BCP318
    appInsightsResourceId: deployObservability ? appInsights.outputs.resourceId : ''
    #disable-next-line BCP318
    logAnalyticsWorkspaceResourceId: deployObservability ? logAnalytics.outputs.resourceId : ''
    environmentName: environmentName
    tags: sharedTags
    opsEmailReceivers: opsAlertEmailReceivers
    #disable-next-line BCP318
    availabilityTestId: deployObservability ? appInsights.outputs.availabilityTestId : ''
    availabilityTestLocationCount: length(availabilityTestLocations)
    availabilityTestFrequencySeconds: availabilityTestFrequencySeconds
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
    emailFromAddress: emailFromAddress
    emailSenderDisplayName: emailSenderDisplayName
    smsEnabled: smsEnabled
    smsFromPhoneNumber: smsFromPhoneNumber
    twilioMessagingServiceSid: twilioMessagingServiceSid
    // Twilio signs each webhook over the URL it called, and calls back here.
    twilioWebhookBaseUrl: 'https://${apiDnsPrefix}.${apiZoneName}'
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
    allowSharedKeyAccess: storageAllowSharedKeyAccess
    devBlobAccessPrincipalId: devBlobAccessPrincipalId
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

// A single-entry array today (the preliminary-assessment model), built here rather
// than passed as a raw object literal so openai.bicep stays reusable for whatever
// gets added next (e.g. issue #584's "another module to switch to Sonnet" note —
// though a Claude/Foundry deployment lives on a different resource kind entirely
// and would need its own module, not another entry here).
var additionalOpenAiDeployments = empty(assessmentModelName) ? [] : [
  {
    name: assessmentModelName
    modelName: assessmentModelName
    modelVersion: assessmentModelVersion
    skuName: assessmentDeploymentSkuName
    capacity: assessmentDeploymentCapacity
  }
]

module openAi 'modules/openai.bicep' = {
  name: 'deploy-openai-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    tags: openAiNaming.outputs.tags
    deploymentCapacity: openAiCapacity
    resourceName: openAiNaming.outputs.resourceName
    additionalDeployments: additionalOpenAiDeployments
  }
}

// Name of the assessment-only model deployment, or empty when assessmentModelName is unset.
var openAiAssessmentDeploymentName = empty(assessmentModelName) ? '' : openAi.outputs.additionalDeploymentNames[0]

// Step-6 question generation shares that deployment when switched on (#783); empty keeps it on gpt-4o.
var openAiQuestionsDeploymentName = questionsUseAssessmentDeployment ? openAiAssessmentDeploymentName : ''

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
    openAiAssessmentDeploymentName: openAiAssessmentDeploymentName
    openAiQuestionsDeploymentName: openAiQuestionsDeploymentName
    openAiQuestionsReasoningEffort: questionsReasoningEffort
    whisperOpenAiName: whisper.outputs.name
    whisperOpenAiResourceGroup: rgWhisper.name
    openAiWhisperDeploymentName: whisper.outputs.whisperDeploymentName
  }
}

// ── Email sending domain (SendGrid) ───────────────────────────
// True in staging (mail-staging.rvintake.com) and prod (mail.rvintake.com).
var mailSendingDomainOn = !empty(mailSendingDomain)

// Every RVS email goes From DoNotReply@ the sending subdomain. SendGrid refuses a
// sender on a domain it has not authenticated, so this is only correct once the
// sendGridDnsRecords below resolve and SendGrid shows the domain Verified.
var emailFromAddress = mailSendingDomainOn ? 'DoNotReply@${mailSendingDomain}' : ''

// The subdomain's own label under the intake zone — 'mail' in prod,
// 'mail-staging' in staging. A single label in every environment (#634).
var mailSendingSubLabel = mailSendingDomainOn ? replace(mailSendingDomain, '.${intakeZoneName}', '') : ''

// SendGrid domain authentication with automated security asks for three CNAMEs:
// the return path (em1234.mail → u1234.wl.sendgrid.net), which carries SPF for
// the envelope sender, and two DKIM selectors (s1/s2._domainkey.mail). They are
// minted per domain in the SendGrid console, so they arrive as a parameter, with
// names already zone-relative. Nothing is needed at the subdomain label itself.
var sendGridCnameRecords = mailSendingDomainOn ? sendGridDnsRecords : []

// DMARC is authored here so we control the policy and the reporting address:
// p=none surfaces failures without dropping mail while the domain warms. Relaxed
// alignment is what lets SendGrid's d=mail.rvintake.com DKIM signature and its
// em1234.mail.rvintake.com envelope both align with the From domain. Reports
// reach a human only if the reporting domain authorizes them — see "DMARC
// aggregate-report destination (#608)" below.
var mailSendingDmarcTxtRecords = mailSendingDomainOn ? [
  {
    name: '_dmarc.${mailSendingSubLabel}'
    values: [ 'v=DMARC1; p=none; rua=mailto:${dmarcReportingAddress}; adkim=r; aspf=r' ]
  }
] : []

// ── DMARC aggregate-report destination (#608) ──
//
// Reports go to dmarcReportingAddress, which since #608 is
// support@arnolddigitalsolutions.com, a monitored mailbox on the filing entity's
// own domain. It is on a different organizational domain from
// the records that name it, so RFC 7489 §7.1 applies: before sending, a receiver
// looks up <policy-domain>._report._dmarc.<rua-domain> for a TXT "v=DMARC1", and
// a conforming one drops the report if it is missing. Those records belong in
// the arnolddigitalsolutions.com zone, which is at its registrar, not in Azure, so
// this template cannot declare them. It computes their exact names instead and
// prints them in the dmarcReportAuthorizationAction output, the same way other
// out-of-band steps are surfaced. Any policy domain whose record carries this
// rua needs one: the sending subdomain in every environment and the
// rvintake.com apex in prod.
//
// One explicit record per policy domain, not a *._report._dmarc wildcard. A
// wildcard would let any domain on the internet send its reports to that mailbox.
//
// "Outside rvintake.com" is judged by suffix, which is enough for a single-label
// TLD like .com. An address inside the intake zone needs no authorization record.
var dmarcReportingDomain = empty(dmarcReportingAddress) ? '' : toLower(last(split(dmarcReportingAddress, '@')))
var dmarcReportingIsExternal = !empty(dmarcReportingDomain) && dmarcReportingDomain != intakeZoneName && !endsWith(dmarcReportingDomain, '.${intakeZoneName}')
var dmarcReportAuthorizationNames = dmarcReportingIsExternal ? concat(
  mailSendingDomainOn ? [ '${mailSendingDomain}._report._dmarc.${dmarcReportingDomain}' ] : [],
  (intakeApexIsManaged && deploySwa && deployDns) ? [ '${intakeZoneName}._report._dmarc.${dmarcReportingDomain}' ] : []
) : []

// ── go.rvintake.com — the channel-tagging redirect (Spec A-13, #599) ──
//
// Every distribution path — QR sticker, texted link, printed card — is supposed to route
// through this host so the hit is logged and the channel observed before the customer reaches
// the intake form. It fronts the API, not the Intake SWA, because the redirect has to write to
// the hit log; a static host could serve the redirect but could not count it.
//
// Declared here: the CNAME to the Web App and the "asuid" ownership TXT, whose value the site
// itself supplies (customDomainVerificationId), so neither needs a human.
//
// NOT declared here, deliberately, and for the same reason the Intake apex binding is not:
// the hostname binding waits on DNS to validate, and the App Service managed certificate waits
// on the binding, so a first bring-up from a single template deadlocks on records the same
// template has not written yet. Both are one-time out-of-band steps — README.md "Bind the
// go.<zone> redirect host". Redeploys never touch them.
var redirectCnameRecords = deployAppService ? [
  {
    name: redirectDnsPrefix
    #disable-next-line BCP318
    target: appService.outputs.defaultHostname
  }
] : []

var redirectTxtRecords = deployAppService ? [
  {
    name: 'asuid.${redirectDnsPrefix}'
    #disable-next-line BCP318
    values: [ appService.outputs.customDomainVerificationId ]
  }
] : []

// ── api.rvserviceflow.com — the API origin (#633) ─────────────
//
// Until this, api.rvserviceflow.com existed only as the Auth0 resource-server identifier: an
// opaque audience string with no DNS behind it, while both Blazor apps called the API at its
// *.azurewebsites.net default hostname. This binds the name for real.
//
// In the CORPORATE zone, unlike every other host: an XHR origin is not something a customer
// reads. The redirect host above fronts the same Web App from the intake zone, because a link
// on a QR sticker very much is.
//
// Same split as the redirect host: the CNAME and the "asuid" ownership TXT are declared here
// (the site supplies customDomainVerificationId itself, so neither needs a human), but the
// hostname binding and the managed certificate are NOT — the binding waits on DNS to validate
// and the certificate waits on the binding, so a first bring-up from a single template
// deadlocks on records that template has not written yet. Both are one-time out-of-band steps,
// per environment — README.md "Bind the api.<zone> host". Redeploys never touch them.
var apiCnameRecords = deployAppService ? [
  {
    name: apiDnsPrefix
    #disable-next-line BCP318
    target: appService.outputs.defaultHostname
  }
] : []

var apiTxtRecords = deployAppService ? [
  {
    name: 'asuid.${apiDnsPrefix}'
    #disable-next-line BCP318
    values: [ appService.outputs.customDomainVerificationId ]
  }
] : []

// ── manager.rvintake.com — the dealer-facing Manager SWA (#632) ────
//
// In the INTAKE zone, not the corporate one: a service advisor signs in here, so it carries the
// brand every other host they and their customers touch already carries. The corporate zone
// keeps the API origin, which nobody types.
//
// Unlike the redirect host above, this one binds entirely in-template — it is a subdomain
// CNAME, so swa-custom-domain.bicep can validate it with cname-delegation once the record
// exists. That is why swaManagerDomain below dependsOn dnsIntake.
var managerCnameRecords = [
  {
    name: managerDnsPrefix
    #disable-next-line BCP318
    target: swaManager.outputs.defaultHostname
  }
]

// ── login.rvintake.com — the Auth0 Universal Login host (#627) ──────
//
// In the INTAKE zone with every other host a human reads: a raw dev-<hash>.us.auth0.com address
// in the browser bar is the biggest "this looks sketchy" tell for a service advisor signing in.
//
// One record for all three environments, not one per environment. The Free plan includes exactly
// one custom domain and dev/staging/prod share a single Auth0 tenant (#610), so there is nothing
// to suffix — both environments' deploys upsert the same name with the same value, by design.
//
// Inert until auth0CustomDomainCnameTarget is filled in: the value is a token Auth0 mints when
// the domain is added in the portal, so it cannot be derived here, and until it exists this
// evaluates to an empty list and the deploy writes nothing. Auth0 checklist §6.3.
//
// If Auth0 asks for a TXT verification record instead of a CNAME, build the same shape
// ({ name: auth0LoginDnsPrefix, values: [ '...' ] }) and append it to dnsIntake's txtRecords.
var auth0CnameRecords = empty(auth0CustomDomainCnameTarget) ? [] : [
  {
    name: auth0LoginDnsPrefix
    target: auth0CustomDomainCnameTarget
  }
]

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

// ── Key Vault Secrets (Storage — Blob endpoint) ──────────────

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
    auth0ProvisionerDomain: auth0ProvisionerDomain
    auth0ProvisionerClientId: auth0ProvisionerClientId
    auth0ProvisionerClientSecret: auth0ProvisionerClientSecret
    adminAllowedUserId: adminAllowedUserId
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

// Custom-domain bindings are declared further down, AFTER the DNS modules —
// see "SWA custom-domain bindings". The SWA resources themselves carry none.

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

// ── DNS: corporate zone (rvserviceflow.com) ───────────────
// No customer-facing hostname lives here: the Manager SWA moved to the intake
// zone in #632, so every host a human reads is on rvintake.com. What remains is
// the API origin (#633) — an XHR target, seen in devtools and a CSP, not on a
// sticker. The DMARC rua mailbox is not here either: it has been on
// arnolddigitalsolutions.com since #608.
//
// Only the CNAME and asuid TXT are declared. The hostname binding and managed
// certificate are out-of-band, one-time, per environment: see the apiCnameRecords
// comment above and README.md "Bind the api.<zone> host".

// Mail posture for the corporate zone. It neither sends nor receives: the packet
// email goes out From mail.rvintake.com, and there are no mailboxes here.
// Saying so explicitly is what stops the domain being usable for spoofing — with
// no SPF and no DMARC, anyone can forge From: anything@rvserviceflow.com and a
// receiver has nothing to check it against. The domain appears in the pilot
// agreement's history and in the JWT claim namespace, so it is guessable.
//
//   null MX (RFC 7505) — preference 0, exchange "." — "accepts no mail", so a
//     sender fails immediately rather than retrying for days. Replace this entry
//     with a real exchanger if corporate mailboxes are ever added here.
//   SPF "-all" with no mechanisms — no host is authorised to send as this domain.
//   DMARC p=reject — act on that, rather than merely publishing it.
//
// No rua on this record, deliberately. The reporting address
// (dmarcReportingAddress) is on another organizational domain, so a rua here
// would need one more RFC 7489 §7.1 authorization record at the registrar. A
// policy-only DMARC record is valid, needs no such record, and there is nothing
// here worth reporting on anyway.
var corporateNullMxRecords = [
  {
    name: '@'
    records: [ { preference: 0, exchange: '.' } ]
  }
]

var corporateMailPolicyTxtRecords = [
  {
    name: '@'
    values: [ 'v=spf1 -all' ]
  }
  {
    name: '_dmarc'
    values: [ 'v=DMARC1; p=reject; adkim=s; aspf=s' ]
  }
]

module dnsApi 'modules/dns.bicep' = if (deploySwa && deployDns) {
  name: 'deploy-dns-api-${environmentName}'
  scope: resourceGroup(dnsResourceGroupName)
  params: {
    zoneName: apiZoneName
    cnameRecords: apiCnameRecords
    txtRecords: concat(apiTxtRecords, corporateMailPolicyTxtRecords)
    mxRecords: corporateNullMxRecords
  }
}

// ── DNS: Intake zone (rvintake.com) ────────────────────────────
// Non-prod: subdomain CNAME (e.g. staging.rvintake.com → default SWA hostname),
//           bound below via cname-delegation like the Manager zone.
// Prod:     the apex. CNAME at apex is invalid (RFC 1034), so this declares an
//           ALIAS A record targeting the Intake SWA resource — Azure DNS tracks
//           the SWA's address itself, nothing is pinned in source.
//
//           What is deliberately NOT declared for the apex:
//             • the TXT ownership record  — its value is a token Azure mints
//               when the custom domain is first registered, so it cannot be
//               known at authoring time; and
//             • the customDomains binding — its PUT waits for that TXT to
//               validate, and re-issuing it from Bicep on every redeploy is a
//               risk with no benefit once it is Ready.
//           Both are created ONCE, out of band, by the portal's "Custom Domain
//           on Azure DNS" flow (or the CLI equivalent) — README.md "Deploy
//           Production". Incremental deploys leave them alone thereafter.

// The Intake zone also carries the sending-domain records (SendGrid CNAMEs and
// DMARC for mail.rvintake.com in prod, mail-staging.rvintake.com in staging)
// when mailSendingDomain is set — see "Email sending domain (SendGrid)" above.
// The two use distinct record names, so neither environment's deploy touches the
// other's. An env without mailSendingDomain leaves them empty.
//
// It also carries the Auth0 Universal Login record (login.rvintake.com, #627) once
// auth0CustomDomainCnameTarget is set — see "login.rvintake.com" above. That one is
// tenant-wide rather than per-environment, so unlike every other record here both
// environments write the same name and value.

// Mail posture for the intake APEX (#652). The apex sends no mail: the packet
// email goes out From mail.rvintake.com (prod) / mail-staging.rvintake.com
// (staging), and each of those has its own _dmarc record above. Without SPF and
// DMARC here, anyone could forge From: anything@rvintake.com (the brand customers
// actually see) and a receiver would have nothing to check it against.
// rvserviceflow.com got the same treatment in #651.
//
//   SPF "-all" with no mechanisms: no host is authorised to send as the apex.
//   DMARC p=reject; sp=reject: act on failures, for the apex and for every
//     subdomain that lacks a _dmarc record of its own.
//
// ⚠ READ THIS BEFORE ADDING A SENDING SUBDOMAIN. A DMARC policy applies to every
// subdomain with no record of its own, and this one says reject. A new sender
// under rvintake.com (a second SendGrid domain, a marketing provider)
// that is not given its own _dmarc record has ALL of its mail rejected from the
// first message. Nothing on the sending side tells you why, and nobody changed a
// record. Give the new subdomain a _dmarc record, the way mailSendingDmarcTxtRecords
// does, in the same change that starts it sending.
//
// Why sp=reject rather than sp=none: mail.rvintake.com is expected to stay the
// only sender. sp=none would keep future senders working without that record,
// but every subdomain would be spoofable until someone remembered to add one.
// sp= is written out even though reject is also what it would inherit from p=,
// so the choice is visible here and not left to a default.
//
// Null MX and rua (#608). DMARC reports go to dmarcReportingAddress on another
// domain, so nothing needs to receive mail at rvintake.com. The null MX (RFC 7505:
// preference 0, exchange ".") says so, and a sender fails at once instead of
// retrying for days. The rua lets the apex report forgery attempts, and does the
// same for any subdomain without its own record. It needs its own §7.1
// authorization record, which is listed in dmarcReportAuthorizationAction.
// Replace the null MX with a real exchanger only if mailboxes are ever added at
// rvintake.com.
//
// Prod deploy only, like the apex ALIAS: staging writes into this same zone and
// never touches "@". The SPF string shares the apex TXT record-set with the SWA
// validation token, and dns.bicep replaces a record-set wholesale. So the TXT set
// is declared only when intakeApexValidationToken is supplied, and a deploy that
// cannot re-assert the token never removes it. DMARC and MX live in their own
// record-sets, so they have no such dependency.
var intakeApexIsManaged = environmentName == 'prod'

var intakeApexSpfTxtRecords = (intakeApexIsManaged && !empty(intakeApexValidationToken)) ? [
  {
    name: '@'
    values: [ intakeApexValidationToken, 'v=spf1 -all' ]
  }
] : []

var intakeApexDmarcTxtRecords = intakeApexIsManaged ? [
  {
    name: '_dmarc'
    values: [ 'v=DMARC1; p=reject; sp=reject; adkim=s; aspf=s${empty(dmarcReportingAddress) ? '' : '; rua=mailto:${dmarcReportingAddress}'}' ]
  }
] : []

var intakeApexNullMxRecords = intakeApexIsManaged ? [
  {
    name: '@'
    records: [ { preference: 0, exchange: '.' } ]
  }
] : []

module dnsIntake 'modules/dns.bicep' = if (deploySwa && deployDns) {
  name: 'deploy-dns-intake-${environmentName}'
  scope: resourceGroup(dnsResourceGroupName)
  params: {
    zoneName: intakeZoneName
    cnameRecords: concat(environmentName == 'prod' ? [] : [
      {
        name: intakeDnsPrefix
        #disable-next-line BCP318
        target: swaIntake.outputs.defaultHostname
      }
    ], managerCnameRecords, sendGridCnameRecords, redirectCnameRecords, auth0CnameRecords)
    aRecords: environmentName == 'prod' ? [
      {
        name: '@'
        #disable-next-line BCP318
        targetResourceId: swaIntake.outputs.id
      }
    ] : []
    txtRecords: concat(mailSendingDmarcTxtRecords, redirectTxtRecords, intakeApexSpfTxtRecords, intakeApexDmarcTxtRecords)
    mxRecords: intakeApexNullMxRecords
  }
}

// ── SWA custom-domain bindings ─────────────────────────────────
// Ordered AFTER the dns modules on purpose: the customDomains PUT is a
// long-running operation that waits for the CNAME to validate, so the record
// must exist before the binding is requested (a first bring-up otherwise
// deadlocks waiting for a record a later module would write). Idempotent on
// redeploy — an already-Ready binding is a no-op.
//
// Intake in prod binds the apex with dns-txt-token and is handled out of band
// (see the Intake zone comment above), hence the environmentName guard.

module swaManagerDomain 'modules/swa-custom-domain.bicep' = if (deploySwa && deployDns) {
  name: 'deploy-swa-manager-domain-${environmentName}'
  scope: rgSwa
  dependsOn: [
    dnsIntake
  ]
  params: {
    #disable-next-line BCP318
    staticSiteName: swaManager.outputs.name
    hostname: '${managerDnsPrefix}.${intakeZoneName}'
    validationMethod: 'cname-delegation'
  }
}

module swaIntakeDomain 'modules/swa-custom-domain.bicep' = if (deploySwa && deployDns && environmentName != 'prod') {
  name: 'deploy-swa-intake-domain-${environmentName}'
  scope: rgSwa
  dependsOn: [
    dnsIntake
  ]
  params: {
    #disable-next-line BCP318
    staticSiteName: swaIntake.outputs.name
    hostname: '${intakeDnsPrefix}.${intakeZoneName}'
    validationMethod: 'cname-delegation'
  }
}

// ── DNS RBAC: zone-scoped grants for cross-env deployers ──────
// Run only from the prod deployment (the prod RG owns the zones).
// Grants DNS Zone Contributor on each zone — NOT on the RG — so
// non-prod deployers can write record sets without broader access.

module dnsApiRbac 'modules/dns-zone-contributor.bicep' = if (deploySwa && deployDns && environmentName == 'prod' && !empty(dnsZoneContributorPrincipalIds)) {
  name: 'deploy-dns-api-rbac-${environmentName}'
  scope: resourceGroup(dnsResourceGroupName)
  dependsOn: [
    dnsApi
  ]
  params: {
    zoneName: apiZoneName
    principalIds: dnsZoneContributorPrincipalIds
  }
}

module dnsIntakeRbac 'modules/dns-zone-contributor.bicep' = if (deploySwa && deployDns && environmentName == 'prod' && !empty(dnsZoneContributorPrincipalIds)) {
  name: 'deploy-dns-intake-rbac-${environmentName}'
  scope: resourceGroup(dnsResourceGroupName)
  dependsOn: [
    dnsIntake
  ]
  params: {
    zoneName: intakeZoneName
    principalIds: dnsZoneContributorPrincipalIds
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
output openAiAssessmentDeploymentName string = openAiAssessmentDeploymentName
output openAiQuestionsDeploymentName string = openAiQuestionsDeploymentName

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

@description('Ops action group resource ID for packet-pipeline alerts (#494). Empty when deployObservability = false.')
#disable-next-line BCP318
output opsActionGroupId string = deployObservability ? monitorAlerts.outputs.actionGroupId : ''

@description('Manual follow-up when opsAlertEmailReceivers is empty: the ops action group deploys with no receivers and no alert reaches a human until one is added. Do not "fix" this by adding a receiver in the portal instead — the Action Groups resource provider does a full-replace PUT, so the next deploy silently deletes it (#639). Set the parameter and redeploy.')
#disable-next-line BCP318
output opsAlertReceiverAction string = (deployObservability && empty(opsAlertEmailReceivers))
  ? 'ACTION REQUIRED: set opsAlertEmailReceivers and redeploy — e.g. --parameters opsAlertEmailReceivers=\'[{"name":"oncall","email":"..."}]\'. Do NOT add a receiver via the portal instead: the next deploy will silently delete it (#639). Until a receiver is set via this parameter, packet-pipeline critical alerts fire but notify nobody.'
  : ''

// ── Messaging ─────────────────────────────────────────────────

@description('Email From address applied to the API app settings. Empty when mailSendingDomain is unset.')
output emailFromAddress string = emailFromAddress

@description('Manual follow-up for the sending domain. Bicep writes the SendGrid CNAMEs and the _dmarc record, but the domain is added and verified in the SendGrid console. Empty when mailSendingDomain is unset.')
output mailSendingDomainAction string = mailSendingDomainOn
  ? (empty(sendGridDnsRecords)
      ? 'ACTION REQUIRED: in SendGrid (Settings → Sender Authentication), authenticate ${mailSendingDomain} with automated security on, copy its three CNAMEs into sendGridDnsRecords (zone-relative names) in the parameter file, redeploy, then press Verify in SendGrid. Until then every email is refused.'
      : 'SendGrid CNAMEs for ${mailSendingDomain} are written. If SendGrid does not show the domain Verified, press Verify there; see Infra/Bicep.IaC/README.md "Email — SendGrid".')
  : ''

@description('Manual DNS step that Bicep cannot do (#608). dmarcReportingAddress is on another organizational domain, so RFC 7489 §7.1 requires that domain to publish a TXT "v=DMARC1" at <policy-domain>._report._dmarc.<rua-domain> for every policy domain naming it, or conforming receivers drop the reports. That zone is at its registrar, outside Azure. Empty when the address is inside the intake zone or unset.')
output dmarcReportAuthorizationAction string = empty(dmarcReportAuthorizationNames)
  ? ''
  : 'MANUAL DNS (outside Azure): in the ${dmarcReportingDomain} zone at its registrar, make sure a TXT record with value "v=DMARC1" exists at each of: ${join(dmarcReportAuthorizationNames, ', ')}. Without them, DMARC aggregate reports for these domains are dropped. See Infra/Bicep.IaC/README.md "DMARC aggregate reports".'

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

@description('Azure-assigned nameservers for the corporate DNS zone (rvserviceflow.com). Empty when deployDns = false.')
#disable-next-line BCP318
output dnsApiNameServers array = (deploySwa && deployDns) ? dnsApi.outputs.nameServers : []

@description('Azure-assigned nameservers for the Intake DNS zone (rvintake.com). Empty when deployDns = false.')
#disable-next-line BCP318
output dnsIntakeNameServers array = (deploySwa && deployDns) ? dnsIntake.outputs.nameServers : []

@description('FQDN for the Intake SWA custom domain — apex in prod, subdomain elsewhere.')
output intakeFqdn string = environmentName == 'prod' ? intakeZoneName : '${intakeDnsPrefix}.${intakeZoneName}'

@description('FQDN of the channel-tagging redirect host (Spec A-13, #599). Its DNS records are deployed; the hostname binding and managed certificate are one-time out-of-band steps — README.md "Bind the go.<zone> redirect host".')
output redirectFqdn string = '${redirectDnsPrefix}.${intakeZoneName}'

@description('FQDN of the API origin host (#633). Its DNS records are deployed; the hostname binding and managed certificate are one-time out-of-band steps — README.md "Bind the api.<zone> host".')
output apiFqdn string = '${apiDnsPrefix}.${apiZoneName}'

@description('FQDN for the Manager SWA custom domain, in the intake zone (#632).')
output managerFqdn string = '${managerDnsPrefix}.${intakeZoneName}'

@description('Manual follow-up after a prod deploy. Bicep writes the rvintake.com apex ALIAS record but cannot bind the apex custom domain — Azure mints the ownership token only at registration time. Until this one-time step is done, rvintake.com resolves but https:// fails with a cert error. Empty for non-prod (subdomain CNAMEs bind in-template).')
output intakeApexAction string = (deploySwa && deployDns && environmentName == 'prod')
  ? 'ACTION REQUIRED: register the rvintake.com apex on the Intake SWA (dns-txt-token) — see Infra/Bicep.IaC/README.md "Deploy Production" step 2.'
  : ''

@description('Manual follow-up after any deploy that first introduces the API origin host (#633). Bicep writes the CNAME and the asuid ownership TXT, but the App Service hostname binding waits on DNS to validate and the managed certificate waits on the binding, so neither can be declared in the same template that writes the records. Until this one-time step is run, the host resolves but TLS serves the *.azurewebsites.net wildcard and the apps must keep calling the default hostname.')
output apiHostBindingAction string = (deployAppService && deployDns)
  ? 'ACTION REQUIRED: bind ${apiDnsPrefix}.${apiZoneName} on the API Web App and issue its managed certificate — see Infra/Bicep.IaC/README.md "Bind the api.<zone> host". Once it answers, repoint ApiBaseUrl in both Blazor apps and drop the *.azurewebsites.net entries from the Manager CSP.'
  : ''
