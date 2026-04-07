// ──────────────────────────────────────────────────────────────
// Main Orchestration – RVS Azure OpenAI Infrastructure
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The Azure region for all resources.')
param location string = 'westus3'

@description('The target environment (dev, staging, or prod).')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string = 'dev'

@description('Model deployment capacity in thousands of tokens per minute (K TPM). Dev = 1, Staging = 10, Prod = 30+.')
@minValue(1)
param openAiCapacity int = 1

@description('Whisper deployment capacity in thousands of tokens per minute (K TPM). Dev = 1.')
@minValue(1)
param whisperCapacity int = 1

@description('Optional. Name of an existing Key Vault to store OpenAI secrets. Leave empty to skip secret creation.')
param keyVaultName string = ''

@description('Optional. Name of the model deployment used for text workloads (issue text refinement, category suggestion). Defaults to the standard gpt-4o deployment name when both workloads share the same deployment.')
param textDeploymentName string = 'gpt-4o'

// ── Modules ───────────────────────────────────────────────────

module openAiNaming 'modules/naming-tags.bicep' = {
  name: 'deploy-openai-naming-${environmentName}'
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
  params: {
    location: location
    environmentName: environmentName
    tags: openAiNaming.outputs.tags
    deploymentCapacity: openAiCapacity
    whisperCapacity: whisperCapacity
    resourceName: openAiNaming.outputs.resourceName
  }
}

module keyVaultSecrets 'modules/openai-keyvault-secrets.bicep' = if (!empty(keyVaultName)) {
  name: 'deploy-openai-kv-secrets-${environmentName}'
  params: {
    keyVaultName: keyVaultName
    openAiName: openAi.outputs.name
    openAiDeploymentName: openAi.outputs.deploymentName
    openAiWhisperDeploymentName: openAi.outputs.whisperDeploymentName
    openAiTextDeploymentName: textDeploymentName
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The Azure OpenAI resource endpoint URL.')
output openAiEndpoint string = openAi.outputs.endpoint

@description('The name of the GPT-4o vision model deployment.')
output openAiDeploymentName string = openAi.outputs.deploymentName

@description('The name of the Whisper speech-to-text model deployment.')
output whisperDeploymentName string = openAi.outputs.whisperDeploymentName

@description('The resource group containing all deployed resources.')
output resourceGroupName string = resourceGroup().name
