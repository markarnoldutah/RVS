// ──────────────────────────────────────────────────────────────
// Module: Azure Key Vault (RBAC Access Model)
// ──────────────────────────────────────────────────────────────
// Creates a Key Vault with RBAC authorization (not access
// policies). Grants the API managed identity the Key Vault
// Secrets User role (get + list).
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Azure region for the Key Vault.')
param location string

@description('Key Vault resource name. Must be 3-24 characters, alphanumeric and hyphens only.')
@minLength(3)
@maxLength(24)
param keyVaultName string

@description('Tags to apply to the Key Vault.')
param tags object = {}

@description('Principal ID (object ID) of the API App Service managed identity to grant Key Vault Secrets User role. Leave empty to skip role assignment.')
param apiPrincipalId string = ''

@description('Enable soft delete. Recommended for production.')
param enableSoftDelete bool = true

@description('Soft delete retention in days.')
@minValue(7)
@maxValue(90)
param softDeleteRetentionInDays int = 90

@description('Enable purge protection. Recommended for production; prevents permanent deletion during retention period.')
param enablePurgeProtection bool = true

// ── Variables ─────────────────────────────────────────────────

// Key Vault Secrets User — grants get + list on secrets
// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/security#key-vault-secrets-user
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ── Resources ─────────────────────────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: enableSoftDelete
    softDeleteRetentionInDays: softDeleteRetentionInDays
    enablePurgeProtection: enablePurgeProtection
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Grant API managed identity Key Vault Secrets User (get + list)
resource apiSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(apiPrincipalId)) {
  name: guid(keyVault.id, apiPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the Key Vault.')
output resourceId string = keyVault.id

@description('Name of the Key Vault.')
output name string = keyVault.name

@description('Key Vault URI (e.g. https://kv-rvs-staging.vault.azure.net/).')
output vaultUri string = keyVault.properties.vaultUri
