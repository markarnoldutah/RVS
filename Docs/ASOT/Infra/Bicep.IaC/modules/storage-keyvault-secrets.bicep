// ──────────────────────────────────────────────────────────────
// Module: Store Storage Account secrets in Key Vault
// ──────────────────────────────────────────────────────────────
// Stores the Blob Storage endpoint in Key Vault for the RVS API
// configuration provider. The API uses Managed Identity for Blob
// access (no key needed).
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The name of the existing Key Vault where secrets will be stored.')
param keyVaultName string

@description('The name of the storage account in the current resource group.')
param storageAccountName string

// ── Existing Resource References ──────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2025-01-01' existing = {
  name: storageAccountName
}

// ── Key Vault Secrets ─────────────────────────────────────────

@description('Blob Storage endpoint — used by the API with Managed Identity auth (no key needed).')
resource blobEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'BlobStorage--Endpoint'
  properties: {
    value: storageAccount.properties.primaryEndpoints.blob
    contentType: 'text/plain'
  }
}
