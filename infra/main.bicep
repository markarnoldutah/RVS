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

@description('Optional. Name of an existing Key Vault to store OpenAI secrets. Leave empty to skip secret creation.')
param keyVaultName string = ''

// ── Variables ─────────────────────────────────────────────────

var environmentDisplayName = {
  dev: 'Development'
  staging: 'Staging'
  prod: 'Production'
}

var tags = {
  Environment: environmentDisplayName[environmentName]
  Workload: 'RVS'
  CostCenter: 'Engineering'
  Owner: 'platform-team@example.com'
  ManagedBy: 'Bicep'
}

// ── Modules ───────────────────────────────────────────────────

module openAi 'modules/openai.bicep' = {
  name: 'deploy-openai-${environmentName}'
  params: {
    location: location
    environmentName: environmentName
    tags: tags
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
