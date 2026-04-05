# RVS – Azure OpenAI Infrastructure (Bicep)

Infrastructure as Code for the **VIN Extraction from Photo** feature powered by Azure OpenAI GPT-4o Vision.

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
infra/
├── main.bicep                          # Orchestration template
├── modules/
│   ├── naming-tags.bicep               # Shared naming + tags helper
│   ├── openai.bicep                    # Azure OpenAI + GPT-4o deployment
│   └── openai-keyvault-secrets.bicep   # Stores OpenAI secrets in Key Vault
├── parameters/
│   ├── dev.bicepparam                  # Dev environment values
│   └── prod.bicepparam                 # Prod environment values
└── README.md                           # This file
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
  --name rg-rvs-dev-westus2 \
  --location westus2

# 4. Deploy
az deployment group create \
  --resource-group rg-rvs-dev-westus2 \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --parameters stamp='s01' instance='001'

# 5. (Optional) Deploy with Key Vault secret injection
az deployment group create \
  --resource-group rg-rvs-dev-westus2 \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --parameters keyVaultName='kv-rvs-dev-x7m2'
```

### Deploy Prod

```bash
az group create --name rg-rvs-prod-westus2 --location westus2

az deployment group create \
  --resource-group rg-rvs-prod-westus2 \
  --template-file infra/main.bicep \
  --parameters infra/parameters/prod.bicepparam \
  --parameters stamp='s01' instance='001' \
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
  --resource-group rg-rvs-dev-westus2 \
  --name deploy-openai-dev \
  --query properties.outputs.openAiEndpoint.value -o tsv

# API key
az cognitiveservices account keys list \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus2 \
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

Azure OpenAI pricing changes over time and may vary by region, model, and billing plan. Verify current GPT-4o pricing on the official Azure pricing page before using these estimates: https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/
A typical VIN extraction request uses ~1,000 input tokens (image + prompt) and ~50 output tokens.

---

## Post-Deployment Verification

```bash
# 1. Confirm the resource exists
az cognitiveservices account show \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus2 \
  --query '{name:name, endpoint:properties.endpoint, provisioningState:properties.provisioningState}'

# 2. Confirm the model deployment
az cognitiveservices account deployment list \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus2 \
  --query '[].{name:name, model:properties.model.name, version:properties.model.version, capacity:sku.capacity}'

# 3. Quick smoke test (requires jq)
ENDPOINT=$(az cognitiveservices account show \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus2 \
  --query properties.endpoint -o tsv)
API_KEY=$(az cognitiveservices account keys list \
  --name oai-rvs-dev \
  --resource-group rg-rvs-dev-westus2 \
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
| **GPT-4o `2024-08-06`** | GA version selected for this deployment at the time of writing; verify the current latest supported version before deployment |
| **Standard deployment SKU** | Pay-per-token; no reserved capacity cost |
| **`NoAutoUpgrade` version policy** | Prevents unexpected model behaviour changes in production |
| **Public network disabled (staging/prod)** | Defence-in-depth; access via Private Endpoint or VNet integration |
| **System-assigned managed identity** | Enables RBAC-based access to other Azure resources without secrets |
