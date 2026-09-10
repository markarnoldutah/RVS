using '../main.bicep'

// ──────────────────────────────────────────────────────────────
// PROD — the one and only production parameter file
// ──────────────────────────────────────────────────────────────
// Deployable as committed, first time and every time. There is no
// phase 1 / phase 2 and nothing in here to fill in after the fact.
//
// The single thing Bicep does not do is the one-time registration
// of the rvintake.com apex against the Intake SWA: Azure mints the
// ownership token at registration time, so it cannot be authored
// here. That handshake is a portal (or CLI) step you run once,
// after the first deploy — README.md "Deploy Production", step 2.
// Redeploys never touch it.
// ──────────────────────────────────────────────────────────────

param environmentName = 'prod'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-prod-westus3'
param whisperResourceGroupName = 'rg-rvs-prod-ncus'
param openAiCapacity = 30
param whisperCapacity = 2

// App Service (API) — Standard S1: Always On, deployment slots (staging), autoscale ready.
// Cost-conscious alternative: appServiceSkuName = 'B1' (see prod_basic.bicepparam).
param deployAppService = true
param appServiceSkuName = 'S1'

// Cosmos DB — Serverless, upgrade to Provisioned via: cosmosCapacityMode = 'Provisioned'
param deployCosmosDb = true
param cosmosCapacityMode = 'Serverless'

// Storage (rvs-attachments container + CORS for SAS uploads)
// Custom domains only — default SWA hostnames intentionally excluded.
param deployStorageAccount = true
param storageAllowSharedKeyAccess = false
param storageCorsOrigins = [
  'https://rvintake.com'
  'https://manager.rvserviceflow.com'
]
// devBlobAccessPrincipalId intentionally unset for prod — the app uses its managed
// identity; humans get blob data access just-in-time (PIM) or via break-glass, never standing.

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

// DNS — Manager: CNAME manager.rvserviceflow.com, bound by Bicep.
//       Intake:  ALIAS A record at the rvintake.com apex → Intake SWA, written by
//                Bicep; the apex *binding* is the one-time out-of-band step above.
param deployDns = true

// Grant DNS Zone Contributor (zone-scoped, NOT RG-wide) to the staging
// GitHub Actions service principal so its `staging.bicepparam` deploys can
// upsert CNAMEs in the prod-owned zones. Object IDs only — get with:
//   az ad sp show --id <appId> --query id -o tsv
param dnsZoneContributorPrincipalIds = [
  // Display Name:  github-actions-rvs-deployment
  // APP_ID: de5714f8-924a-4761-b278-9e7a94f2d116
  '9b1e460a-6bed-418a-9d65-1bbed1f768a9'
]
