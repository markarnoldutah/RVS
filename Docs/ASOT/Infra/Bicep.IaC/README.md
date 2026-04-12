# RVS – Azure Infrastructure (Bicep)

Infrastructure as Code for the RVS Azure platform:

- **REST API** — Azure App Service (Linux, .NET 10) with Managed Identity
- **Data Tier** — Azure Cosmos DB for NoSQL (10 containers) + Azure Blob Storage
- **Secrets** — Azure Key Vault with RBAC authorization
- **Observability** — Application Insights + Log Analytics Workspace
- **VIN Extraction** — Azure OpenAI GPT-4o Vision
- **Text Refinement & Category Suggestion** — Azure OpenAI GPT-4o
- **Speech-to-Text** — Azure OpenAI Whisper
- **Email & SMS** — Azure Communication Services (ACS)
- **Frontend Hosting** — Azure Static Web Apps (Blazor WASM Intake + Manager)
- **DNS** — Azure DNS Zone with SWA CNAME records

All workloads are deployed by `main.bicep` at subscription scope.

---

## Prerequisites

| Tool | Minimum Version | Install |
|------|-----------------|---------|
| Azure CLI | 2.61+ | [Install](https://learn.microsoft.com/cli/azure/install-azure-cli) |
| Bicep CLI | 0.28+ | `az bicep upgrade` |
| Azure subscription | — | Required resource providers registered |
| Permissions | Contributor + Cognitive Services Contributor | — |

Register the resource providers if needed:

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
├── rg-scaffold.bicep                       # Pre-provision all 9 resource groups
├── modules/
│   ├── naming-tags.bicep                   # Shared naming & tagging helper
│   ├── log-analytics.bicep                 # Log Analytics Workspace
│   ├── app-insights.bicep                  # Application Insights + availability test
│   ├── key-vault.bicep                     # Azure Key Vault (RBAC mode)
│   ├── cosmos-db.bicep                     # Cosmos DB account + 10 containers
│   ├── storage-account.bicep               # Blob Storage + rvs-attachments container
│   ├── app-service-plan.bicep              # Linux App Service Plan
│   ├── app-service.bicep                   # App Service (API) with Managed Identity
│   ├── rbac-key-vault.bicep                # RBAC: Key Vault Secrets User
│   ├── rbac-cosmos-db.bicep                # RBAC: Cosmos DB Account Reader
│   ├── openai.bicep                        # Azure OpenAI + GPT-4o deployment
│   ├── openai-whisper.bicep                # Azure OpenAI + Whisper STT deployment
│   ├── openai-keyvault-secrets.bicep       # Stores OpenAI secrets in Key Vault
│   ├── communication-services.bicep        # Azure Communication Services (Email + SMS)
│   ├── acs-keyvault-secrets.bicep          # Stores ACS secrets in Key Vault
│   ├── static-web-app.bicep                # Azure Static Web App
│   └── dns.bicep                           # Azure DNS Zone + SWA CNAME records
├── parameters/
│   ├── dev.bicepparam                      # Dev parameter values
│   ├── staging.bicepparam                  # Staging parameter values
│   └── prod.bicepparam                     # Prod parameter values
├── bicepconfig.json                        # Linter and analyzer configuration
└── README.md                               # This file
```

---

## Quick-Start – Deploy Dev (Full Stack)

```bash
# 1. Log in and set subscription
az login
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"

# 2. Deploy all resources (subscription scope)
az deployment sub create \
  --location westus3 \
  --template-file main.bicep \
  --parameters parameters/dev.bicepparam \
  --name "rvs-dev-$(date +%Y%m%d%H%M)"
```

### Deploy Staging

```bash
az deployment sub create \
  --location westus3 \
  --template-file main.bicep \
  --parameters parameters/staging.bicepparam \
  --name "rvs-staging-$(date +%Y%m%d%H%M)"
```

### Deploy Prod

```bash
az deployment sub create \
  --location westus3 \
  --template-file main.bicep \
  --parameters parameters/prod.bicepparam \
  --name "rvs-prod-$(date +%Y%m%d%H%M)"
```

---

## Feature Flags

All major resource groups are controlled by boolean parameters for incremental deployment:

| Parameter | Default | Description |
|---|---|---|
| `deployAppService` | `false` | App Service Plan + App Service (API) |
| `deployCosmosDb` | `false` | Cosmos DB account with 10 containers |
| `deployStorageAccount` | `false` | Blob Storage with rvs-attachments container |
| `deployKeyVault` | `false` | Key Vault with RBAC authorization |
| `deployObservability` | `false` | Log Analytics + Application Insights |
| `deployAcs` | `false` | Azure Communication Services |
| `deploySwa` | `false` | Static Web Apps (Intake + Manager) |
| `deployDns` | `false` | DNS Zone + CNAME records |
| `enableAvailabilityTest` | `false` | App Insights availability test on `/health/live` |

OpenAI (GPT-4o + Whisper) is always deployed as it has no feature flag.

---

## Resource Summary by Environment

| Resource | Dev | Staging | Prod |
|---|---|---|---|
| App Service Plan | B1 (1 vCPU) | P1v3 (2 vCPU) | P2v3 (4 vCPU) |
| App Service | Always On: off | Always On: on | Always On: on |
| Cosmos DB | 1000 RU/s shared | 4000 RU/s shared | 10000 RU/s shared |
| Storage | Standard_LRS | Standard_LRS | Standard_LRS |
| Key Vault | No purge protection | Purge protection | Purge protection |
| SWA | Free tier | Standard tier | Standard tier |
| Log Analytics | 90-day retention, 5 GB/day | 90-day, 10 GB/day | 90-day, 20 GB/day |
| Backup | Continuous 7-day | Continuous 7-day | Continuous 30-day |

---

## Cosmos DB Containers

The `cosmos-db.bicep` module provisions 10 containers in the `rvsdb` database with shared throughput:

| # | Container | Partition Key | Unique Keys | Composite Indexes |
|---|---|---|---|---|
| 1 | `service-requests` | `/tenantId` | — | `(status ASC, createdAtUtc DESC)`, `(locationId ASC, status ASC)` |
| 2 | `customer-profiles` | `/tenantId` | `(/tenantId, /email)` | — |
| 3 | `global-customer-accounts` | `/email` | — | — |
| 4 | `asset-ledger` | `/assetId` | `(/assetId, /serviceRequestId)` | — |
| 5 | `dealerships` | `/tenantId` | — | `(type ASC, name ASC)` |
| 6 | `locations` | `/tenantId` | `(/tenantId, /slug)` | — |
| 7 | `slug-lookups` | `/slug` | — | — |
| 8 | `tenant-configs` | `/tenantId` | — | — |
| 9 | `lookup-sets` | `/category` | — | `(category ASC, name ASC)` |
| 10 | `rv-warranty-rules` | `/manufacturer` | — | — |

---

## RBAC Role Assignments

When both the App Service and target resource are deployed, the following RBAC bindings are created:

| Target Resource | Role | Purpose |
|---|---|---|
| Key Vault | Key Vault Secrets User | API reads secrets via Managed Identity |
| Cosmos DB | Cosmos DB Account Reader | API reads data via Managed Identity |
| Blob Storage | Storage Blob Data Contributor | API reads/writes attachments |
| Blob Storage | Storage Blob Delegator | API generates user-delegation SAS tokens |

---

## Connecting to the RVS API

The deployed resources map to the following **appsettings** keys consumed by the RVS API:

| appsettings Key | Source | Example Value |
|---|---|---|
| `AzureOpenAi:Endpoint` | Deployment output `openAiEndpoint` | `https://oai-rvs-dev.openai.azure.com/` |
| `AzureOpenAi:ApiKey` | Key Vault secret `AzureOpenAi--ApiKey` | — |
| `CosmosDb:AccountEndpoint` | Deployment output `cosmosDbEndpoint` | `https://cosmos-rvs-data-dev.documents.azure.com:443/` |
| `CosmosDb:DatabaseName` | App setting | `rvsdb` |
| `BlobStorage:AccountName` | Deployment output `storageAccountName` | `strvsdevwus3001` |
| `BlobStorage:ContainerName` | App setting | `rvs-attachments` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Deployment output `appInsightsConnectionString` | — |

---

## Architecture Decision Records

| Decision | Rationale |
|---|---|
| **Subscription-scoped deployment** | Single `main.bicep` manages resource groups across regions |
| **Feature flags per resource** | Incremental deployment; enables partial stack for cost savings |
| **Shared database throughput** | Cosmos DB containers share RU/s to reduce dev costs (~$24/month vs $216/month) |
| **RBAC authorization for Key Vault** | Microsoft recommended; access policies are deprecated |
| **System-assigned Managed Identity** | Eliminates secrets for service-to-service auth |
| **Availability test on /health/live** | Proactive monitoring of API health |
| **Separate RBAC modules** | Clarity and auditability over inline role assignments |
| **ACS for Email + SMS** | Unified Azure-native provider; managed identity auth |
