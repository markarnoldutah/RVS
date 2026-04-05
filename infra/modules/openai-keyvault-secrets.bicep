// ──────────────────────────────────────────────────────────────
// Module: Store Azure OpenAI secrets in Key Vault
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The name of the existing Key Vault where secrets will be stored.')
param keyVaultName string

@description('The name of the Azure OpenAI resource to retrieve connection details from.')
param openAiName string

@description('The resource group containing the Azure OpenAI resource. Defaults to the current resource group.')
param openAiResourceGroup string = resourceGroup().name

@description('The name of the GPT-4o vision model deployment.')
param openAiDeploymentName string = 'gpt-4o'

// ── Existing Resource References ──────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: openAiName
  scope: resourceGroup(openAiResourceGroup)
}

// ── Key Vault Secrets ─────────────────────────────────────────

resource endpointSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureOpenAi--Endpoint'
  properties: {
    value: openAiAccount.properties.endpoint
    contentType: 'text/plain'
  }
}

resource apiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureOpenAi--ApiKey'
  properties: {
    value: openAiAccount.listKeys().key1
    contentType: 'text/plain'
  }
}

resource deploymentNameSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureOpenAi--VisionDeploymentName'
  properties: {
    value: openAiDeploymentName
    contentType: 'text/plain'
  }
}
