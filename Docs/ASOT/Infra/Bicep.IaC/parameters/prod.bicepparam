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
// Redeploys never touch the binding. The token itself is then copied
// into intakeApexValidationToken below (#652), because it shares the
// apex TXT record-set with SPF and each deploy re-writes that set.
// ──────────────────────────────────────────────────────────────

param environmentName = 'prod'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-prod-westus3'
param whisperResourceGroupName = 'rg-rvs-prod-ncus'
param openAiCapacity = 30
param whisperCapacity = 2

// Preliminary-assessment-only model, independent of textDeploymentName (#584).
// Blank assessmentModelName and redeploy to revert to gpt-4o with zero
// application-code changes. DataZoneStandard (US) SKU — gpt-5 isn't offered
// under the regional Standard SKU textDeploymentName uses.
param assessmentModelName = 'gpt-5'
// Capacity 10 (10K TPM). Each call reserves ~3K tokens against TPM up front
// (~1K prompt + max_completion_tokens 2000, reasoning included), so at 2 every
// call was refused. Check DataZoneStandard gpt-5 quota in westus3 first.
param assessmentDeploymentCapacity = 10

// App Service (API) — Basic B1 for the pilot: no Always On, no deployment slot.
// Upgrade path: 'S1' adds Always On and a staging slot (README "SKU Upgrade Paths").
param deployAppService = true
param appServiceSkuName = 'B1'

// Cosmos DB — Serverless, upgrade to Provisioned via: cosmosCapacityMode = 'Provisioned'
param deployCosmosDb = true
param cosmosCapacityMode = 'Serverless'

// Storage (rvs-attachments container + CORS for SAS uploads)
// Custom domains only — default SWA hostnames intentionally excluded.
param deployStorageAccount = true
param storageAllowSharedKeyAccess = false
param storageCorsOrigins = [
  'https://rvintake.com'
  'https://manager.rvintake.com'
]
// devBlobAccessPrincipalId intentionally unset for prod — the app uses its managed
// identity; humans get blob data access just-in-time (PIM) or via break-glass, never standing.

// Key Vault (RBAC model, API managed identity get + list)
param deployKeyVault = true

// Observability (Log Analytics + Application Insights + /health availability test)
param deployObservability = true
// Off until go-live: billed per run. Turning it on also creates the two alerts
// that read it (#602) — test failing, and telemetry gone dark (pings pass, App
// Insights records nothing). Flip at go-live — Docs/RVS_GoLive_Activities.md (G-3).
// Scale frequency/locations with traffic: README "Turning the availability test on".
param deployAvailabilityTest = false
param availabilityTestFrequencySeconds = 900
param availabilityTestLocations = [
  'us-ca-sjc-azr'
]
// 30 is the lowest the workspace accepts; the first 31 days cost nothing extra.
param logAnalyticsRetentionInDays = 30
// Pre-go-live cap. 0.08 GB/day per environment keeps staging + prod together
// inside the 5 GB/month Log Analytics free grant (per billing account). Past the
// cap, ingestion stops until the daily reset and the #494 alerts go blind — raise
// it (or set '-1') at go-live: Docs/RVS_GoLive_Activities.md (G-2).
param logAnalyticsDailyCapGb = '0.08'

// Ops alert receivers for the packet-pipeline critical alerts (#494). Committed
// here rather than left empty — the Action Groups resource provider does a
// full-replace PUT, so an empty array here deletes any receiver added by hand
// in the portal on every deploy (#639); the portal is not a safe place to set
// this. Real ops alias, replacing the personal address that was a stopgap for
// #639 (#648).
param opsAlertEmailReceivers = [
  {
    name: 'oncall'
    email: 'ops@arnolddigitalsolutions.com'
  }
]

// Communication Services (Email + SMS)
param deployAcs = true

