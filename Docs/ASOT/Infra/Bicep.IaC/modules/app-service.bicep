// ──────────────────────────────────────────────────────────────
// Module: Azure App Service (API)
// ──────────────────────────────────────────────────────────────
// ASP.NET Core 10 REST API with system-assigned managed identity,
// health check, CORS, and Application Insights integration.
// Managed Identity is used for Key Vault, Cosmos DB, and
// Blob Storage access — no secrets in app settings.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('App Service name (e.g. app-rvs-api-dev-wus3-s01-001).')
param name string

@description('Azure region for the App Service.')
param location string

@description('Resource ID of the App Service Plan.')
param serverFarmResourceId string

@description('Tags to apply to the resource.')
param tags object = {}

@description('Application Insights connection string for telemetry.')
param appInsightsConnectionString string = ''

@description('Enable Always On. Set to true for staging/prod, false for dev (B1 tier).')
param alwaysOn bool = false

@description('Health check path for the API.')
param healthCheckPath string = '/health/live'

@description('Allowed CORS origins for Blazor WASM frontends.')
param corsAllowedOrigins string[] = []

@description('Cosmos DB account endpoint URL.')
param cosmosDbEndpoint string = ''

@description('Cosmos DB database name.')
param cosmosDbDatabaseName string = 'rvsdb'

@description('Blob Storage account name.')
param storageAccountName string = ''

@description('Blob Storage container name for attachments.')
param storageContainerName string = 'rvs-attachments'

@description('Key Vault URI for secret references.')
param keyVaultUri string = ''

@description('The .NET runtime stack version.')
param linuxFxVersion string = 'DOTNETCORE|10.0'

// ── Resources ─────────────────────────────────────────────────

resource appService 'Microsoft.Web/sites@2024-11-01' = {
  name: name
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: serverFarmResourceId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      alwaysOn: alwaysOn
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: healthCheckPath
      cors: {
        allowedOrigins: corsAllowedOrigins
        supportCredentials: false
      }
      appSettings: concat(
        [
          {
            name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
            value: appInsightsConnectionString
          }
          {
            name: 'CosmosDb__AccountEndpoint'
            value: cosmosDbEndpoint
          }
          {
            name: 'CosmosDb__DatabaseName'
            value: cosmosDbDatabaseName
          }
          {
            name: 'BlobStorage__AccountName'
            value: storageAccountName
          }
          {
            name: 'BlobStorage__ContainerName'
            value: storageContainerName
          }
        ],
        !empty(keyVaultUri)
          ? [
              {
                name: 'KeyVault__Uri'
                value: keyVaultUri
              }
            ]
          : []
      )
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The resource ID of the App Service.')
output resourceId string = appService.id

@description('The name of the App Service.')
output name string = appService.name

@description('The default hostname of the App Service (e.g. app-rvs-api-dev.azurewebsites.net).')
output defaultHostname string = appService.properties.defaultHostName

@description('The principal ID of the system-assigned managed identity.')
output systemAssignedMIPrincipalId string = appService.identity.principalId
