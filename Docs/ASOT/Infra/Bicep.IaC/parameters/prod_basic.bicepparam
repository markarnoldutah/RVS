using '../main.bicep'

// PROD — cost-conscious alternate to prod.bicepparam (App Service B1 instead of S1:
// no Always On, no deployment slot). Keep the two files in step; the only intended
// difference is appServiceSkuName. Does not set dnsZoneContributorPrincipalIds —
// deploy prod.bicepparam at least once so the zone-scoped grants exist.

param environmentName = 'prod'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-prod-westus3'
param whisperResourceGroupName = 'rg-rvs-prod-ncus'
param openAiCapacity = 30
param whisperCapacity = 2

// App Service (API) — Basic B1 (~$12/mo): cost-conscious production, no Always On / slots
param deployAppService = true
param appServiceSkuName = 'B1'

// Cosmos DB — Serverless, upgrade to Provisioned via: cosmosCapacityMode = 'Provisioned'
param deployCosmosDb = true
param cosmosCapacityMode = 'Serverless'

// Storage (rvs-attachments container + CORS for SAS uploads)
param deployStorageAccount = true
param storageAllowSharedKeyAccess = false
// devBlobAccessPrincipalId intentionally unset for prod — the app uses its managed
// identity; humans get blob data access just-in-time (PIM) or via break-glass, never standing.

// Key Vault (RBAC model, API managed identity get + list)
param deployKeyVault = true

// Observability (Log Analytics + Application Insights + /health availability test)
param deployObservability = true
param deployAvailabilityTest = true

// Communication Services (Email + SMS)
param deployAcs = true

// Custom sending subdomain for the packet email (#532) — see prod.bicepparam
// for the rationale and the manual follow-up. Kept in step with prod.bicepparam.
param acsCustomEmailDomain = 'mail.rvintake.com'
param dmarcReportingAddress = 'dmarc-reports@rvserviceflow.com'

// Static Web Apps (Standard tier required for Auth0 custom auth + custom domains)
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-prod-westus2'
param swaSkuName = 'Standard'

// DNS — Manager: CNAME manager.rvserviceflow.com, bound by Bicep.
//       Intake:  ALIAS A record at the rvintake.com apex → Intake SWA. The apex *binding*
//                is a one-time out-of-band step — README.md "Deploy Production", step 2.
param deployDns = true
