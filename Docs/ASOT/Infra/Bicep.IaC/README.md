# RVS – Azure Infrastructure (Bicep)

Infrastructure as Code for the RVS Azure platform:

- **VIN Extraction from Photo** — powered by Azure OpenAI GPT-4o Vision
- **Issue Text Refinement & Category Suggestion** — powered by Azure OpenAI GPT-4o
- **Speech-to-Text Transcription** — powered by Azure OpenAI Whisper
- **Transactional Email** — powered by Azure Communication Services (ACS) Email
- **SMS Notifications** — powered by Azure Communication Services (ACS) SMS

All workloads are deployed by `main.bicep` at subscription scope.

---

## Prerequisites

| Tool | Minimum Version | Install |
|------|-----------------|---------|
| Azure CLI | 2.61+ | [Install](https://learn.microsoft.com/cli/azure/install-azure-cli) |
| Bicep CLI | 0.28+ | `az bicep upgrade` |
| Azure subscription | — | OpenAI resource provider registered (`Microsoft.CognitiveServices`) |
| Permissions | Contributor + Cognitive Services Contributor on the target resource group | — |

Register the resource providers if needed:

```bash
az provider register --namespace Microsoft.CognitiveServices
az provider register --namespace Microsoft.Communication
```

---

## Repository Layout

```text
Docs/ASOT/Infra/Bicep.IaC/
├── main.bicep                              # Orchestration template (subscription scope)
├── modules/
│   ├── naming-tags.bicep                   # Shared naming & tagging helper
│   ├── openai.bicep                        # Azure OpenAI + GPT-4o deployment
│   ├── openai-whisper.bicep                # Azure OpenAI + Whisper STT deployment
│   ├── openai-keyvault-secrets.bicep       # Stores OpenAI secrets in Key Vault
│   ├── communication-services.bicep        # Azure Communication Services (Email + SMS)
│   ├── acs-keyvault-secrets.bicep          # Stores ACS endpoint/connection string in Key Vault
│   └── storage-account.bicep               # General-purpose storage account
├── parameters/
│   ├── dev.bicepparam                      # Dev parameter values
│   └── prod.bicepparam                     # Prod parameter values
├── scratch.azcli                           # Post-deploy inspection commands
└── README.md                               # This file
```

---

## Quick-Start – Deploy Dev

```bash
# 1. Log in
az login

# 2. Set the target subscription
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"

# 3. Create the resource group (if it doesn't exist)
az group create \
  --name rg-rvs-dev-westus3 \
  --location westus3

# 4. Deploy
az deployment group create \
  --resource-group rg-rvs-dev-westus3 \
  --template-file main.bicep \
  --parameters parameters/dev.bicepparam

# 5. (Optional) Deploy with Key Vault secret injection
az deployment group create \
  --resource-group rg-rvs-dev-westus3 \
  --template-file main.bicep \
  --parameters parameters/dev.bicepparam \
  --parameters keyVaultName='kv-rvs-dev-x7m2'
```

### Deploy Prod

```bash
az group create --name rg-rvs-prod-westus3 --location westus3

az deployment group create \
  --resource-group rg-rvs-prod-westus3 \
  --template-file main.bicep \
  --parameters parameters/prod.bicepparam \
  --parameters keyVaultName='kv-rvs-prod-ab12'
```

---

## Connecting to the RVS API

The deployed resources map to the following **appsettings** keys consumed by the RVS API:

| appsettings Key | Source | Example Value |
|---|---|---|
| `AzureOpenAi:Endpoint` | Deployment output `openAiEndpoint` | `https://oai-rvs-dev.openai.azure.com/` |
| `AzureOpenAi:ApiKey` | Azure Portal → OpenAI resource → Keys | `abc123…` |
| `AzureOpenAi:VisionDeploymentName` | Deployment output `openAiDeploymentName` | `gpt-4o` |
| `AzureOpenAi:WhisperDeploymentName` | Deployment output `whisperDeploymentName` | `whisper` |
| `Ai:MaxImageBytes` | Hard-coded / appsettings | `5242880` (5 MB) |
| `Ai:MaxAudioBytes` | Hard-coded / appsettings | `10485760` (10 MB) |

### Option A – Key Vault Integration (recommended)

When you pass the `keyVaultName` parameter, secrets are stored automatically as:

- `AzureOpenAi--Endpoint`
- `AzureOpenAi--ApiKey`
- `AzureOpenAi--VisionDeploymentName`
- `AzureOpenAi--TextDeploymentName`
- `AzureOpenAi--WhisperDeploymentName`

Configure the RVS API to read from Key Vault using the
[Azure Key Vault configuration provider](https://learn.microsoft.com/aspnet/core/security/key-vault-configuration).

### Option B – Manual Configuration

Retrieve the values after deployment:

```bash
# Endpoint
az deployment group show \
  --resource-group rg-rvs-dev-westus3 \
  --name deploy-openai-dev \
  --query properties.outputs.openAiEndpoint.value -o tsv

# API key
az cognitiveservices account keys list \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus3 \
  --query key1 -o tsv

# Whisper deployment name
az deployment group show \
  --resource-group rg-rvs-dev-westus3 \
  --name deploy-openai-dev \
  --query properties.outputs.whisperDeploymentName.value -o tsv
```

Set them in `appsettings.Development.json` or as environment variables:

```json
{
  "AzureOpenAi": {
    "Endpoint": "https://oai-rvs-dev.openai.azure.com/",
    "ApiKey": "<KEY>",
    "VisionDeploymentName": "gpt-4o",
    "WhisperDeploymentName": "whisper"
  }
}
```

> **Note:** When `Integrations:UseMocks` is `true`, the API automatically falls back to
> `MockSpeechToTextService` so local development is not blocked.

---

## Connecting ACS to the RVS API

When `deployAcs = true`, the deployed ACS resource maps to these **appsettings** keys:

| appsettings Key | Source | Example Value |
|---|---|---|
| `AzureCommunicationServices:Endpoint` | Deployment output `acsEndpoint` | `https://acs-rvs-notify-dev-wus3-s01-001.communication.azure.com` |
| `AzureCommunicationServices:Email:FromAddress` | Azure portal → ACS → Email → Domains | `DoNotReply@<guid>.azurecomm.net` (Azure-managed) |
| `AzureCommunicationServices:Email:SenderDisplayName` | appsettings | `RV Service Flow` |
| `AzureCommunicationServices:Sms:FromPhoneNumber` | Azure portal → ACS → Phone Numbers | `+18005551234` (purchased separately) |

### Authentication

ACS uses **Azure Managed Identity** — no API keys or connection strings needed in production.
The API registers `EmailClient` and `SmsClient` with `DefaultAzureCredential` pointed at the ACS endpoint.

For local development, a connection string is stored in Key Vault as `AzureCommunicationServices--ConnectionString`
when the `keyVaultName` parameter is provided.

### Retrieve ACS Values After Deployment

```bash
# ACS endpoint
az deployment sub show \
  --name deploy-acs-dev \
  --query properties.outputs.acsEndpoint.value -o tsv

# ACS connection string (for local dev)
az communication list-key \
  --name acs-rvs-notify-dev-wus3-s01-001 \
  --resource-group rg-rvs-dev-westus3 \
  --query primaryConnectionString -o tsv

# Azure-managed email domain MailFrom address
az communication email domain show \
  --domain-name AzureManagedDomain \
  --email-service-name acs-rvs-notify-dev-wus3-s01-001-email \
  --resource-group rg-rvs-dev-westus3 \
  --query mailFromSenderDomain -o tsv
```

### Phone Number Provisioning (SMS)

Phone numbers are **not provisioned by Bicep** for the MVP. Purchase a US toll-free number
through the Azure portal:

1. Navigate to your ACS resource → **Phone numbers**
2. Select **Get a number** → **Toll-free** → **United States**
3. Enable **Send SMS** and **Receive SMS** capabilities
4. Note the number and add it to `appsettings` as `AzureCommunicationServices:Sms:FromPhoneNumber`

> **Cost:** Toll-free number is $2.00/month. See the cost section below for full details.

---

## Estimated Monthly Costs

> Costs are **approximate** and depend on actual usage.

### Azure OpenAI Costs

| Environment | GPT-4o Capacity (K TPM) | Whisper Capacity (K TPM) | Est. Base Cost | Notes |
|---|---|---|---|---|
| **Dev** | 1 | 1 | ~$0 pay-as-you-go | Only billed per 1K tokens consumed |
| **Staging** | 10 | 1 | ~$0 pay-as-you-go | Same billing model, higher burst limit |
| **Prod** | 30 | 1 | ~$0 pay-as-you-go | Standard deployment; billed per token |

**GPT-4o pricing** (as of 2024): $2.50 / 1M input tokens, $10.00 / 1M output tokens.
A typical VIN extraction request uses ~1,000 input tokens (image + prompt) and ~50 output tokens.

**Whisper pricing** (as of 2024): $0.36 / audio hour (~$0.006 / audio minute).
A typical issue description recording is 30–60 seconds (~$0.003–$0.006 per transcription).

### Azure Communication Services Costs

#### Resource & Number Costs

| Resource | Monthly Cost | Notes |
|---|---|---|
| ACS resource | **Free** | No base cost for the resource itself |
| ACS Email (Azure-managed domain) | **$0.00025/email** | First 1,000 emails/month are free |
| Toll-free number (1, shared) | **$2.00** | Shared across all tenants for MVP |

#### Per-Message Costs (US Domestic SMS)

| Direction | Cost per Message | Carrier Surcharge |
|---|---|---|
| Outbound SMS (toll-free) | $0.0079 | ~$0.003 (variable) |
| Inbound SMS (toll-free) | $0.0079 | ~$0.003 (variable) |

#### Dev Environment — Minimum Cost Estimate

For development and testing with minimal traffic:

| Line Item | Quantity | Monthly Cost |
|---|---|---|
| ACS resource | 1 | $0.00 |
| ACS Email (Azure-managed domain) | < 100 emails | $0.00 (within free tier) |
| Toll-free number | 1 | $2.00 |
| Outbound SMS (testing) | ~50 messages | $0.54 |
| **Total ACS (dev)** | — | **~$2.54/month** |

> **Least expensive dev option:** Deploy ACS with the Azure-managed email domain (free) and purchase
> one toll-free number ($2/month). Email is free for the first 1,000 messages/month. SMS is
> pay-per-message with no minimum commitment. The ACS resource itself has no base cost.
> **Total minimum dev infrastructure cost: ~$2.00/month** (number only, if no SMS messages are sent).

#### Production Projections by Growth Phase

| Phase | Tenants | SRs/Month | Emails | Outbound SMS | Inbound SMS | Est. Monthly Cost |
|---|---|---|---|---|---|---|
| **MVP** (10 dealers) | 10 | 500 | 1,500 | 2,000 | 500 | ~$29 |
| **Early Growth** (50 dealers) | 50 | 5,000 | 15,000 | 20,000 | 5,000 | ~$278 |
| **Scale** (200 dealers) | 200 | 50,000 | 150,000 | 200,000 | 50,000 | ~$2,754 |

> **Breakdown:** Email cost is negligible (~$0.25 per 1,000 emails). SMS is the primary cost driver
> at $0.0079 + ~$0.003 carrier surcharge per message. See `RVS_SMS_Notification_Architecture.md`
> Section 6 for detailed cost modeling.

### Combined Dev Environment Cost Summary

| Service | Monthly Cost |
|---|---|
| Azure OpenAI (GPT-4o, pay-per-token) | ~$0 (minimal dev usage) |
| Azure OpenAI (Whisper, pay-per-token) | ~$0 (minimal dev usage) |
| Storage Account (Standard_LRS) | ~$0.02 (minimal data) |
| **ACS resource** | **$0.00** |
| **ACS Email** | **$0.00** (free tier) |
| **ACS toll-free number** | **$2.00** |
| **ACS SMS (testing)** | **~$0.54** |
| **Total estimated dev cost** | **~$2.56/month** |

---

## Post-Deployment Verification

### Azure OpenAI

```bash
# 1. Confirm the resource exists
az cognitiveservices account show \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus3 \
  --query '{name:name, endpoint:properties.endpoint, provisioningState:properties.provisioningState}'

# 2. Confirm model deployments (should show gpt-4o AND whisper)
az cognitiveservices account deployment list \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus3 \
  --query '[].{name:name, model:properties.model.name, version:properties.model.version, capacity:sku.capacity}'

# 3. Quick smoke test – GPT-4o (requires jq)
ENDPOINT=$(az cognitiveservices account show \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus3 \
  --query properties.endpoint -o tsv)
API_KEY=$(az cognitiveservices account keys list \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus3 \
  --query key1 -o tsv)

curl -s "${ENDPOINT}openai/deployments/gpt-4o/chat/completions?api-version=2024-02-01" \
  -H "Content-Type: application/json" \
  -H "api-key: ${API_KEY}" \
  -d '{
    "messages": [{"role":"user","content":"Say hello"}],
    "max_tokens": 5
  }' | jq .choices[0].message.content

# 4. Quick smoke test – Whisper (requires a test audio file)
curl -s "${ENDPOINT}openai/deployments/whisper/audio/transcriptions?api-version=2024-02-01" \
  -H "api-key: ${API_KEY}" \
  -F "file=@test-audio.wav" \
  -F "response_format=json" | jq .text
```

### Azure Communication Services

```bash
# 1. Confirm ACS resource exists
az communication show \
  --name acs-rvs-notify-dev-wus3-s01-001 \
  --resource-group rg-rvs-dev-westus3 \
  --query '{name:name, hostName:hostName, provisioningState:provisioningState, dataLocation:dataLocation}'

# 2. Confirm Email Service and domain
az communication email domain list \
  --email-service-name acs-rvs-notify-dev-wus3-s01-001-email \
  --resource-group rg-rvs-dev-westus3 \
  --query '[].{name:name, domainManagement:domainManagement, mailFrom:mailFromSenderDomain}'

# 3. List phone numbers (after purchasing)
az communication phonenumber list \
  --connection-string "$(az communication list-key \
    --name acs-rvs-notify-dev-wus3-s01-001 \
    --resource-group rg-rvs-dev-westus3 \
    --query primaryConnectionString -o tsv)" \
  --query '[].{phoneNumber:phoneNumber, capabilities:capabilities}'
```

---

## Architecture Decision Records

| Decision | Rationale |
|---|---|
| **GPT-4o `2024-11-20`** | Latest GA version with native vision support |
| **Whisper `001`** | Only GA Whisper version on Azure OpenAI; proven accuracy |
| **Whisper replaces Azure Speech Service** | Consolidates all AI under one OpenAI resource — simpler infra, single API key, unified billing. See migration docs for details. |
| **Standard deployment SKU** | Pay-per-token; no reserved capacity cost |
| **`OnceCurrentVersionExpired` version policy** | Prevents unexpected model behaviour changes in production |
| **Whisper `dependsOn` GPT-4o** | Azure OpenAI does not support concurrent model deployments within the same account |
| **Public network disabled (staging/prod)** | Defence-in-depth; access via Private Endpoint or VNet integration |
| **System-assigned managed identity** | Enables RBAC-based access to other Azure resources without secrets |
| **ACS for Email + SMS** | Unified Azure-native provider replaces SendGrid. Single resource, managed identity auth, consolidated billing. See `RVS_SMS_Notification_Architecture.md` for full rationale. |
| **Azure-managed email domain (dev)** | Zero DNS configuration needed for development. Production uses a custom verified domain (`notifications.rvserviceflow.com`). |
| **Shared toll-free number (MVP)** | $2/month for one shared number across all tenants. Per-tenant numbers are a Phase 2 upgrade path. |
| **ACS data location: United States** | Data residency compliance — all customer PII stays in US data centers. |
