// ──────────────────────────────────────────────────────────────
// Module: Store Storage Account secrets in Key Vault
// ──────────────────────────────────────────────────────────────
// Stores the Blob and Table Storage endpoints in Key Vault for the
// RVS API configuration provider. The API uses Managed Identity for
// both (no keys needed).
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

@description('Table Storage endpoint — the go.rvintake.com redirect hit log (Spec A-13, #599). Same Managed Identity auth as Blob. When this secret is absent the API falls back to a no-op hit log: redirects still work and the channel still reaches the service request, only the conversion denominator is lost.')
resource tableEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'TableStorage--Endpoint'
  properties: {
    value: storageAccount.properties.primaryEndpoints.table
    contentType: 'text/plain'
  }
}
