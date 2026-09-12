using '../main.bicep'

param environmentName = 'staging'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-staging-westus3'
param whisperResourceGroupName = 'rg-rvs-staging-ncus'
param openAiCapacity = 10
param whisperCapacity = 1

// Preliminary-assessment-only model, independent of textDeploymentName (#584).
// Try gpt-5 here first; blank assessmentModelName and redeploy to revert to
// gpt-4o with zero application-code changes. DataZoneStandard (US) SKU — gpt-5
// isn't offered under the regional Standard SKU textDeploymentName uses.
// Capacity 1 (1K TPM) — confirmed against remaining subscription quota for
// gpt-5 in westus3 at the time this was set; raise via quota request first if
// this account's other AI usage grows into it.
param assessmentModelName = 'gpt-5'
param assessmentDeploymentCapacity = 1

// App Service (API) — Basic B1 ($13.14/mo), upgrade path: B1 → S1
param deployAppService = true
param appServiceSkuName = 'B1'

// Cosmos DB — Serverless, upgrade to Provisioned via: cosmosCapacityMode = 'Provisioned'
param deployCosmosDb = true
param cosmosCapacityMode = 'Serverless'

// Storage (rvs-attachments container + CORS for SAS uploads)
// Custom domains only — default SWA hostnames intentionally excluded; use the
// custom domains for browser-based SAS uploads.
param deployStorageAccount = true
param storageAllowSharedKeyAccess = false
param storageCorsOrigins = [
  'https://staging.rvintake.com'
  'https://manager-staging.rvserviceflow.com'
]

// Developer / manual blob access on the staging storage account. Local API runs
// (AzureCliCredential) and ops inspection authenticate as the developer, so their
// Entra identity needs Storage Blob Data Contributor + Storage Blob Delegator here.
// Grant it to a group, manage access via membership.
//
// One-time setup:
//   az ad group create --display-name sg-rvs-dev-blob --mail-nickname sg-rvs-dev-blob
//   az ad group member add --group sg-rvs-dev-blob --member-id <your-user-object-id>
//   az ad group show --group sg-rvs-dev-blob --query id -o tsv   # paste below
//
// Leave as '' to skip the grant (deploy still succeeds).
param devBlobAccessPrincipalId = 'e7c21157-3e7a-4a31-9408-c4ccef22670e' 

// Key Vault (RBAC model, API managed identity get + list)
param deployKeyVault = true

// Observability (Log Analytics + Application Insights + /health availability test)
param deployObservability = true
param deployAvailabilityTest = true

// Ops alert receivers for the packet-pipeline critical alerts (#494). Left empty
// here — the ops mailbox is not committed to git, same rule as the Auth0 values.
// Set it on the deploy:
//   --parameters opsAlertEmailReceivers='[{"name":"oncall","email":"ops@yourco.com"}]'
// or add receivers to the ag-rvs-ops-staging-wus3 action group in the portal.
// Until a receiver exists the alert rules fire but notify nobody
// (the opsAlertReceiverAction deployment output repeats this).
param opsAlertEmailReceivers = []

// Communication Services (Email + SMS)
param deployAcs = true

// Custom sending subdomain for staging (#532) — on staging's own ACS resource,
// never prod's. ACS tracks failures, the suppression list and send quota per
// resource and domain, and staging fails many sends (seeded recipients are
// .example.com), so sharing prod's would spend prod's bounce budget while it
// warms. A sibling of mail.rvintake.com, not a child of it. Replaces the
// Azure-managed *.azurecomm.net domain (10/hour, not raisable, lands in Junk).
// Manual follow-up is verification only — no quota request, no warming:
// README "Communication Services — Email". Keep staging recipients to
// mailboxes we control; that is what keeps rvintake.com's reputation clean.
param acsCustomEmailDomain = 'mail.staging.rvintake.com'
param dmarcReportingAddress = 'dmarc-reports@rvserviceflow.com'

// Static Web Apps (Standard tier required for Auth0 custom auth + custom domains)
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-staging-westus2'
param swaSkuName = 'Standard'

// DNS — Manager: CNAME manager-staging.rvserviceflow.com. Intake: CNAME staging.rvintake.com (separate zone).
param deployDns = true