// Custom sending subdomain for the packet email (#532). The Azure-managed
// *.azurecomm.net domain is capped at 10 emails/hour with no support path to
// raise it, and carries no sender reputation — a spam quarantine at a pilot
// shop is unrecoverable. Bicep provisions the CustomerManaged ACS domain and
// writes its SPF (-all) / DKIM / DMARC (p=none) records into the rvintake.com
// zone; the operator then runs `az communication email domain
// initiate-verification` — README "Deploy Production" step 4. The send-quota
// increase is volume-triggered (#603), not part of bring-up.
param acsCustomEmailDomain = 'mail.rvintake.com'
// DMARC aggregate-report destination (#608): a monitored mailbox on the filing
// entity's own domain, the same address the Intake footer shows (SiteIdentity.cs).
//
// It is on a different organizational domain from the DMARC records that name
// it, so RFC 7489 §7.1 applies. Receivers look up
// <policy-domain>._report._dmarc.arnolddigitalsolutions.com for TXT "v=DMARC1"
// and drop the report if it is missing. That zone is at the registrar, not in
// Azure, so Bicep cannot write those records. The deploy prints the exact names
// in the dmarcReportAuthorizationAction output, and README "DMARC aggregate
// reports" has the steps and the dig checks.
//
// History: dmarc-reports@rvserviceflow.com, whose only MX was a third party's
// placeholder, until 2026-09-17. Then dmarc-reports@rvintake.com, which bounced
// because rvintake.com has no MX. It now has a null MX (prod apex, #608).
param dmarcReportingAddress = 'support@arnolddigitalsolutions.com'
// Verified and linked. Must stay true: false unlinks the domain on redeploy.
param acsCustomDomainVerified = true
// SMS (#661). Prod's ACS resource owns toll-free +18332398230. Its toll-free
// verification is #659; flip acsSmsEnabled to true and redeploy only once the
// portal shows it verified.
param acsSmsFromPhoneNumber = '+18332398230'
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
param eventGridWebhookKey = az.getSecret('4d1d5e99-e872-496d-98be-e8a0a4232aec', 'rg-rvs-prod-westus3', 'kv-rvs-prod-wus3', 'EventGrid--Inbound--Key')

// Static Web Apps — Free until go-live, to cut cost while prod carries no
// traffic. Set back to 'Standard' at go-live for the SLA:
// Docs/RVS_GoLive_Activities.md (G-1).
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-prod-westus2'
param swaSkuName = 'Free'

// DNS — Manager: CNAME manager.rvintake.com, bound by Bicep (#632).
//       Intake:  ALIAS A record at the rvintake.com apex → Intake SWA, written by
//                Bicep; the apex *binding* is the one-time out-of-band step above.
//       rvserviceflow.com is kept as the corporate zone — no customer-facing host,
//                but it holds the API origin (#633).
param deployDns = true

// The apex TXT record-set holds this token (from the one-time apex registration)
// plus the apex SPF "v=spf1 -all" (#652). A deploy replaces that whole set, so
// the token has to be written here or the deploy deletes it. It is public, not a
// secret: `dig +short TXT rvintake.com` shows it. Check it against the zone,
// not the SWA: `hostname show --query validationToken` reads blank once the
// apex is Ready.
//   az network dns record-set txt show -g rg-rvs-prod-westus3 -z rvintake.com \
//     -n @ --query "TXTRecords[].value" -o json
// Every value there other than "v=spf1 -all" must be carried here, or the deploy
// deletes it. A what-if that shows only "+ v=spf1 -all" on TXT/@ confirms it.
// If the apex is ever re-registered and Azure mints a new token, update this
// before the next deploy. Blank it and the deploy stops managing the apex TXT
// set: no SPF, and nothing is deleted.
param intakeApexValidationToken = '_gd6d39rcb0u1byedjysur407jqdp2cy'

// Grant DNS Zone Contributor (zone-scoped, NOT RG-wide) to the staging
// GitHub Actions service principal so its `staging.bicepparam` deploys can
// upsert CNAMEs in the prod-owned zones. Object IDs only — get with:
//   az ad sp show --id <appId> --query id -o tsv
param dnsZoneContributorPrincipalIds = [
  // Display Name:  github-actions-rvs-deployment
  // APP_ID: de5714f8-924a-4761-b278-9e7a94f2d116
  '9b1e460a-6bed-418a-9d65-1bbed1f768a9'
]
