// ──────────────────────────────────────────────────────────────
// Module: App Service Configuration
// ──────────────────────────────────────────────────────────────
// Applies app settings to an existing Web App. Deployed after
// Key Vault and Application Insights to break circular deps.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Name of the existing Web App to configure.')
param appName string

@description('Application Insights connection string. Leave empty to skip.')
param appInsightsConnectionString string = ''

@description('Key Vault URI for configuration provider. Leave empty to skip.')
param keyVaultUri string = ''

// ── Resources ─────────────────────────────────────────────────

resource webApp 'Microsoft.Web/sites@2024-11-01' existing = {
  name: appName
}

resource appSettings 'Microsoft.Web/sites/config@2024-11-01' = {
  parent: webApp
  name: 'appsettings'
  properties: union(
    !empty(appInsightsConnectionString)
      ? {
          APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
        }
      : {},
    !empty(keyVaultUri)
      ? {
          KeyVault__VaultUri: keyVaultUri
        }
      : {}
  )
}
