# RVS – Azure Infrastructure (Bicep)

Infrastructure as Code for the RVS Azure platform. Supports **independent deployment** of staging and production environments via separate parameter files.

## Environment Model

| Environment | Purpose | Notes |
|---|---|---|
| **Staging** | Pre-production validation | Full cloud resources |
| **Production** | Live service | Deployed only when explicitly triggered |

---

## Resources Provisioned

| Resource | Module | Staging | Production |
|---|---|---|---|
| App Service (API) | `app-service.bicep` | Free F1 ($0/mo) | Basic B1 (~$12/mo) or Standard S1 (~$58/mo) + staging slot |
| Cosmos DB | `cosmos-db.bicep` | Serverless | Serverless |
| Blob Storage | `storage-account.bicep` | Standard LRS | Standard LRS |
| Key Vault | `key-vault.bicep` | RBAC model | RBAC model |
| Log Analytics | `log-analytics.bicep` | Per env | Per env |
| Application Insights | `app-insights.bicep` | /health test | /health test |
| Monitor alerts + ops action group | `monitor-alerts.bicep` | Packet-pipeline alerts | Packet-pipeline alerts |
| OpenAI (GPT-4o) | `openai.bicep` | 10 K TPM | 30 K TPM |
| OpenAI (Whisper) | `openai-whisper.bicep` | 1 K TPM | 1 K TPM |
| Communication Services | `communication-services.bicep` | Email + SMS | Email + SMS |
| Static Web App (Intake) | `static-web-app.bicep` | Standard | Standard |
| Static Web App (Manager) | `static-web-app.bicep` | Standard | Standard |
| DNS (Manager + Intake zones) | `dns.bicep` (×2) | Shared zones | Shared zones |

---

## Prerequisites

