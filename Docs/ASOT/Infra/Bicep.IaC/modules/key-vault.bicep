// ──────────────────────────────────────────────────────────────
// Module: Azure Key Vault
// ──────────────────────────────────────────────────────────────
// Centralized secret management for Auth0, OpenAI, ACS, and
// App Insights connection strings. Uses Azure RBAC for data
// plane access (access policies are deprecated).
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Key Vault name (max 24 characters, e.g. kv-rvs-dev-wus3-s01).')
@maxLength(24)
param name string

@description('Azure region for the Key Vault.')
param location string

@description('Tags to apply to the Key Vault.')
param tags object = {}

@description('Enable purge protection. Recommended for staging/prod to prevent permanent deletion during soft-delete period.')
param enablePurgeProtection bool = false

@description('Soft delete retention period in days.')
@minValue(7)
@maxValue(90)
param softDeleteRetentionInDays int = 90

@description('The tenant ID for the Key Vault. Defaults to the current subscription tenant.')
param tenantId string = subscription().tenantId

// ── Resources ─────────────────────────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: softDeleteRetentionInDays
    enablePurgeProtection: enablePurgeProtection ? true : null
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The resource ID of the Key Vault.')
output resourceId string = keyVault.id

@description('The name of the Key Vault.')
output name string = keyVault.name

@description('The Key Vault URI (e.g. https://kv-rvs-dev-wus3-s01.vault.azure.net/).')
output uri string = keyVault.properties.vaultUri
