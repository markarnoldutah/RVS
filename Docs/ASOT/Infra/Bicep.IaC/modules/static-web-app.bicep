// ──────────────────────────────────────────────────────────────
// Module: Azure Static Web App
// Deploys a single SWA resource and surfaces the deployment token
// for storage as a GitHub Secret (SWA_TOKEN_*).
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Azure region for the Static Web App management endpoint. Must be an SWA-supported region: westus2, eastus2, centralus, westeurope, or eastasia.')
param location string

@description('Resource name for the Static Web App (e.g. stapp-rvs-intake-dev).')
param resourceName string

@description('Resource tags to apply.')
param tags object

@description('SKU tier. Use Free for dev/test and Standard for staging/production (required for SLA and higher bandwidth).')
@allowed([
  'Free'
  'Standard'
])
param skuName string = 'Free'

// ── Resources ─────────────────────────────────────────────────

resource staticSite 'Microsoft.Web/staticSites@2024-11-01' = {
  name: resourceName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    // Allow PR preview environments to be created automatically.
    stagingEnvironmentPolicy: 'Enabled'
    // Allow staticwebapp.config.json changes at deploy time.
    allowConfigFileUpdates: true
    // Azure Front Door CDN — disabled; use default Azurestaticapps.net CDN.
    enterpriseGradeCdnStatus: 'Disabled'
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('Name of the deployed Static Web App resource.')
output name string = staticSite.name

@description('Default Azure-assigned hostname (e.g. ashy-wave-abc123.azurestaticapps.net). Use this value as the CNAME target when configuring your DNS provider.')
output defaultHostname string = staticSite.properties.defaultHostname

@description('Resource ID of the Static Web App.')
output id string = staticSite.id

@secure()
@description('Deployment API token for GitHub Actions. Store this value as a GitHub Secret named SWA_TOKEN_<APP>_<ENV>.')
output deploymentToken string = staticSite.listSecrets().properties.apiKey
