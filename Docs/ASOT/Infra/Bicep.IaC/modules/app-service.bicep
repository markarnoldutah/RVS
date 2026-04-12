// ──────────────────────────────────────────────────────────────
// Module: Azure App Service (API)
// ──────────────────────────────────────────────────────────────
// Deploys an App Service Plan and Web App for the RVS API with
// system-assigned managed identity. SKU is parameterised for
// easy upgrade from Basic B1 to Standard S1.
//
// B1 limitations accepted for MVP:
//   • No Always On (cold starts expected)
//   • No deployment slots
//   • No autoscale
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

@description('App Service Plan SKU name. B1 = Basic (MVP), S1 = Standard (upgrade path).')
@allowed([
  'B1'
  'S1'
])
param skuName string = 'B1'

@description('The .NET runtime stack version.')
param dotnetVersion string = 'v10.0'

// ── Variables ─────────────────────────────────────────────────

var skuTier = skuName == 'B1' ? 'Basic' : 'Standard'

// Always On is not supported on Basic B1 — only enable for Standard and above
var alwaysOn = skuName != 'B1'

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

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the Web App.')
output resourceId string = webApp.id

@description('Name of the Web App.')
output name string = webApp.name

@description('Default hostname of the Web App (e.g. app-rvs-api-staging-wus3.azurewebsites.net).')
output defaultHostname string = webApp.properties.defaultHostName

@description('Principal ID of the system-assigned managed identity.')
output principalId string = webApp.identity.principalId

@description('App Service Plan resource ID.')
output appServicePlanId string = appServicePlan.id