| Tool | Minimum Version | Install |
|------|-----------------|---------|
| Azure CLI | 2.61+ | [Install](https://learn.microsoft.com/cli/azure/install-azure-cli) |
| Bicep CLI | 0.28+ | `az bicep upgrade` |
| Azure subscription | — | Resource providers registered |
| Permissions | Contributor + Cognitive Services Contributor | — |

Register resource providers if needed:

```bash
az provider register --namespace Microsoft.CognitiveServices
az provider register --namespace Microsoft.Communication
az provider register --namespace Microsoft.DocumentDB
az provider register --namespace Microsoft.Web
az provider register --namespace Microsoft.KeyVault
az provider register --namespace Microsoft.OperationalInsights
az provider register --namespace Microsoft.Insights
```

---

## Repository Layout

```text
Docs/ASOT/Infra/Bicep.IaC/
├── main.bicep                              # Orchestration template (subscription scope)
├── rg-scaffold.bicep                       # Pre-provision all resource groups
├── bicepconfig.json                        # Linter rules
├── modules/
│   ├── app-service.bicep                   # App Service Plan + Web App (Managed Identity)
│   ├── app-service-config.bicep            # Post-deploy app settings (App Insights, Key Vault)
│   ├── app-insights.bicep                  # Application Insights + /health availability test
│   ├── monitor-alerts.bicep                # Ops action group + packet-pipeline critical/warn alert rules (#494)
│   ├── cosmos-db.bicep                     # Cosmos DB account + 10 containers with index policies
│   ├── key-vault.bicep                     # Key Vault (RBAC access model)
│   ├── log-analytics.bicep                 # Log Analytics Workspace
│   ├── naming-tags.bicep                   # Shared naming & tagging helper
│   ├── openai.bicep                        # Azure OpenAI + GPT-4o deployment
│   ├── openai-whisper.bicep                # Azure OpenAI + Whisper STT deployment
│   ├── openai-keyvault-secrets.bicep       # Stores OpenAI secrets in Key Vault
│   ├── communication-services.bicep        # Azure Communication Services (Email + SMS)
│   ├── acs-keyvault-secrets.bicep          # Stores ACS secrets in Key Vault
│   ├── storage-account.bicep               # Storage account + rvs-attachments container
│   ├── static-web-app.bicep                # Azure Static Web App (resource only — no bindings)
│   ├── swa-custom-domain.bicep             # SWA custom-domain binding, ordered after its DNS record
│   └── dns.bicep                           # DNS zone + CNAME / A / ALIAS-A / TXT record sets (called once per zone)
├── parameters/
│   ├── staging.bicepparam                  # Staging parameter values (full)
│   └── prod.bicepparam                     # Production — the one prod file, deployable as committed
└── README.md                               # This file
```

---

## Quick-Start Deployment

### Deploy Staging

> **Note (PowerShell on Windows):** Use the PowerShell block below.
> Inline `$(date +%Y%m%d%H%M)` in a double-quoted string is not evaluated by PowerShell;
> the literal `%` format specifiers are passed to Azure and cause an `InvalidDoubleEncodedRequestUri` error.

**PowerShell**
```powershell
az login
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"

$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam `
  --name "rvs-staging-$ts"
```

**bash / zsh**
```bash
az login
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"

TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam \
  --name "rvs-staging-${TS}"
```

### Deploy Production

One parameter file, `prod.bicepparam`, deployable as committed — there is no
phase 1 / phase 2 and nothing to fill in afterwards. Bicep stands up every
resource, binds `manager.rvserviceflow.com`, and writes an **ALIAS** A record
at the `rvintake.com` apex that tracks the Intake SWA (no pinned IP). The one
thing it does not do is *register* the apex with the SWA: Azure mints the
ownership token at registration time, so that is a one-time step you do by
hand after the first deploy (step 2). Redeploys never touch it.

Prerequisites: the six resource groups exist (`rg-scaffold.bicep`). The two
DNS zones and the registrar delegation onto them are **not** prerequisites —
`deployDns = true` in `prod.bicepparam` creates both zones (idempotent, safe
to redeploy), but Azure DNS only becomes authoritative once the registrar's
NS records point at the four nameservers Azure assigned the zone. Until that
delegation happens, the zone exists in Azure but the domain resolves nowhere
(`dig +short rvintake.com` returns nothing) — this was the actual state of
`rvintake.com` as of the G7 pilot-readiness check (`#535`): the domain was
held but had zero DNS recorded, registrar delegation included. Verify before
assuming either zone is live:

```bash
dig NS rvintake.com +short          # expect ns1-XX.azure-dns.com. (x4)
dig NS rvserviceflow.com +short     # same check for the Manager zone
```

If that returns nothing (or your registrar's default parking nameservers),
delegate before continuing:

1. Deploy (or re-run) the template once so the zones exist — the outputs
   `dnsIntakeNameServers` / `dnsManagerNameServers` list the four Azure-
   assigned nameservers for each zone (also visible via
   `az network dns zone show -g rg-rvs-prod-westus3 -n rvintake.com --query nameServers`).
2. At the domain registrar for each domain, replace the NS records with
   those four values.
3. Wait for propagation (minutes to a few hours depending on the registrar
   and the previous NS TTL), then re-run the `dig NS` check above until it
   shows the `azure-dns.com` nameservers.

Only once both zones show Azure's nameservers will the ALIAS/CNAME records
Bicep writes actually resolve for anyone outside Azure.

**Step 0 — pre-flight (read-only).** See the change set before you commit
to it. On a first bring-up expect a wall of `Create`; on a redeploy expect
`NoChange` almost everywhere.

> **Whisper quota — northcentralus is capped at 3 units, subscription-wide.**
> `staging.bicepparam` now takes `whisperCapacity = 1` and `prod.bicepparam`
> takes `2`, so the two environments fit the cap exactly (1 + 2 = 3). This
> leaves no headroom: raising either value, or adding a third environment in
> that region, needs a quota increase first (Portal → Azure OpenAI → Quotas →
> *Whisper* / *North Central US*). If staging is still deployed at its old
> `whisperCapacity = 3`, redeploy staging before the prod pre-flight — the
> what-if fails `InsufficientQuota` until staging releases those 2 units.

```bash
az deployment sub what-if \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam
```

**Step 1 — deploy.** Pass the Auth0 values on the command line (they are
deliberately not in the parameter file — see *Key Vault Configuration*), or
omit them and write the secrets to the vault by hand afterwards.

```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam \
  --parameters auth0Domain='https://<tenant>.us.auth0.com/' \
               auth0Audience='https://api.rvserviceflow.com' \
               auth0ClientId='<client id>' \
               auth0ClientSecret='<client secret>' \
  --name "rvs-prod-${TS}"
```

```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam `
  --parameters auth0Domain='https://<tenant>.us.auth0.com/' auth0Audience='https://api.rvserviceflow.com' auth0ClientId='<client id>' auth0ClientSecret='<client secret>' `
  --name "rvs-prod-$ts"
```

When it finishes: `manager.rvserviceflow.com` is bound and serving;
`rvintake.com` resolves (the ALIAS record is in place) but the SWA does not
yet accept that hostname, so the apex returns an Azure placeholder page —
and `https://` fails with a cert-name mismatch — until step 2. The
deployment's `intakeApexAction` output repeats this reminder (it is a
non-empty string only for prod).

**Step 2 — register the apex (once, ~5 minutes).** Portal path, which does
the whole TXT-token handshake for you:

1. Portal → `stapp-rvs-intake-prod` (in `rg-rvs-prod-westus2`) → *Settings* →
   **Custom domains** → **+ Add** → **Custom Domain on Azure DNS**.
2. Pick the `rvintake.com` zone, leave the hostname as the apex, **Add**.
3. Azure writes the TXT ownership record into the zone, confirms the ALIAS
   record, and validates. Watch *Status* on the row go `Validating` → `Ready`.

If the portal objects that an `@` A record already exists in the zone (it
should accept it — Bicep wrote the same ALIAS the portal would), delete it and
retry; the next Bicep redeploy re-creates it as a no-op:

```bash
az network dns record-set a delete -g rg-rvs-prod-westus3 -z rvintake.com -n @ --yes
```

CLI path, equivalent (note `hostname set` — there is no `hostname add`):

```bash
# 1. Register — --no-wait, because the operation blocks until the TXT validates
az staticwebapp hostname set -n stapp-rvs-intake-prod -g rg-rvs-prod-westus2 \
  --hostname rvintake.com --validation-method dns-txt-token --no-wait

# 2. Read the token Azure minted (this is the ONLY value `hostname show` returns —
#    it does not return IP addresses; none are needed with the ALIAS record)
TOKEN=$(az staticwebapp hostname show -n stapp-rvs-intake-prod -g rg-rvs-prod-westus2 \
  --hostname rvintake.com --query validationToken -o tsv)

# 3. Publish it
az network dns record-set txt add-record -g rg-rvs-prod-westus3 -z rvintake.com \
  -n @ -v "$TOKEN"

# 4. Wait for Ready (re-run until it flips; minutes, not the 72 h the docs quote,
#    because the zone is on Azure DNS)
az staticwebapp hostname show -n stapp-rvs-intake-prod -g rg-rvs-prod-westus2 \
  --hostname rvintake.com --query '{status:status,error:errorMessage}' -o json
```

**Step 3 — verify.**

```bash
dig +short rvintake.com A                       # resolves via the ALIAS
dig +short manager.rvserviceflow.com CNAME      # → <name>.azurestaticapps.net
az staticwebapp hostname list -n stapp-rvs-intake-prod  -g rg-rvs-prod-westus2 -o table
az staticwebapp hostname list -n stapp-rvs-manager-prod -g rg-rvs-prod-westus2 -o table
curl -sSI https://rvintake.com | head -1        # 200 once the app is deployed by CI
curl -sS  https://<api hostname>/health
```

The SWAs serve Azure's placeholder until `deploy-production.yml` pushes the
apps; that workflow needs the two deployment tokens from this deployment's
outputs stored as GitHub secrets — see *SWA deployment tokens* in
`deployment-cmds.azcli` §6.

**Step 4 — verify and warm the custom sending domain (`#532`).** Bicep has
already provisioned the `CustomerManaged` ACS domain `mail.rvintake.com` and
written its SPF / DKIM / DMARC records into the `rvintake.com` zone. What is
left is the data-plane verification, the quota bump, and the warming window —
none expressible in Bicep. The `acsCustomDomainAction` deployment output
repeats this. Commands are in `deployment-cmds.azcli` §4e (2)(c).

1. **Confirm the zone records match ACS.** `az communication email domain show --domain-name mail.rvintake.com --email-service-name <acs>-email -g rg-rvs-prod-westus3 --query properties.verificationRecords` and spot-check each against the `rvintake.com` zone. They should already agree — Bicep wrote them from the same source.
2. **Initiate verification** for each record type (`Domain`, `SPF`, `DKIM`, `DKIM2`):
   ```bash
   for t in Domain SPF DKIM DKIM2; do
     az communication email domain initiate-verification \
       --domain-name mail.rvintake.com \
       --email-service-name <acs>-email -g rg-rvs-prod-westus3 \
       --verification-type $t
   done
   ```
   Then poll until every entry in `properties.verificationStates` is `Verified` (minutes, since the zone is on Azure DNS). Sends From `mail.rvintake.com` fail until then; the Azure-managed domain stays linked as a fallback.
3. **Check DMARC resolves:** `dig +short TXT _dmarc.mail.rvintake.com` → `v=DMARC1; p=none; rua=mailto:dmarc-reports@rvserviceflow.com`. Make sure that mailbox (or a DMARC-processor address) is actually monitored — `p=none` is only useful if someone reads the aggregate reports.
4. **Request the ACS send-quota increase.** Portal → ACS → **Email** → **Domains** → `mail.rvintake.com` → the quota request form (or Help + Support → *Service and Subscription Limits (Quotas)*). Approval takes **up to 72 hours** — longer if filed on a Friday — and requires a sustained bounce rate **under 1 %**. Do not schedule a pilot launch inside that window.
5. **Warm the domain.** Let Jay Lyons's real intake traffic (P1, `#525`) send through `mail.rvintake.com` for **2–3 weeks** before any *other* shop's mailbox receives a packet. Ramp volume gradually; watch the ACS delivery / bounce metrics and the DMARC `rua` reports. Sustained ACS failures above ~1 % risk throttling.
6. **In-room deliverability check (second pilot onward — `FS-7` in `RVS_Plan.md`).** When onboarding a shop after Jay: send a test packet while you are with the service manager, confirm it lands in the inbox and not Junk, and have them mark the sender safe on the spot. Not needed for the first pilot.

**Redeploys** are step 1 again, verbatim. ARM incremental mode leaves the
apex TXT record and binding alone (the template does not declare them), and
re-asserts the ACS domain and its SPF/DKIM/DMARC records as no-ops once
`verificationStates` is `Verified` — redeploys never re-trigger verification.

### Pre-Provision Resource Groups (all environments)

**PowerShell**
```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/rg-scaffold.bicep `
  --name "rvs-rg-scaffold-$ts"
```

**bash / zsh**
```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/rg-scaffold.bicep \
  --name "rvs-rg-scaffold-${TS}"
```

---

## SKU Upgrade Paths

All upgrades are performed by changing a single parameter value and redeploying.

### App Service: B1 → S1

Change `appServiceSkuName` from `'B1'` to `'S1'`:

**PowerShell**
```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam `
  --parameters appServiceSkuName='S1' `
  --name "rvs-staging-sku-upgrade-$ts"
```

**bash / zsh**
```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam \
  --parameters appServiceSkuName='S1' \
  --name "rvs-staging-sku-upgrade-${TS}"
```

**What changes:** Always On enabled, deployment slots available, autoscale possible.

### Cosmos DB: Serverless → Provisioned Autoscale

> **Note:** Serverless → Provisioned requires account recreation. Back up data first.

Change `cosmosCapacityMode` from `'Serverless'` to `'Provisioned'` and optionally set `cosmosAutoscaleMaxThroughput`:

**PowerShell**
```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam `
  --parameters cosmosCapacityMode='Provisioned' cosmosAutoscaleMaxThroughput=4000 `
  --name "rvs-prod-cosmos-upgrade-$ts"
```

**bash / zsh**
```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam \
  --parameters cosmosCapacityMode='Provisioned' cosmosAutoscaleMaxThroughput=4000 \
  --name "rvs-prod-cosmos-upgrade-${TS}"
```

**What changes:** Autoscale throughput (400–4000 RU/s default), provisioned billing.

### SWA: Free → Standard

Change `swaSkuName` from `'Free'` to `'Standard'`:

**PowerShell**
```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam `
  --parameters swaSkuName='Standard' `
  --name "rvs-staging-swa-upgrade-$ts"
```

**bash / zsh**
```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/staging.bicepparam \
  --parameters swaSkuName='Standard' \
  --name "rvs-staging-swa-upgrade-${TS}"
```

---

## Cross-Environment DNS RBAC

Both DNS zones (`rvserviceflow.com`, `rvintake.com`) live in the **prod** RG
(`rg-rvs-prod-westus3`) by design — apex zones are global. The staging
deployment writes CNAME records into those zones (`manager-staging`,
`staging`), so the staging GitHub Actions service principal needs write
access to the zones.

We grant **DNS Zone Contributor** (`befefa01-2a29-4197-83a8-272ff33ce314`)
**at the zone scope only** — never at the prod RG scope.

**One-time setup:**

1. Get the staging deployer SP object id:
   ```bash
   az ad sp show --id <STAGING_APP_REG_APPID> --query id -o tsv
   ```
2. Add it to `dnsZoneContributorPrincipalIds` in `prod.bicepparam`.
3. Re-run the prod deploy. The role assignments are created at zone scope.

The staging principal can now upsert record sets in both zones but holds
no other rights on the prod RG.

---

## Key Vault Configuration

The Key Vault module uses **RBAC authorization** (not access policies). The API managed identity is automatically granted the **Key Vault Secrets User** role (get + list) when both `deployKeyVault` and `deployAppService` are `true`.

### Secrets to store manually after deployment

| Secret Name | Source | Purpose |
|---|---|---|
| `OpenAi--ApiKey` | Azure Portal → OpenAI resource → Keys | Stored automatically when Key Vault is deployed |
| `AzureCommunicationServices--ConnectionString` | ACS resource → Keys | Stored automatically when ACS + Key Vault are deployed |
| `Stripe--WebhookSecret` | Stripe Dashboard → Webhooks | **Manual** — add after Stripe configuration |

### Add Stripe webhook secret

```bash
az keyvault secret set \
  --vault-name kv-rvs-staging-wus3 \
  --name "Stripe--WebhookSecret" \
  --value "<YOUR_STRIPE_WEBHOOK_SECRET>"
```

---

## Cosmos DB Containers

The `cosmos-db.bicep` module creates 10 containers with optimized index policies matching the seed tool:

| # | Container | Partition Key | Unique Keys | Composite Indexes |
|---|---|---|---|---|
| 1 | `service-requests` | `/tenantId` | — | status+createdAtUtc, locationId+status |
| 2 | `customer-profiles` | `/tenantId` | tenantId+email | — |
| 3 | `global-customer-accounts` | `/email` | — | — |
| 4 | `asset-ledger` | `/assetId` | assetId+serviceRequestId | — |
| 5 | `dealerships` | `/tenantId` | — | type+name |
| 6 | `locations` | `/tenantId` | tenantId+slug | — |
| 7 | `slug-lookups` | `/slug` | — | — |
| 8 | `tenant-configs` | `/tenantId` | — | — |
| 9 | `lookup-sets` | `/category` | — | category+name |
| 10 | `rv-warranty-rules` | `/manufacturer` | — | — |

---

## Blob Storage

The `storage-account.bicep` module creates:

- **Storage account**: Standard LRS, TLS 1.2, no public blob access
- **`rvs-attachments` container**: `PublicAccess = None` — holds intake file attachments and, since #434, generated packet PDFs under the `packets/` prefix
- **CORS rules**: Configured per environment for browser-based SAS uploads
- **Role assignments**: Storage Blob Data Contributor + Blob Delegator for the API managed identity (and the staging deployment slot's identity when present)

### Developer / manual blob access (`devBlobAccessPrincipalId`)

The running app authenticates with its managed identity. A **local API run uses `AzureCliCredential`**, i.e. the developer's own Entra identity, so that identity needs the same two data-plane roles on the storage account it talks to (packet generation is the first dev path that exercises Blob — a blank `BlobStorage:Endpoint` also makes `BlobServiceClient` throw at startup).

`devBlobAccessPrincipalId` (main → `storage-account.bicep`) takes the **object ID of an Entra group** (`sg-rvs-dev-blob`); the module grants it Storage Blob Data Contributor + Storage Blob Delegator, scoped to that storage account only, with `principalType: 'Group'`. Add or remove developers via **group membership** — no redeploy.

- **Set it only in non-prod parameter files.** `staging.bicepparam` carries a `TODO` placeholder plus the `az ad group create` / `member add` / `show` commands. Deploy succeeds with it left as `''`.
- **Prod leaves it unset** (explicit comment in the prod param files). Humans get prod blob data access **just-in-time** (PIM-eligible activation) or via break-glass, never standing.
- Granting the role assignment requires the deploying principal to hold *User Access Administrator* / *Owner* on the scope.

---

## Application Insights

The `app-insights.bicep` module creates:

- **Workspace-based Application Insights** linked to Log Analytics
- **Standard availability test** on `/health` (URL ping from 3 US locations, every 5 minutes)

---

## Monitoring & alerts

`monitor-alerts.bicep` (gated by `deployObservability`, deployed in staging and
prod) turns the packet-pipeline `LogCritical` events — which until now only
landed in Application Insights — into Azure Monitor alerts (#494).

**Ops action group** — `ag-rvs-ops-<env>-wus3` (short name `rvs-ops-stg` /
`rvs-ops-prod`). Receivers come from the `opsAlertEmailReceivers` parameter,
left empty in the param files (the ops mailbox is not committed to git, same as
the Auth0 values) — set it on the deploy or add receivers in the portal:

```bash
az deployment sub create ... \
  --parameters opsAlertEmailReceivers='[{"name":"oncall","email":"ops@yourco.com"}]'
```

Until a receiver exists the rules evaluate and fire but notify nobody. The
`opsAlertReceiverAction` deployment output flags this.

**Alert rules** — log-search rules (`Microsoft.Insights/scheduledQueryRules`,
`kind: LogAlert`) scoped to the App Insights component. Each query is
`union traces, exceptions | where tostring(customDimensions.EventId) == "<id>"`
and projects the structured log properties as **split dimensions**, so the
alert payload carries `TenantId` plus the offending `LocationId` /
`ServiceRequestId`. `union traces, exceptions` because 434001 logs with an
exception argument and so lands in `exceptions`, not `traces`.

| Rule | EventId | Tier | Cadence / window | Dimensions |
|---|---|---|---|---|
| `sqr-rvs-packet-recipients-bounced-<env>-wus3` | 439002 `AllRecipientsBounced` | Sev 1 — page | 5 min / 5 min | `LocationId`, `TenantId` |
| `sqr-rvs-packet-email-delivery-exhausted-<env>-wus3` | 438001 `PacketEmailDeliveryExhausted` | Sev 1 — page | 5 min / 5 min | `ServiceRequestId`, `TenantId` |
| `sqr-rvs-packet-generation-exhausted-<env>-wus3` | 434001 `PacketGenerationExhausted` | Sev 1 — page | 5 min / 5 min | `ServiceRequestId`, `TenantId` |
| `sqr-rvs-packet-email-oversized-<env>-wus3` | 521001 `PacketEmailOversized` | Sev 1 — page | 5 min / 5 min | `ServiceRequestId`, `TenantId` |
| `sqr-rvs-packet-recipient-bounced-warn-<env>-wus3` | 439001 `RecipientHardBounced` | Sev 3 — digest | 1 h / 6 h | `LocationId`, `TenantId` |

5 minutes is the practical near-real-time floor for log-search alerts; the four
Sev 1 rules use it. **439001** (one recipient disabled, others still receive
packets) is deliberately lower: it is not a delivery failure, so it does not
page — a Sev 3 rule on a 6-hour window evaluated hourly reads as a digest. Fix
the address before it becomes the last active one (439002).

**521001** (`PacketEmailOversized`) pages from day one. Since #566 validates the
email size budget at startup, it can no longer be reached by a misconfigured
budget — it now fires only when the packet PDF has grown past its ~1.5–3 MB norm
(a renderer regression, or an oversized embedded asset such as a per-location
logo). The email still delivered and any photos that fit were still attached, so
nothing else catches the missing PDF. Treat any occurrence as a bug to chase.
The sibling `LogWarning` for the ordinary photo-heavy case (some attachments
trimmed, no `EventId`) is informational and is not alerted.

### Runbook — on-call

- **439002 `AllRecipientsBounced`** — packets for that location are going
  nowhere. In the manager app (or Cosmos), open the location's packet settings
  and **fix or replace the bounced address in `recipients`, then save**.
  `LocationService.UpdateAsync` reconciles the disabled list — re-enabling a
  bounced address is just "add it back to `recipients` and save". The alert
  auto-resolves once no 439002 recurs within the window. Payload carries
  `LocationId` + `TenantId`.
- **438001 / 434001** — a service request has no packet delivered / generated
  after 3 attempts. Payload carries `ServiceRequestId` + `TenantId`. Check the
  correlated traces/exceptions for the failure, then trigger regeneration
  (`POST /api/.../packet:regenerate`, per `PacketGenerationService`).
- **521001 `PacketEmailOversized`** — the email went out without its PDF.
  Not a delivery incident; it is a rendering-size regression. Pull the packet
  for that `ServiceRequestId` from the manager app, check the PDF size and what
  inflated it (embedded image resampling, a per-location logo, the HTML body),
  and file a bug.
- **439001 `RecipientHardBounced`** (digest) — one address on a location was
  disabled; delivery still works via the others. Replace it at leisure before
  the location hits 439002.

Verify the pipeline end to end (payload really carries the dimensions):

```kusto
union traces, exceptions
| where tostring(customDimensions.EventId) in ("439002","438001","434001","521001","439001")
| project timestamp, customDimensions.EventId, SeverityLevel,
          LocationId = tostring(customDimensions.LocationId),
          ServiceRequestId = tostring(customDimensions.ServiceRequestId),
          TenantId = tostring(customDimensions.TenantId)
| order by timestamp desc
```

---

## Communication Services — Email

`communication-services.bicep` provisions the ACS account, an Email Service, and
an **Azure-managed email domain** (`<guid>.azurecomm.net`). In **staging and prod**
it also provisions a **CustomerManaged sending subdomain** — `mail.staging.rvintake.com`
and `mail.rvintake.com` respectively (`acsCustomEmailDomain`, issue `#532`) — and
links both domains to that environment's account.
`AcsEmailNotificationService` sends the service-department packet email (`#437`)
and authenticates with the API **managed identity** (`DefaultAzureCredential`),
so two things must line up with the deployed resource — both now handled by Bicep:

| Concern | How Bicep handles it |
|---|---|
| **Send permission** | `communication-services.bicep` grants the API managed identity (and the S1 staging-slot identity) **Contributor scoped to the ACS resource** — ACS has no granular data-plane "email sender" role yet. Wired from `main.bicep` (`apiPrincipalId` / `stagingSlotPrincipalId`) exactly like the Key Vault / Storage grants. |
| **Sender address** | `app-service-config.bicep` sets the app setting `AzureCommunicationServices__Email__FromAddress` to `DoNotReply@<sender domain>` — the custom verified subdomain (`mail.rvintake.com`) when `acsCustomEmailDomain` is set, otherwise the Azure-managed domain. `main.bicep` picks between `communicationServices.outputs.customFromSenderDomain` and `.azureManagedMailFrom`. The hardcoded `DoNotReply@<guid>.azurecomm.net` was removed from `RVS.API/appsettings.json` so a stale value cannot shadow it. |

A plain [idempotent redeploy](#quick-start-deployment) applies both. RBAC
propagation can take a few minutes.

**Local development** sends through **staging's** ACS resource, never prod's.
`RVS.API/appsettings.Development.json` sets the staging endpoint and the From
address `DoNotReply@mail.staging.rvintake.com`, and the API authenticates with
`AzureCliCredential` (the same shortcut Blob uses). Your `az login` identity
therefore needs **Contributor** on `acs-rvs-notify-staging-wus3-s01-001`;
subscription Owner already covers it. Local intake runs send real email.

### Custom sending domains — `mail.rvintake.com`, `mail.staging.rvintake.com` (`#532`)

The Azure-managed `*.azurecomm.net` domain caps at **5 emails/min, 10/hour with
no support path to raise it** (`#521`) and carries no sender reputation — a spam
quarantine at a pilot shop is the one failure the "no IT involvement" pitch
cannot survive. Prod therefore sends from a dedicated verified subdomain.

Staging sends from its own verified subdomain on its **own ACS resource**, never
prod's. ACS tracks failures, the suppression list and send quota per resource and
domain, and staging fails many sends (seeded recipients are `.example.com`), so
sharing prod's resource would spend prod's bounce budget while it warms.
`mail.staging` is a sibling of `mail`, not a child of it. Mailbox providers still
weigh any subdomain partly against `rvintake.com`, so the staging subdomain is
kept harmless by behaviour: staging mail that reaches a real inbox goes only to
mailboxes we control.

**What Bicep does** (any env whose params set `acsCustomEmailDomain` — staging and prod):

- Provisions a `CustomerManaged` `Microsoft.Communication/emailServices/domains`
  named after `acsCustomEmailDomain` and adds it to the account's `linkedDomains`.
- Writes the records ACS requires into the `rvintake.com` zone via `dnsIntake`:
  **domain-ownership** TXT, **SPF** TXT (`v=spf1 include:… -all` — ACS fails
  verification on `~all`), and **DKIM** + **DKIM2** CNAMEs. Values come straight
  from `communicationServices.outputs.customDomain{Txt,Cname}Records`, so there
  is nothing to transcribe.
- Publishes **DMARC** at `_dmarc.mail` (prod) / `_dmarc.mail.staging` (staging) — `v=DMARC1; p=none; rua=mailto:<dmarcReportingAddress>`
  — authored in `main.bicep` (not taken from ACS) so the policy stays `p=none`
  and the reporting mailbox is ours. `p=none` makes alignment failures visible
  in the aggregate reports without dropping mail while the domain is cold.

**What stays manual** (surfaced by the `acsCustomDomainAction` output — see
"Deploy Production" step 4): `az communication email domain initiate-verification`
for each record type, the ACS **send-quota increase** request (72 h lead, needs a
bounce rate < 1 %), and **domain warming**. Staging needs only the verification:
its default 30/min, 100/hour quota is enough for testing, and it is never warmed.

### Manual steps after the deploy (not expressible in Bicep)

Commands for each are in `deployment-cmds.azcli` §4e. Summary:

1. **Verify the sending domain.** Staging and prod send from a custom domain: run "Deploy Production" step 4 (1)–(3) against that environment's domain, ACS resource and resource group — sends From it fail until every record shows `Verified`. The Azure-managed fallback domain verifies automatically but can lag (`az communication email domain show` → `provisioningState = Succeeded`); sends from it fail with `DomainNotLinked` until it completes.
2. **Read back the real sender domain** (`properties.fromSenderDomain`) and confirm the deployed `AzureCommunicationServices__Email__FromAddress` app setting is `DoNotReply@<that domain>`.
3. **Confirm the RBAC grant landed** (`az role assignment list --scope <acs-resource-id>`). If not (older Bicep, or propagation), assign **Contributor** on the ACS resource by hand — §4e (1).
4. **Check the ACS email send quota.** A verified custom domain starts at 30/min, 100/hour — enough for staging, so there is no request to file there. Prod raises it against `mail.rvintake.com` — "Deploy Production" step 4. An environment left on the Azure-managed domain is capped at 10/hour, and no request lifts that.
5. **Set a real recipient on a staging Location.** Seed data uses RFC 2606 `.example.com` addresses that hard-bounce. Point at least one location's `packetConfig.recipients` at a mailbox you control — `PUT /api/dealers/{dealerId}/locations/{locationId}` or directly in Cosmos. `packetConfig.enabled` defaults to `true`. Only ever use mailboxes you control in staging: it is the rule that keeps `mail.staging.rvintake.com` from affecting `rvintake.com`'s reputation.
6. **Run the end-to-end check.** Complete a staging intake; App Insights should show `ACS packet email send initiated …` from `AcsEmailNotificationService`. A failure logs `Packet email dispatch failed …` from `PacketGenerationService` and is otherwise swallowed (packet generation still reports `Succeeded`; retry/idempotency is `#438`). Confirm the mail arrives with the PDF + photo attachments, subject `[RVS] {category} — {year} {make} {model} — {customer last name}`.

> **Prod sends from `mail.rvintake.com` and staging from `mail.staging.rvintake.com`,
> not the managed domain** — provisioned by Bicep (`acsCustomEmailDomain`), verified
> by hand, and warmed in prod only. See "Custom sending domains" above and "Deploy
> Production" step 4.

---

## Estimated Monthly Costs

| Environment | Estimated Monthly Cost | Notes |
|---|---|---|
| **Staging** | ~$50–70 | B1 App Service, Serverless Cosmos, Standard SWAs |
| **Production** | ~$60–120 | Same SKUs; higher OpenAI TPM allocation |
| **Total** | ~$110–190 | Two independent cloud environments |

---

## Post-Deployment: Auth0 Configuration

Auth0 is configured outside of Bicep (external SaaS). For each environment:

1. Create a separate Auth0 tenant (e.g. `rvs-staging.us.auth0.com`, `rvs.us.auth0.com`)
2. Configure API audience matching the App Service hostname
3. Create an Organization per tenant
4. Seed test users with appropriate roles

> **Important:** Never share Auth0 tenants across environments.

---

## Architecture Decision Records

| Decision | Rationale |
|---|---|
| **App Service B1** | Cost-optimized for MVP; no Always On accepted. Upgrade path to S1. |
| **Cosmos DB Serverless** | Pay-per-request; no minimum cost. Upgrade when RU cost exceeds ~$100/month. |
| **SWA Standard** | Required for custom auth (Auth0) and custom domains. |
| **Key Vault RBAC** | Modern best practice; no access policies to manage. |
| **Separate parameter files** | Independent staging and prod deployment; prod not deployed until needed. |
| **Feature flags (deploy*)** | Granular control over which resources are provisioned per environment. |
