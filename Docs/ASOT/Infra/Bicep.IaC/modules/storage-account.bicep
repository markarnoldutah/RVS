// ──────────────────────────────────────────────────────────────
// Module: Azure Storage Account
// ──────────────────────────────────────────────────────────────
// Creates a general-purpose v2 storage account with the
// standard LRS redundancy tier suitable for non-production
// workloads. All public-blob access is disabled by default.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Azure region in which to create the storage account.')
param location string

@description('Storage account name. Must be 3-24 lowercase alphanumeric characters only and globally unique.')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Storage SKU redundancy tier.')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_ZRS'
  'Standard_RAGRS'
  'Premium_LRS'
])
param sku string = 'Standard_LRS'

@description('Tags to apply to the storage account.')
param tags object = {}

@description('Principal ID (object ID) of the managed identity that needs blob access. Leave empty to skip role assignments.')
param blobAccessPrincipalId string = ''

@description('Principal ID of the staging slot managed identity for blob access. Leave empty to skip role assignments.')
param stagingSlotBlobAccessPrincipalId string = ''

@description('Object ID of an Entra ID group granted blob data access for developer / manual operations (e.g. sg-rvs-dev-blob). Members can read/write blobs and mint user-delegation SAS from a workstation via AzureCliCredential. Set only in non-production parameter files; leave empty to skip.')
param devBlobAccessPrincipalId string = ''

@description('Allowed CORS origins for browser-based SAS uploads (e.g. the Blazor WASM host URL). Pass an empty array to skip CORS configuration.')
param corsAllowedOrigins string[] = []

@description('Allow shared key (storage account key) access. Set to false for production to enforce Entra ID-only authentication.')
param allowSharedKeyAccess bool = true

// ── Variables ──────────────────────────────────────────────────

// Built-in role definition IDs
// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/storage
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageBlobDelegatorRoleId = 'db58b8e5-c6ad-4a2a-8342-4190687cbf4a'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

// ── Resources ─────────────────────────────────────────────────

resource storageAccount 'Microsoft.Storage/storageAccounts@2025-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: sku
  }
  kind: 'StorageV2'
  tags: tags
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: allowSharedKeyAccess
    supportsHttpsTrafficOnly: true
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    encryption: {
      services: {
        blob: {
          enabled: true
          keyType: 'Account'
        }
        file: {
          enabled: true
          keyType: 'Account'
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

// ── Blob Service (CORS for browser-based SAS uploads & downloads) ─

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2025-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    cors: {
      corsRules: !empty(corsAllowedOrigins)
        ? [
            {
              allowedOrigins: corsAllowedOrigins
              allowedMethods: ['GET', 'HEAD', 'PUT']
              allowedHeaders: ['Content-Type', 'x-ms-blob-type', 'Range']
              exposedHeaders: ['ETag', 'Content-Length', 'Content-Range']
              maxAgeInSeconds: 3600
            }
          ]
        : []
    }
  }
}

// ── Blob Containers ────────────────────────────────────────────

@description('Application blob container for service request file attachments. Public access is always disabled.')
resource attachmentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-01-01' = {
  parent: blobService
  name: 'rvs-attachments'
  properties: {
    publicAccess: 'None'
  }
}

// ── Table Service (go.rvintake.com redirect hit log, #599) ─────

// Spec A-13 keeps redirect hits out of Cosmos on purpose: they are a high-volume write path
// read occasionally, most of them never convert, and link-preview fetchers alone produce
// several per link composed. Cosmos would charge request units on every one of those; here the
// cost is storage. No CORS — nothing in a browser ever talks to this table directly.

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2025-01-01' = {
  parent: storageAccount
  name: 'default'
}

@description('Append-only log of go.rvintake.com redirect hits, partitioned by location (Spec A-13, #599). Table names are alphanumeric only — Table Storage forbids the hyphens the Cosmos containers use.')
resource intakeRedirectHitsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2025-01-01' = {
  parent: tableService
  name: 'intakeRedirectHits'
}

// ── Role Assignments ───────────────────────────────────────────

