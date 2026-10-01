// ──────────────────────────────────────────────────────────────
// Module: Store Azure OpenAI secrets in Key Vault
// ──────────────────────────────────────────────────────────────
// Deploys into the primary resource group where the Key Vault
// lives. References the Whisper OpenAI account cross-RG.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The name of the existing Key Vault where secrets will be stored.')
param keyVaultName string

@description('The name of the primary Azure OpenAI resource (GPT-4o) in the current resource group.')
param openAiName string

@description('The name of the Whisper Azure OpenAI resource (lives in a separate resource group).')
param whisperOpenAiName string

@description('The resource group containing the Whisper Azure OpenAI resource.')
param whisperOpenAiResourceGroup string

@description('The name of the GPT-4o vision model deployment (used for VIN extraction).')
param openAiDeploymentName string = 'gpt-4o'

@description('The name of the GPT-4o text model deployment (used for issue text refinement and category suggestion). Defaults to the vision deployment name when the same deployment handles both workloads.')
param openAiTextDeploymentName string = openAiDeploymentName

@description('The name of the Whisper model deployment (used for speech-to-text transcription).')
param openAiWhisperDeploymentName string = 'whisper'

// ── Existing Resource References ──────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

// GPT-4o account — same resource group as this module deployment (primary RG)
resource openAiAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: openAiName
}

// Whisper account — different resource group (ncus RG)
resource whisperAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: whisperOpenAiName
  scope: resourceGroup(whisperOpenAiResourceGroup)
}

// ── Key Vault Secrets ─────────────────────────────────────────

// -- Primary OpenAI (GPT-4o, westus3) --

resource endpointSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureOpenAi--Endpoint'
  properties: {
    value: openAiAccount.properties.endpoint
    contentType: 'text/plain'
  }
}

resource apiKeySecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureOpenAi--ApiKey'
  properties: {
    value: openAiAccount.listKeys().key1
    contentType: 'text/plain'
  }
}

resource deploymentNameSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureOpenAi--VisionDeploymentName'
  properties: {
    value: openAiDeploymentName
    contentType: 'text/plain'
  }
}

resource textDeploymentNameSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureOpenAi--TextDeploymentName'
  properties: {
    value: openAiTextDeploymentName
    contentType: 'text/plain'
  }
}

// -- Whisper (dedicated region: northcentralus) --

resource whisperEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureOpenAi--WhisperEndpoint'
  properties: {
    value: whisperAccount.properties.endpoint
    contentType: 'text/plain'
  }
}

resource whisperApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureOpenAi--WhisperApiKey'
  properties: {
    value: whisperAccount.listKeys().key1
    contentType: 'text/plain'
  }
}

resource whisperDeploymentNameSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureOpenAi--WhisperDeploymentName'
  properties: {
    value: openAiWhisperDeploymentName
    contentType: 'text/plain'
  }
}
