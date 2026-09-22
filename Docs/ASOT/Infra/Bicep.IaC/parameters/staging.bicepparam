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
// Off permanently: billed per run (3 locations × every 5 min ≈ 26K runs/month)
// and no alert rule reads its results, so nobody would learn it had failed.
param deployAvailabilityTest = false
// 30 is the lowest the workspace accepts; the first 31 days cost nothing extra.
param logAnalyticsRetentionInDays = 30
// 0.08 GB/day per environment keeps staging + prod together inside the 5 GB/month
// Log Analytics free grant (per billing account), so a lower cap saves nothing.
// Past the cap, ingestion stops until the daily reset and the #494 alerts go blind.
param logAnalyticsDailyCapGb = '0.08'

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
// DMARC aggregate-report destination. On the SAME organizational domain as the
// DMARC record itself (rvintake.com), which is what keeps it standards-clean:
// RFC 7489 §7.1 requires an authorization record
// (<domain>._report._dmarc.<rua-domain> TXT "v=DMARC1") whenever the rua address
// sits outside the publishing domain's org domain, and none existed while this
// pointed at rvserviceflow.com — so conforming reporters had grounds to drop the
// reports outright. Same org domain, no authorization record needed, ever.
//
// It previously read dmarc-reports@rvserviceflow.com. That domain's only MX is
// mail.yourmailprovider.com, a placeholder registered to Domains By Proxy and
// controlled by a third party, so reports were addressed somewhere nobody here
// owns. See the note in #634 / #608.
//
// ⚠ rvintake.com has NO MX record, so reports BOUNCE at the reporter rather than
// arriving. That is deliberate and strictly better than delivery to a stranger,
// but it means p=none is still doing nothing useful: nobody reads the reports.
// #608 tracks giving this a real destination — a monitored mailbox or a DMARC
// processor address. Until then this is a correctness fix, not a working pipeline.
param dmarcReportingAddress = 'dmarc-reports@rvintake.com'
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
// SMS (#661). Staging's ACS resource owns toll-free +18662319618 (bought
// 2026-04-12). Off until that number clears toll-free verification (#659);
// flip acsSmsEnabled to true and redeploy once the portal shows it verified.
param acsSmsFromPhoneNumber = '+18662319618'
param acsSmsEnabled = false

// Inbound Event Grid webhook (#665, #678). The value lives only in Key Vault as
// EventGrid--Inbound--Key; this line makes ARM read it at deploy time, so it is
// never typed, never on a command line and never in this file. Every deploy
// picks up the current value. If the secret cannot be read (missing, vault not
// enabledForTemplateDeployment, deployer lacks
// Microsoft.KeyVault/vaults/deploy/action) the deploy FAILS before creating or
// removing anything, rather than quietly deploying no subscription.
// The subscription id is the one this environment deploys into; getSecret needs
// it as a literal. Rotation and first bring-up of a brand-new environment: see
// the runbook in RVS_Infrastructure.md.
param eventGridWebhookKey = az.getSecret('4d1d5e99-e872-496d-98be-e8a0a4232aec', 'rg-rvs-staging-westus3', 'kv-rvs-staging-wus3', 'EventGrid--Inbound--Key')

// Static Web Apps — Free tier. Staging stays on Free permanently; it needs no SLA,
// and Free's two custom domains per app cover the one each app binds.
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-staging-westus2'
param swaSkuName = 'Free'

// DNS — Manager: CNAME manager-staging.rvintake.com (#632). Intake: CNAME staging.rvintake.com.
//       Both now in the rvintake.com zone; rvserviceflow.com is corporate-only.
param deployDns = true
