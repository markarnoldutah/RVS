# RVS – Azure AI Infrastructure (Bicep)

Infrastructure as Code for the AI-powered features in RVS:

- **VIN Extraction from Photo** — powered by Azure OpenAI GPT-4o Vision (`main.bicep`)
- **Speech-to-Text** — powered by Azure Cognitive Services Speech (`speech-main.bicep`)

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
├── main.bicep                              # OpenAI orchestration template
├── speech-main.bicep                       # Speech-to-Text orchestration template
├── modules/
│   ├── naming-tags.bicep                   # Shared naming & tagging helper
│   ├── openai.bicep                        # Azure OpenAI + GPT-4o deployment
│   ├── openai-keyvault-secrets.bicep       # Stores OpenAI secrets in Key Vault
│   ├── speech.bicep                        # Azure Cognitive Services Speech resource
│   └── speech-keyvault-secrets.bicep       # Stores Speech secrets in Key Vault
├── parameters/
│   ├── dev.bicepparam                      # OpenAI dev values
│   ├── prod.bicepparam                     # OpenAI prod values
│   └── speech-dev.bicepparam               # Speech dev values
├── scratch.azcli                           # OpenAI post-deploy inspection commands
├── speech.azcli                            # Speech dev deployment & inspection commands
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
| `Ai:MaxImageBytes` | Hard-coded / appsettings | `5242880` (5 MB) |

### Option A – Key Vault Integration (recommended)

When you pass the `keyVaultName` parameter, secrets are stored automatically as:

- `AzureOpenAi--Endpoint`
- `AzureOpenAi--ApiKey`
- `AzureOpenAi--VisionDeploymentName`

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
```

Set them in `appsettings.Development.json` or as environment variables:

```json
{
  "AzureOpenAi": {
    "Endpoint": "https://oai-rvs-dev.openai.azure.com/",
    "ApiKey": "<KEY>",
    "VisionDeploymentName": "gpt-4o"
  }
}
```

---

## Estimated Monthly Costs

> Costs are **approximate** and depend on actual token consumption.
> The table below reflects the **base / reserved capacity** cost plus typical usage.

| Environment | Capacity (K TPM) | Est. Base Cost | Notes |
|---|---|---|---|
| **Dev** | 1 | ~$0 pay-as-you-go | Only billed per 1K tokens consumed |
| **Staging** | 10 | ~$0 pay-as-you-go | Same billing model, higher burst limit |
| **Prod** | 30 | ~$0 pay-as-you-go | Standard deployment; billed per token |

GPT-4o pricing (as of 2024): **$2.50 / 1M input tokens**, **$10.00 / 1M output tokens**.
A typical VIN extraction request uses ~1,000 input tokens (image + prompt) and ~50 output tokens.

---

## Post-Deployment Verification

```bash
# 1. Confirm the resource exists
az cognitiveservices account show \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus3 \
  --query '{name:name, endpoint:properties.endpoint, provisioningState:properties.provisioningState}'

# 2. Confirm the model deployment
az cognitiveservices account deployment list \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus3 \
  --query '[].{name:name, model:properties.model.name, version:properties.model.version, capacity:sku.capacity}'

# 3. Quick smoke test (requires jq)
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
```

---

## Architecture Decision Records

| Decision | Rationale |
|---|---|
| **GPT-4o `2024-08-06`** | Latest GA version with native vision support |
| **Standard deployment SKU** | Pay-per-token; no reserved capacity cost |
| **`NoAutoUpgrade` version policy** | Prevents unexpected model behaviour changes in production |
| **Public network disabled (staging/prod)** | Defence-in-depth; access via Private Endpoint or VNet integration |
| **System-assigned managed identity** | Enables RBAC-based access to other Azure resources without secrets |

---

## Speech-to-Text Infrastructure

### Quick-Start – Deploy Speech Dev

```bash
# 1. Log in
az login

# 2. Set the target subscription
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"

# 3. Create the resource group (if it doesn't exist)
az group create \
  --name rg-rvs-dev-westus3 \
  --location westus3

# 4. Deploy Speech (free tier – F0)
az deployment group create \
  --name deploy-speech-dev \
  --resource-group rg-rvs-dev-westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/speech-main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/speech-dev.bicepparam

# 5. (Optional) Deploy with Key Vault secret injection
az deployment group create \
  --name deploy-speech-dev \
  --resource-group rg-rvs-dev-westus3 \
  --template-file Docs/ASOT/Infra/Bicep.IaC/speech-main.bicep \
  --parameters Docs/ASOT/Infra/Bicep.IaC/parameters/speech-dev.bicepparam \
  --parameters keyVaultName='kv-rvs-dev-x7m2'
```

See `speech.azcli` for full post-deploy inspection and smoke-test commands.

### Connecting the RVS API to Azure Speech

The deployed resource maps to the following **appsettings** keys:

| appsettings Key | Source | Example Value |
|---|---|---|
| `AzureSpeech:Region` | Deployment output `speechRegion` | `westus3` |
| `AzureSpeech:ApiKey` | `az cognitiveservices account keys list … --query key1` | `abc123…` |

When `keyVaultName` is provided, secrets are stored automatically as:

- `AzureSpeech--Region`
- `AzureSpeech--ApiKey`

Set them in `appsettings.Development.json` or as environment variables:

```json
{
  "AzureSpeech": {
    "Region": "westus3",
    "ApiKey": "<KEY>"
  }
}
```

> **Note:** When both `AzureSpeech:Region` and `AzureSpeech:ApiKey` are absent (or when
> `Integrations:UseMocks` is `true`), the API automatically falls back to `MockSpeechToTextService`
> so local development is not blocked.

### Speech Estimated Monthly Costs

| Environment | SKU | Est. Cost | Notes |
|---|---|---|---|
| **Dev** | F0 (free) | $0 | 5 hours of audio recognition/month included |
| **Staging** | S0 | Pay-per-use | ~$1 per audio hour |
| **Prod** | S0 | Pay-per-use | ~$1 per audio hour |

### Speech Architecture Decision Records

| Decision | Rationale |
|---|---|
| **`SpeechServices` kind** | Targets the regional REST STT endpoint used by `AzureSpeechToTextService` |
| **F0 (free) for dev** | Eliminates cost for development; automatically upgraded to S0 for staging/prod |
| **Regional endpoint (no custom subdomain required)** | App uses `https://{region}.stt.speech.microsoft.com/…` — custom subdomain is provisioned but optional |
| **Public network enabled (dev only)** | Matches OpenAI pattern; staging/prod lock down public access |
| **System-assigned managed identity** | Consistent with OpenAI module; enables future RBAC-based integrations |
