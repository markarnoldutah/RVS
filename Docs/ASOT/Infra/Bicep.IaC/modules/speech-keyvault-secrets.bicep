// ──────────────────────────────────────────────────────────────
// Module: Store Azure Speech secrets in Key Vault
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The name of the existing Key Vault where secrets will be stored.')
param keyVaultName string

@description('The name of the Azure Speech resource to retrieve keys from.')
param speechName string

@description('The resource group containing the Azure Speech resource. Defaults to the current resource group.')
param speechResourceGroup string = resourceGroup().name

@description('The Azure region where the Speech resource is deployed (used as AzureSpeech:Region config value).')
param speechRegion string

// ── Existing Resource References ──────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource speechAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: speechName
  scope: resourceGroup(speechResourceGroup)
}

// ── Key Vault Secrets ─────────────────────────────────────────

// Double-dash separator maps to nested config sections in ASP.NET Core Key Vault provider:
// AzureSpeech--Region  →  AzureSpeech:Region
// AzureSpeech--ApiKey  →  AzureSpeech:ApiKey

resource regionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureSpeech--Region'
  properties: {
    value: speechRegion
    contentType: 'text/plain'
  }
}

resource apiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureSpeech--ApiKey'
  properties: {
    value: speechAccount.listKeys().key1
    contentType: 'text/plain'
  }
}
