// ──────────────────────────────────────────────────────────────
// Module: App Service Configuration
// ──────────────────────────────────────────────────────────────
// Applies app settings to an existing Web App and its optional
// staging deployment slot. Deployed after Key Vault and
// Application Insights to break circular dependencies.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Name of the existing Web App to configure.')
param appName string

@description('Application Insights connection string. Leave empty to skip.')
param appInsightsConnectionString string = ''

@description('Key Vault URI for configuration provider. Leave empty to skip.')
param keyVaultUri string = ''

@description('When true, also applies settings to the staging deployment slot with ASPNETCORE_ENVIRONMENT=Staging.')
param configureStagingSlot bool = false

// ── Resources ─────────────────────────────────────────────────

resource webApp 'Microsoft.Web/sites@2024-11-01' existing = {
  name: appName
}

resource appSettings 'Microsoft.Web/sites/config@2024-11-01' = {
  parent: webApp
  name: 'appsettings'
  properties: union(
    configureStagingSlot
      ? {
          ASPNETCORE_ENVIRONMENT: 'Production'
        }
      : {},
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

// ── Staging Slot Settings ─────────────────────────────────────

resource stagingSlot 'Microsoft.Web/sites/slots@2024-11-01' existing = if (configureStagingSlot) {
  parent: webApp
  name: 'staging'
}

resource stagingSlotAppSettings 'Microsoft.Web/sites/slots/config@2024-11-01' = if (configureStagingSlot) {
  parent: stagingSlot
  name: 'appsettings'
  properties: union(
    {
      ASPNETCORE_ENVIRONMENT: 'Staging'
    },
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
