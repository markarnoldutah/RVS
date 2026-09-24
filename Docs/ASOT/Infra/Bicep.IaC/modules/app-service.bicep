// ──────────────────────────────────────────────────────────────
// Module: Azure App Service (API)
// ──────────────────────────────────────────────────────────────
// Deploys an App Service Plan, Web App, and optional staging
// deployment slot for the RVS API with system-assigned managed
// identity. SKU is parameterised for easy upgrade.
//
// F1 (Free):    $0/mo, 60 CPU-min/day, no Always On / slots / health checks
// B1 (Basic):   ~$12/mo, no Always On, no deployment slots, no autoscale
// S1 (Standard): ~$58/mo, Always On, deployment slots, autoscale ready
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Azure region for the App Service resources.')
param location string

@description('Resource name for the App Service Plan (e.g. asp-rvs-api-staging-wus3).')
param appServicePlanName string

@description('Resource name for the Web App (e.g. app-rvs-api-staging-wus3).')
param appName string

@description('Tags to apply to all resources created by this module.')
param tags object = {}

@description('App Service Plan SKU name. F1 = Free (staging), B1 = Basic (MVP prod), S1 = Standard (upgrade path).')
@allowed([
  'F1'
  'B1'
  'S1'
])
param skuName string = 'F1'

@description('The .NET runtime version for linuxFxVersion (bare version number, e.g. 8.0, 9.0, 10.0 = no "v" prefix).')
param dotnetVersion string = '10.0'

@description('When true, creates a staging deployment slot with its own managed identity. Requires Standard tier (S1) or higher.')
param deployStagingSlot bool = false

// ── Variables ─────────────────────────────────────────────────

var skuTier = skuName == 'F1' ? 'Free' : skuName == 'B1' ? 'Basic' : 'Standard'

// Always On requires Standard (S1) or higher — Free and Basic do not support it
var alwaysOn = skuName == 'S1'

// Deployment slots require Standard (S1) tier or higher
var slotSupported = skuName == 'S1'
var createSlot = deployStagingSlot && slotSupported

// Health checks are not supported on the Free (F1) tier
var enableHealthCheck = skuName != 'F1'

// File-system logging (#602): a server-side log path that does not depend on
// Application Insights. On Linux, httpLogs.fileSystem is what captures the
// container's stdout/stderr, so the ASP.NET Core console logger lands in
// /home/LogFiles and in `az webapp log tail`. Capped by size and age, so it
// cannot fill the plan's storage.
var fileSystemLogs = {
  applicationLogs: {
    fileSystem: {
      level: 'Information'
    }
  }
  httpLogs: {
    fileSystem: {
      enabled: true
      retentionInMb: 35
      retentionInDays: 3
    }
  }
}

// ── Resources ─────────────────────────────────────────────────

resource appServicePlan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-11-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|${dotnetVersion}'
      alwaysOn: alwaysOn
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      healthCheckPath: enableHealthCheck ? '/health' : ''
    }
  }
}

resource webAppLogs 'Microsoft.Web/sites/config@2024-11-01' = {
  parent: webApp
  name: 'logs'
  properties: fileSystemLogs
}

// ── Staging Deployment Slot

resource stagingSlot 'Microsoft.Web/sites/slots@2024-11-01' = if (createSlot) {
  parent: webApp
  name: 'staging'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|${dotnetVersion}'
      alwaysOn: alwaysOn
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      healthCheckPath: '/health'
    }
  }
}

resource stagingSlotLogs 'Microsoft.Web/sites/slots/config@2024-11-01' = if (createSlot) {
  parent: stagingSlot
  name: 'logs'
  properties: fileSystemLogs
}

// Mark ASPNETCORE_ENVIRONMENT as slot-sticky so it does NOT swap with code
resource slotConfigNames 'Microsoft.Web/sites/config@2024-11-01' = if (createSlot) {
  parent: webApp
  name: 'slotConfigNames'
  properties: {
    appSettingNames: [
      'ASPNETCORE_ENVIRONMENT'
    ]
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the Web App.')
output resourceId string = webApp.id

@description('Name of the Web App.')
output name string = webApp.name

@description('Default hostname of the Web App (e.g. app-rvs-api-staging-wus3.azurewebsites.net).')
output defaultHostname string = webApp.properties.defaultHostName

@description('The site-scoped token an "asuid.<label>" TXT record must carry before a custom hostname can be bound to this Web App. Used for the go.rvintake.com redirect host (Spec A-13, #599).')
output customDomainVerificationId string = webApp.properties.customDomainVerificationId

@description('Principal ID of the system-assigned managed identity.')
output principalId string = webApp.identity.principalId

@description('App Service Plan resource ID.')
output appServicePlanId string = appServicePlan.id

@description('Principal ID of the staging slot managed identity. Empty when no slot is created.')
#disable-next-line BCP318
output stagingSlotPrincipalId string = createSlot ? stagingSlot.identity.principalId : ''

@description('Default hostname of the staging slot (e.g. app-rvs-api-prod-wus3-staging.azurewebsites.net). Empty when no slot.')
#disable-next-line BCP318
output stagingSlotHostname string = createSlot ? stagingSlot.properties.defaultHostName : ''
