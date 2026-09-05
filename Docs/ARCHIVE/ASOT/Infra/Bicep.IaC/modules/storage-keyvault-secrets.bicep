// ──────────────────────────────────────────────────────────────
// Module: Store Storage Account secrets in Key Vault
// ──────────────────────────────────────────────────────────────
// Stores the Blob Storage endpoint and the Azure Tables
// connection string in Key Vault for the RVS API configuration
// provider. The API uses Managed Identity for Blob access and
// the connection string for Azure Tables.
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

@description('Azure Tables connection string — used by the API for audit logging and tenant access gate.')
resource tablesConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureTables--ConnectionString'
  properties: {
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
    contentType: 'text/plain'
  }
}
