using '../main.bicep'

// ──────────────────────────────────────────────────────────────
// PROD — Phase 2 of 2 (apex domain binding)
// ──────────────────────────────────────────────────────────────
// Run only AFTER phase 1 has completed and Azure has issued the
// SWA custom-domain validation token + apex anycast IPs for the
// prod Intake SWA. Get them with:
//
//   az staticwebapp hostname show \
//     --name stapp-rvs-intake-prod \
//     --hostname rvintake.com \
//     --resource-group rg-rvs-prod-westus2
//
// Replace the placeholder values below before deploying.
// ──────────────────────────────────────────────────────────────

param environmentName = 'prod'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-prod-westus3'
param whisperResourceGroupName = 'rg-rvs-prod-ncus'
param openAiCapacity = 30

// App Service (API) — Standard S1
param deployAppService = true
param appServiceSkuName = 'S1'

// Cosmos DB — Serverless
param deployCosmosDb = true
param cosmosCapacityMode = 'Serverless'

// Storage
param deployStorageAccount = true
param storageAllowSharedKeyAccess = false

// Key Vault
param deployKeyVault = true

// Observability
param deployObservability = true
param deployAvailabilityTest = true

// ACS
param deployAcs = true

// Static Web Apps
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-prod-westus2'
param swaSkuName = 'Standard'

// DNS — both Manager CNAME and Intake apex (A + TXT) bind in this phase.
param deployDns = true

// REPLACE with the apex anycast IPs Azure assigned the prod Intake SWA.
// Multiple IPs may be returned; include all of them.
param intakeApexIpv4Addresses = [
  '0.0.0.0' // TODO: replace with value from `az staticwebapp hostname show`
]

// REPLACE with the TXT validation token Azure issued for rvintake.com.
// Single-element array is normal (Azure issues one token per binding).
param intakeApexValidationValues = [
  'REPLACE_WITH_AZURE_TXT_TOKEN'
]

// Zone-scoped DNS Zone Contributor for the staging deployer SP (least privilege).
param dnsZoneContributorPrincipalIds = [
  // Display Name:  github-actions-rvs-deployment
  // APP_ID: de5714f8-924a-4761-b278-9e7a94f2d116
  '9b1e460a-6bed-418a-9d65-1bbed1f768a9'
]
