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

// Preliminary-assessment-only model, independent of textDeploymentName (#584).
// Blank assessmentModelName and redeploy to revert to gpt-4o with zero
// application-code changes. DataZoneStandard (US) SKU — gpt-5 isn't offered
// under the regional Standard SKU textDeploymentName uses.
param assessmentModelName = 'gpt-5'
param assessmentDeploymentCapacity = 2

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

// Custom sending subdomain for the packet email (#532). The Azure-managed
// *.azurecomm.net domain is capped at 10 emails/hour with no support path to
// raise it, and carries no sender reputation — a spam quarantine at a pilot
// shop is unrecoverable. Bicep provisions the CustomerManaged ACS domain and
// writes its SPF (-all) / DKIM / DMARC (p=none) records into the rvintake.com
// zone; the operator then runs `az communication email domain
// initiate-verification` — README "Deploy Production" step 4. The send-quota
// increase is volume-triggered (#603), not part of bring-up.
// dmarcReportingAddress must be a monitored mailbox (or a DMARC-processor
// address) — see #608.
param acsCustomEmailDomain = 'mail.rvintake.com'
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
param acsCustomDomainVerified = true
// SMS (#661). Prod's ACS resource owns no number yet. Buying one and submitting
// its toll-free verification is #659; fill the number in here when it exists,
// and flip acsSmsEnabled only once it is verified.
param acsSmsFromPhoneNumber = ''
param acsSmsEnabled = false

// Static Web Apps (Standard tier required for Auth0 custom auth + custom domains)
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-prod-westus2'
param swaSkuName = 'Standard'

// DNS — Manager: CNAME manager.rvintake.com, bound by Bicep (#632).
//       Intake:  ALIAS A record at the rvintake.com apex → Intake SWA, written by
//                Bicep; the apex *binding* is the one-time out-of-band step above.
//       rvserviceflow.com is kept as the corporate zone — no customer-facing host,
//                but it holds the DMARC rua mailbox and the API origin (#633).
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
