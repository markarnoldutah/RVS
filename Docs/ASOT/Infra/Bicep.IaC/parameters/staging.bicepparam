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
  'https://manager-staging.rvintake.com'
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

// Ops alert receivers for the packet-pipeline critical alerts (#494). Committed
// here rather than left empty — the Action Groups resource provider does a
// full-replace PUT, so an empty array here deletes any receiver added by hand
// in the portal on every deploy (#639); the portal is not a safe place to set
// this. markarnoldutah@gmail.com is a personal address, tracked as a known gap
// — replace with a real ops alias once one exists (#648).
param opsAlertEmailReceivers = [
  {
    name: 'oncall'
    email: 'markarnoldutah@gmail.com'
  }
]

// Communication Services (Email + SMS)
param deployAcs = true

// Custom sending subdomain for staging (#532) — on staging's own ACS resource,
// never prod's. ACS tracks failures, the suppression list and send quota per
// resource and domain, and staging fails many sends (seeded recipients are
// .example.com), so sharing prod's would spend prod's bounce budget while it
// warms. A sibling of mail.rvintake.com, not a child of it — literally so since
// #634 renamed it from mail.staging.rvintake.com: both are now direct labels
// under rvintake.com, which also makes the derived DNS sub-label a single label
// in every environment, the shape the rest of main.bicep already assumes.
// Replaces the
// Azure-managed *.azurecomm.net domain (10/hour, not raisable, lands in Junk).
// Manual follow-up is verification only — no quota request, no warming:
// README "Communication Services — Email". Keep staging recipients to
// mailboxes we control; that is what keeps rvintake.com's reputation clean.
param acsCustomEmailDomain = 'mail-staging.rvintake.com'
param dmarcReportingAddress = 'dmarc-reports@rvserviceflow.com'
// Verified and linked. Must stay true: false unlinks the domain on redeploy.
//
// The #634 rename ran the full three-phase sequence on 2026-09-17: deploy with
// false (creating the domain and its records under the new `mail-staging` label),
// verify out of band, then this flip to link it. Domain, SPF, DKIM and DKIM2 all
// read Verified before it was flipped — ACS rejects linking an unverified domain,
// so a fresh domain always starts at false. DMARC stays NotStarted by design:
// Bicep authors that record itself rather than taking it from ACS, so it is not
// part of ACS's verification set.
param acsCustomDomainVerified = true

// Static Web Apps (Standard tier required for Auth0 custom auth + custom domains)
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-staging-westus2'
param swaSkuName = 'Standard'

// DNS — Manager: CNAME manager-staging.rvintake.com (#632). Intake: CNAME staging.rvintake.com.
//       Both now in the rvintake.com zone; rvserviceflow.com is corporate-only.
param deployDns = true
