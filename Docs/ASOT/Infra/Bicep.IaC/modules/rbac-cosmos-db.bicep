// ──────────────────────────────────────────────────────────────
// Module: RBAC Role Assignment — Cosmos DB
// ──────────────────────────────────────────────────────────────
// Assigns a built-in Cosmos DB role to a managed identity.
// Typically used to grant the App Service MI the
// "Cosmos DB Account Reader Role" for reading data.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Name of the existing Cosmos DB account to scope the role assignment to.')
param cosmosDbAccountName string

@description('Principal ID (object ID) of the managed identity to assign the role to.')
param principalId string

@description('Built-in role definition ID. Default is Cosmos DB Account Reader Role (fbdf93bf-df7d-467e-a4d2-9458aa1360c8).')
param roleDefinitionId string = 'fbdf93bf-df7d-467e-a4d2-9458aa1360c8'

// ── Existing Resource References ──────────────────────────────

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: cosmosDbAccountName
}

// ── Role Assignment ───────────────────────────────────────────

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cosmosDbAccount.id, principalId, roleDefinitionId)
  scope: cosmosDbAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
