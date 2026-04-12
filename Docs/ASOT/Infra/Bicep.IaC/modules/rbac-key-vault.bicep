// ──────────────────────────────────────────────────────────────
// Module: RBAC Role Assignment — Key Vault
// ──────────────────────────────────────────────────────────────
// Assigns a built-in Key Vault role to a managed identity.
// Typically used to grant the App Service MI the
// "Key Vault Secrets User" role for reading secrets.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Name of the existing Key Vault to scope the role assignment to.')
param keyVaultName string

@description('Principal ID (object ID) of the managed identity to assign the role to.')
param principalId string

@description('Built-in role definition ID. Default is Key Vault Secrets User (4633458b-17de-408a-b874-0445c86b69e6).')
param roleDefinitionId string = '4633458b-17de-408a-b874-0445c86b69e6'

// ── Existing Resource References ──────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

// ── Role Assignment ───────────────────────────────────────────

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, roleDefinitionId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
