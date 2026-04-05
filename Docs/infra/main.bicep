// ──────────────────────────────────────────────────────────────
// Main Orchestration – RVS Azure OpenAI Infrastructure
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The Azure region for all resources.')
param location string = 'westus2'

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

@description('Optional. Name of an existing Key Vault to store OpenAI secrets. Leave empty to skip secret creation.')
param keyVaultName string = ''

@description('Deployment stamp identifier for scale units.')
param stamp string = 's01'

@description('Resource instance number used for deterministic naming.')
param instance string = '001'

// ── Variables ─────────────────────────────────────────────────

// ── Modules ───────────────────────────────────────────────────

module naming 'modules/naming-tags.bicep' = {
  name: 'build-naming-${environmentName}'
  params: {
    resourceTypePrefix: 'oai'
    appName: 'rvs'
    workload: 'ai'
    environmentName: environmentName
    location: location
    stamp: stamp
    instance: instance
    tenantModel: 'shared'
  }
}

module openAi 'modules/openai.bicep' = {
  name: 'deploy-openai-${environmentName}'
  params: {
    location: location
    environmentName: environmentName
    resourceName: naming.outputs.resourceName
    tags: naming.outputs.tags
    deploymentCapacity: openAiCapacity
  }
}

module keyVaultSecrets 'modules/openai-keyvault-secrets.bicep' = if (!empty(keyVaultName)) {
  name: 'deploy-openai-kv-secrets-${environmentName}'
  params: {
    keyVaultName: keyVaultName
    openAiName: openAi.outputs.name
    openAiDeploymentName: openAi.outputs.deploymentName
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The Azure OpenAI resource endpoint URL.')
output openAiEndpoint string = openAi.outputs.endpoint

@description('The name of the GPT-4o vision model deployment.')
output openAiDeploymentName string = openAi.outputs.deploymentName

@description('The resource group containing all deployed resources.')
output resourceGroupName string = resourceGroup().name
