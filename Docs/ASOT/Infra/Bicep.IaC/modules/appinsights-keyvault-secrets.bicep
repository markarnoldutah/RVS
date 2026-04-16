// ──────────────────────────────────────────────────────────────
// Module: Store Application Insights connection string in Key Vault
// ──────────────────────────────────────────────────────────────
// Stores the Application Insights connection string in Key Vault
// so the RVS API can read it via the Azure Key Vault configuration
// provider at runtime (ApplicationInsights--ConnectionString).
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The name of the existing Key Vault where secrets will be stored.')
param keyVaultName string

@description('The name of the existing Application Insights resource in the current resource group.')
param appInsightsName string

// ── Existing Resource References ──────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

// ── Key Vault Secrets ─────────────────────────────────────────

@description('Application Insights connection string — used by the API for telemetry and logging.')
resource appInsightsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'ApplicationInsights--ConnectionString'
  properties: {
    value: appInsights.properties.ConnectionString
    contentType: 'text/plain'
  }
}
