using '../main.bicep'

param environmentName = 'staging'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-staging-westus3'
param whisperResourceGroupName = 'rg-rvs-staging-ncus'
param openAiCapacity = 10

// App Service (API) — Basic B1 ($13.14/mo), upgrade path: B1 → S1
param deployAppService = true
param appServiceSkuName = 'B1'

// Cosmos DB — Serverless, upgrade to Provisioned via: cosmosCapacityMode = 'Provisioned'
param deployCosmosDb = true
param cosmosCapacityMode = 'Serverless'

// Storage (rvs-attachments container + CORS for SAS uploads)
param deployStorageAccount = true
param storageCorsOrigins = [
  'https://zealous-island-0ff7ab71e-staging.westus2.6.azurestaticapps.net' // Intake – staging
  'https://mango-grass-08484a41e.1.azurestaticapps.net'                    // Manager – staging
]

// Key Vault (RBAC model, API managed identity get + list)
param deployKeyVault = true

// Observability (Log Analytics + Application Insights + /health availability test)
param deployObservability = true
param deployAvailabilityTest = true

// Communication Services (Email + SMS)
param deployAcs = true

// Static Web Apps (Standard tier required for Auth0 custom auth + custom domains)
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-staging-westus2'
param swaSkuName = 'Standard'

// DNS (CNAME records: intake-staging.rvserviceflow.com, manager-staging.rvserviceflow.com)
param deployDns = true
