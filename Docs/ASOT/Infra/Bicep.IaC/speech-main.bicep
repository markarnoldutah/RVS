// ──────────────────────────────────────────────────────────────
// Main Orchestration – RVS Azure Speech-to-Text Infrastructure
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

@description('Optional. Name of an existing Key Vault to store Speech secrets. Leave empty to skip secret creation.')
param keyVaultName string = ''

// ── Modules ───────────────────────────────────────────────────

module speechNaming 'modules/naming-tags.bicep' = {
  name: 'deploy-speech-naming-${environmentName}'
  params: {
    resourceTypePrefix: 'stt'
    appName: 'rvs'
    workload: 'ai'
    environmentName: environmentName
    location: location
  }
}

module speech 'modules/speech.bicep' = {
  name: 'deploy-speech-${environmentName}'
  params: {
    location: location
    environmentName: environmentName
    tags: speechNaming.outputs.tags
    resourceName: speechNaming.outputs.resourceName
  }
}

module keyVaultSecrets 'modules/speech-keyvault-secrets.bicep' = if (!empty(keyVaultName)) {
  name: 'deploy-speech-kv-secrets-${environmentName}'
  params: {
    keyVaultName: keyVaultName
    speechName: speech.outputs.name
    speechRegion: location
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The name of the Azure Speech resource.')
output speechResourceName string = speech.outputs.name

@description('The Azure region of the Speech resource — set as AzureSpeech:Region in appsettings.')
output speechRegion string = speech.outputs.region

@description('The SKU tier of the deployed Speech resource (F0 = free, S0 = standard).')
output speechSkuName string = speech.outputs.skuName

@description('The resource group containing all deployed resources.')
output resourceGroupName string = resourceGroup().name
