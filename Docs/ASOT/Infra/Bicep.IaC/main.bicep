// ──────────────────────────────────────────────────────────────
// Main Orchestration – RVS Azure OpenAI Infrastructure
// ──────────────────────────────────────────────────────────────
// Deploys at subscription scope to manage two resource groups:
//   • rg-rvs-{env}-westus3   — GPT-4o OpenAI + Key Vault (existing)
//   • rg-rvs-{env}-ncus      — Whisper OpenAI (northcentralus)
// ──────────────────────────────────────────────────────────────
targetScope = 'subscription'

// ── Parameters ────────────────────────────────────────────────

@description('The Azure region for the primary OpenAI resources (GPT-4o).')
param location string = 'westus3'

@description('The Azure region for Whisper STT. Whisper 001 Standard is not available in all regions.')
param whisperLocation string = 'northcentralus'

@description('The target environment (dev, staging, or prod).')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string = 'dev'

@description('Name of the primary resource group (GPT-4o + Key Vault).')
param primaryResourceGroupName string = 'rg-rvs-${environmentName}-westus3'

@description('Name of the Whisper resource group (northcentralus).')
param whisperResourceGroupName string = 'rg-rvs-${environmentName}-ncus'

@description('GPT-4o deployment capacity in thousands of tokens per minute (K TPM). Dev = 1, Staging = 10, Prod = 30+.')
@minValue(1)
param openAiCapacity int = 1

@description('Whisper deployment capacity in thousands of tokens per minute (K TPM). Dev = 1.')
@minValue(1)
param whisperCapacity int = 1

@description('Optional. Name of an existing Key Vault in the primary resource group to store OpenAI secrets. Leave empty to skip secret creation.')
param keyVaultName string = ''

@description('Optional. Name of the model deployment used for text workloads (issue text refinement, category suggestion). Defaults to the standard gpt-4o deployment name when both workloads share the same deployment.')
param textDeploymentName string = 'gpt-4o'

// ── Resource Groups ───────────────────────────────────────────

resource rgPrimary 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: primaryResourceGroupName
  location: location
  tags: {
    Application: 'rvs'
    Environment: environmentName
    ManagedBy: 'Bicep'
  }
}

resource rgWhisper 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: whisperResourceGroupName
  location: whisperLocation
  tags: {
    Application: 'rvs'
    Environment: environmentName
    ManagedBy: 'Bicep'
  }
}

// ── Modules ───────────────────────────────────────────────────

// -- GPT-4o (primary region: westus3) --

module openAiNaming 'modules/naming-tags.bicep' = {
  name: 'deploy-openai-naming-${environmentName}'
  scope: rgPrimary
  params: {
    resourceTypePrefix: 'oai'
    appName: 'rvs'
    workload: 'ai'
    environmentName: environmentName
    location: location
  }
}

module openAi 'modules/openai.bicep' = {
  name: 'deploy-openai-${environmentName}'
  scope: rgPrimary
  params: {
    location: location
    environmentName: environmentName
    tags: openAiNaming.outputs.tags
    deploymentCapacity: openAiCapacity
    resourceName: openAiNaming.outputs.resourceName
  }
}

// -- Whisper STT (dedicated region: northcentralus) --

module whisperNaming 'modules/naming-tags.bicep' = {
  name: 'deploy-whisper-naming-${environmentName}'
  scope: rgWhisper
  params: {
    resourceTypePrefix: 'oai'
    appName: 'rvs'
    workload: 'whisper'
    environmentName: environmentName
    location: whisperLocation
  }
}

module whisper 'modules/openai-whisper.bicep' = {
  name: 'deploy-whisper-${environmentName}'
  scope: rgWhisper
  params: {
    location: whisperLocation
    environmentName: environmentName
    tags: whisperNaming.outputs.tags
    whisperCapacity: whisperCapacity
    resourceName: whisperNaming.outputs.resourceName
  }
}

// -- Key Vault secrets (primary RG where KV lives, cross-RG reference for Whisper) --

module keyVaultSecrets 'modules/openai-keyvault-secrets.bicep' = if (!empty(keyVaultName)) {
  name: 'deploy-openai-kv-secrets-${environmentName}'
  scope: rgPrimary
  params: {
    keyVaultName: keyVaultName
    openAiName: openAi.outputs.name
    openAiDeploymentName: openAi.outputs.deploymentName
    openAiTextDeploymentName: textDeploymentName
    whisperOpenAiName: whisper.outputs.name
    whisperOpenAiResourceGroup: rgWhisper.name
    openAiWhisperDeploymentName: whisper.outputs.whisperDeploymentName
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The primary resource group name (GPT-4o + Key Vault).')
output primaryResourceGroup string = rgPrimary.name

@description('The Whisper resource group name.')
output whisperResourceGroup string = rgWhisper.name

@description('The Azure OpenAI resource endpoint URL (GPT-4o).')
output openAiEndpoint string = openAi.outputs.endpoint

@description('The name of the GPT-4o vision model deployment.')
output openAiDeploymentName string = openAi.outputs.deploymentName

@description('The Whisper Azure OpenAI resource endpoint URL.')
output whisperEndpoint string = whisper.outputs.endpoint

@description('The name of the Whisper speech-to-text model deployment.')
output whisperDeploymentName string = whisper.outputs.whisperDeploymentName
