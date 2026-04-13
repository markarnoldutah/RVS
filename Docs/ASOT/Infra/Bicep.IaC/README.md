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
| OpenAI (GPT-4o) | `openai.bicep` | 10 K TPM | 30 K TPM |
| OpenAI (Whisper) | `openai-whisper.bicep` | 1 K TPM | 1 K TPM |
| Communication Services | `communication-services.bicep` | Email + SMS | Email + SMS |
| Static Web App (Intake) | `static-web-app.bicep` | Standard | Standard |
| Static Web App (Manager) | `static-web-app.bicep` | Standard | Standard |
| DNS Zone + CNAMEs | `dns.bicep` | Shared zone | Shared zone |

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
│   ├── static-web-app.bicep                # Azure Static Web App
│   └── dns.bicep                           # DNS zone + CNAME records
├── parameters/
│   ├── staging.bicepparam                  # Staging parameter values (full)
│   └── prod.bicepparam                     # Production parameter values (full)
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

### Deploy Production (only when ready)

**PowerShell**
```powershell
$ts = Get-Date -Format "yyyyMMddHHmm"
az deployment sub create `
  --location westus3 `
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep `
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam `
  --name "rvs-prod-$ts"
```

**bash / zsh**
```bash
TS=$(date +%Y%m%d%H%M)
az deployment sub create \
  --location westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/prod.bicepparam \
  --name "rvs-prod-${TS}"
```

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
- **`rvs-attachments` container**: `PublicAccess = None`
- **CORS rules**: Configured per environment for browser-based SAS uploads
- **Role assignments**: Storage Blob Data Contributor + Blob Delegator for the API managed identity

---

## Application Insights

The `app-insights.bicep` module creates:

- **Workspace-based Application Insights** linked to Log Analytics
- **Standard availability test** on `/health` (URL ping from 3 US locations, every 5 minutes)

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
