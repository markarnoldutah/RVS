// ──────────────────────────────────────────────────────────────
// Module: Store Azure Communication Services secrets in Key Vault
// ──────────────────────────────────────────────────────────────
// Stores the ACS endpoint and connection string in Key Vault so
// the RVS API can read them via the Azure Key Vault configuration
// provider. Managed identity is the preferred auth method, so only
// the endpoint is strictly required — the connection string is
// stored as a convenience for local development scenarios.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The name of the existing Key Vault where secrets will be stored.')
param keyVaultName string

@description('The name of the ACS resource in the current resource group.')
param acsName string

// ── Existing Resource References ──────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

// 2025-05-01 does not exist; 2025-09-01 fails domain validation at deploy time.
#disable-next-line use-recent-api-versions
resource acsAccount 'Microsoft.Communication/communicationServices@2023-04-01' existing = {
  name: acsName
}

// ── Key Vault Secrets ─────────────────────────────────────────

@description('ACS endpoint URL — used by the API with managed identity authentication.')
resource acsEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureCommunicationServices--Endpoint'
  properties: {
    value: 'https://${acsAccount.properties.hostName}'
    contentType: 'text/plain'
  }
}

@description('ACS connection string — convenience for local development. Production should use managed identity.')
resource acsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'AzureCommunicationServices--ConnectionString'
  properties: {
    value: acsAccount.listKeys().primaryConnectionString
    contentType: 'text/plain'
  }
}
