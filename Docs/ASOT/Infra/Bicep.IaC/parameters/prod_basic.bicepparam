using '../main.bicep'

param environmentName = 'prod'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-prod-westus3'
param whisperResourceGroupName = 'rg-rvs-prod-ncus'
param openAiCapacity = 30

// App Service (API) — Basic B1 (~$12/mo): cost-conscious production, no Always On / slots
param deployAppService = true
param appServiceSkuName = 'B1'

// Cosmos DB — Serverless, upgrade to Provisioned via: cosmosCapacityMode = 'Provisioned'
param deployCosmosDb = true
param cosmosCapacityMode = 'Serverless'

// Storage (rvs-attachments container + CORS for SAS uploads)
param deployStorageAccount = true
param storageCorsOrigins = [
  'https://rvintake.com'
  'https://manager.rvserviceflow.com'
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
param swaResourceGroupName = 'rg-rvs-prod-westus2'
param swaSkuName = 'Standard'

// DNS — Manager: CNAME manager.rvserviceflow.com. Intake: apex A-record for rvintake.com (separate zone).
// Apex binding is two-phase: first deploy creates the zone; register the SWA custom domain, then set
// intakeApexIpv4Addresses + intakeApexValidationValues (from Azure portal / `az staticwebapp hostname`) and redeploy.
param deployDns = true
