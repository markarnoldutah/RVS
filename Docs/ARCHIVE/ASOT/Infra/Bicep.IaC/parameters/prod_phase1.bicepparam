using '../main.bicep'

// ──────────────────────────────────────────────────────────────
// PROD — Phase 1 of 2 (initial deploy)
// ──────────────────────────────────────────────────────────────
// Creates all primary resources, both SWAs, and the rvintake.com
// DNS zone (empty — no apex A/TXT yet). The Intake apex custom
// domain is NOT bound to the SWA in this phase.
//
// After this deploy completes:
//   1. In the Azure Portal (or `az staticwebapp hostname add`),
//      register `rvintake.com` against the prod Intake SWA. Azure
//      issues a TXT validation token and lists the regional apex
//      anycast IPv4 addresses.
//   2. Copy those values into prod_phase2.bicepparam.
//   3. Re-run with prod_phase2.bicepparam.
// ──────────────────────────────────────────────────────────────

param environmentName = 'prod'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-prod-westus3'
param whisperResourceGroupName = 'rg-rvs-prod-ncus'
param openAiCapacity = 30

// App Service (API) — Standard S1: Always On, deployment slots (staging), autoscale ready
param deployAppService = true
param appServiceSkuName = 'S1'

// Cosmos DB — Serverless, upgrade to Provisioned via: cosmosCapacityMode = 'Provisioned'
param deployCosmosDb = true
param cosmosCapacityMode = 'Serverless'

// Storage (rvs-attachments container + CORS for SAS uploads)
param deployStorageAccount = true
param storageAllowSharedKeyAccess = false

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

// DNS — Manager: CNAME manager.rvserviceflow.com binds in this phase.
// Intake apex stays UNBOUND in phase 1 (intakeApex* params left empty);
// the rvintake.com zone is created so Azure can issue the TXT token.
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
