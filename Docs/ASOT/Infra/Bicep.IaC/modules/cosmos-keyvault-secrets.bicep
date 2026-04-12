// ──────────────────────────────────────────────────────────────
// Module: Store Cosmos DB secrets in Key Vault
// ──────────────────────────────────────────────────────────────
// Stores the Cosmos DB endpoint, primary key, and database name
// in Key Vault so the RVS API can read them via the Azure Key
// Vault configuration provider at startup.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The name of the existing Key Vault where secrets will be stored.')
param keyVaultName string

@description('The name of the Cosmos DB account in the current resource group.')
param cosmosAccountName string

@description('The Cosmos DB database name used by the application.')
param databaseName string = 'rvs-db'

// ── Existing Resource References ──────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: cosmosAccountName
}

// ── Key Vault Secrets ─────────────────────────────────────────

resource cosmosEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'CosmosDb--Endpoint'
  properties: {
    value: cosmosAccount.properties.documentEndpoint
    contentType: 'text/plain'
  }
}

resource cosmosKeySecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'CosmosDb--Key'
  properties: {
    value: cosmosAccount.listKeys().primaryMasterKey
    contentType: 'text/plain'
  }
}

resource cosmosDatabaseIdSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'CosmosDb--DatabaseId'
  properties: {
    value: databaseName
    contentType: 'text/plain'
  }
}
