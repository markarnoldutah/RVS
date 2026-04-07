# RVS – Azure AI Infrastructure (Bicep)

Infrastructure as Code for the AI-powered features in RVS:

- **VIN Extraction from Photo** — powered by Azure OpenAI GPT-4o Vision
- **Issue Text Refinement & Category Suggestion** — powered by Azure OpenAI GPT-4o
- **Speech-to-Text Transcription** — powered by Azure OpenAI Whisper

All three workloads share a single Azure OpenAI resource deployed by `main.bicep`.

---

## Prerequisites

| Tool | Minimum Version | Install |
|------|-----------------|---------|
| Azure CLI | 2.61+ | [Install](https://learn.microsoft.com/cli/azure/install-azure-cli) |
| Bicep CLI | 0.28+ | `az bicep upgrade` |
| Azure subscription | — | OpenAI resource provider registered (`Microsoft.CognitiveServices`) |
| Permissions | Contributor + Cognitive Services Contributor on the target resource group | — |

Register the resource provider if needed:

```bash
az provider register --namespace Microsoft.CognitiveServices
```

---

## Repository Layout

```text
Docs/ASOT/Infra/Bicep.IaC/
├── main.bicep                              # Orchestration template (OpenAI + Whisper)
├── modules/
│   ├── naming-tags.bicep                   # Shared naming & tagging helper
│   ├── openai.bicep                        # Azure OpenAI + GPT-4o + Whisper deployments
│   └── openai-keyvault-secrets.bicep       # Stores OpenAI secrets in Key Vault
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
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam

# 5. (Optional) Deploy with Key Vault secret injection
az deployment group create \
  --resource-group rg-rvs-dev-westus3 \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --parameters keyVaultName='kv-rvs-dev-x7m2'
```

### Deploy Prod

```bash
az group create --name rg-rvs-prod-westus3 --location westus3

az deployment group create \
  --resource-group rg-rvs-prod-westus3 \
  --template-file infra/main.bicep \
  --parameters infra/parameters/prod.bicepparam \
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

## Estimated Monthly Costs

> Costs are **approximate** and depend on actual token consumption.
> The table below reflects the **base / reserved capacity** cost plus typical usage.

| Environment | GPT-4o Capacity (K TPM) | Whisper Capacity (K TPM) | Est. Base Cost | Notes |
|---|---|---|---|---|
| **Dev** | 1 | 1 | ~$0 pay-as-you-go | Only billed per 1K tokens consumed |
| **Staging** | 10 | 1 | ~$0 pay-as-you-go | Same billing model, higher burst limit |
| **Prod** | 30 | 1 | ~$0 pay-as-you-go | Standard deployment; billed per token |

**GPT-4o pricing** (as of 2024): $2.50 / 1M input tokens, $10.00 / 1M output tokens.
A typical VIN extraction request uses ~1,000 input tokens (image + prompt) and ~50 output tokens.

**Whisper pricing** (as of 2024): $0.36 / audio hour (~$0.006 / audio minute).
A typical issue description recording is 30–60 seconds (~$0.003–$0.006 per transcription).

---

## Post-Deployment Verification

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