// Storage Blob Data Contributor — read/write blobs, create containers
resource blobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(blobAccessPrincipalId)) {
  name: guid(storageAccount.id, empty(blobAccessPrincipalId) ? 'unset-app' : blobAccessPrincipalId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageBlobDataContributorRoleId
    )
    principalId: blobAccessPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Storage Blob Delegator — required for GetUserDelegationKeyAsync (user delegation SAS)
resource blobDelegatorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(blobAccessPrincipalId)) {
  name: guid(storageAccount.id, empty(blobAccessPrincipalId) ? 'unset-app' : blobAccessPrincipalId, storageBlobDelegatorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDelegatorRoleId)
    principalId: blobAccessPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── Staging Slot Role Assignments ──────────────────────────────

// Storage Blob Data Contributor for staging slot managed identity
resource stagingSlotBlobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(stagingSlotBlobAccessPrincipalId)) {
  name: guid(storageAccount.id, empty(stagingSlotBlobAccessPrincipalId) ? 'unset-staging-slot' : stagingSlotBlobAccessPrincipalId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageBlobDataContributorRoleId
    )
    principalId: stagingSlotBlobAccessPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Storage Blob Delegator for staging slot managed identity
resource stagingSlotBlobDelegatorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(stagingSlotBlobAccessPrincipalId)) {
  name: guid(storageAccount.id, empty(stagingSlotBlobAccessPrincipalId) ? 'unset-staging-slot' : stagingSlotBlobAccessPrincipalId, storageBlobDelegatorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDelegatorRoleId)
    principalId: stagingSlotBlobAccessPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── Table Role Assignments (redirect hit log, #599) ────────────

// Storage Table Data Contributor — read/write entities in the redirect hit log. Scoped to the
// storage account, like the blob roles above; the same managed identity serves both, so a
// separate identity would buy nothing but another thing to rotate.
resource tableDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(blobAccessPrincipalId)) {
  name: guid(storageAccount.id, empty(blobAccessPrincipalId) ? 'unset-app' : blobAccessPrincipalId, storageTableDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageTableDataContributorRoleId
    )
    principalId: blobAccessPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource stagingSlotTableDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(stagingSlotBlobAccessPrincipalId)) {
  name: guid(storageAccount.id, empty(stagingSlotBlobAccessPrincipalId) ? 'unset-staging-slot' : stagingSlotBlobAccessPrincipalId, storageTableDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageTableDataContributorRoleId
    )
    principalId: stagingSlotBlobAccessPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── Developer / Manual Access Role Assignments (Entra group) ───

// The running application uses its managed identity (assignments above). This grants the same
// two roles to an Entra ID group so developers can point a workstation at this account
// (AzureCliCredential) for local runs and manual ops. Add/remove people via group membership —
// no redeploy. Scoped to this storage account only; set only in non-prod parameter files.

// Storage Blob Data Contributor — read/write blobs, create containers
resource devBlobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(devBlobAccessPrincipalId)) {
  name: guid(storageAccount.id, empty(devBlobAccessPrincipalId) ? 'unset-dev' : devBlobAccessPrincipalId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageBlobDataContributorRoleId
    )
    principalId: devBlobAccessPrincipalId
    principalType: 'Group'
  }
}

// Storage Blob Delegator — required for GetUserDelegationKeyAsync (user delegation SAS)
resource devBlobDelegatorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(devBlobAccessPrincipalId)) {
  name: guid(storageAccount.id, empty(devBlobAccessPrincipalId) ? 'unset-dev' : devBlobAccessPrincipalId, storageBlobDelegatorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDelegatorRoleId)
    principalId: devBlobAccessPrincipalId
    principalType: 'Group'
  }
}

// Storage Table Data Contributor — the same developer access for the redirect hit log, so a
// workstation pointed at this account (AzureCliCredential) can read and write it locally.
resource devTableDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(devBlobAccessPrincipalId)) {
  name: guid(storageAccount.id, empty(devBlobAccessPrincipalId) ? 'unset-dev' : devBlobAccessPrincipalId, storageTableDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageTableDataContributorRoleId
    )
    principalId: devBlobAccessPrincipalId
    principalType: 'Group'
  }
}

// ── Outputs ───────────────────────────────────────────────────
output resourceId string = storageAccount.id

@description('The name of the created storage account.')
output name string = storageAccount.name

@description('The primary blob service endpoint URL.')
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob

@description('The primary table service endpoint URL — the go.rvintake.com redirect hit log (Spec A-13, #599).')
output tableEndpoint string = storageAccount.properties.primaryEndpoints.table

// reading
