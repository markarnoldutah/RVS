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
// under the regional Standard SKU textDeploymentName uses. Keep in step with
// prod_basic.bicepparam.
param assessmentModelName = 'gpt-5'
param assessmentDeploymentCapacity = 2

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

// Ops alert receivers for the packet-pipeline critical alerts (#494). Left empty
// here — the ops mailbox is not committed to git, same rule as the Auth0 values.
// Set it on the deploy:
//   --parameters opsAlertEmailReceivers='[{"name":"oncall","email":"ops@yourco.com"}]'
// or add receivers to the ag-rvs-ops-prod-wus3 action group in the portal.
// Until a receiver exists the alert rules fire but notify nobody
// (the opsAlertReceiverAction deployment output repeats this).
param opsAlertEmailReceivers = []

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
param dmarcReportingAddress = 'dmarc-reports@rvserviceflow.com'
// Verified and linked. Must stay true: false unlinks the domain on redeploy.
param acsCustomDomainVerified = true

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
