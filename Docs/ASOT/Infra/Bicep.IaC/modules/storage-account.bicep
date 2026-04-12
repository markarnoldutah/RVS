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

@description('Allowed CORS origins for browser-based SAS uploads (e.g. the Blazor WASM host URL). Pass an empty array to skip CORS configuration.')
param corsAllowedOrigins string[] = []

@description('Allow shared key (storage account key) access. Set to false for production to enforce Entra ID-only authentication.')
param allowSharedKeyAccess bool = true

@description('Name of the blob container for customer attachments (photos, videos).')
param attachmentsContainerName string = 'rvs-attachments'

@description('Enable blob versioning for accidental delete protection.')
param enableBlobVersioning bool = true

@description('Soft delete retention in days for blobs.')
@minValue(1)
@maxValue(365)
param blobSoftDeleteDays int = 7

@description('Soft delete retention in days for containers.')
@minValue(1)
@maxValue(365)
param containerSoftDeleteDays int = 7

// ── Variables ──────────────────────────────────────────────────

// Built-in role definition IDs
// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/storage
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageBlobDelegatorRoleId = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d2'

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

// ── Blob Service (CORS for browser-based SAS uploads) ──────────

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2025-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    cors: {
      corsRules: !empty(corsAllowedOrigins)
        ? [
            {
              allowedOrigins: corsAllowedOrigins
              allowedMethods: ['PUT']
              allowedHeaders: ['Content-Type', 'x-ms-blob-type']
              exposedHeaders: ['ETag']
              maxAgeInSeconds: 3600
            }
          ]
        : []
    }
    isVersioningEnabled: enableBlobVersioning
    deleteRetentionPolicy: {
      enabled: true
      days: blobSoftDeleteDays
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: containerSoftDeleteDays
    }
  }
}

// ── Attachments Container ──────────────────────────────────────

resource attachmentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-01-01' = {
  parent: blobService
  name: attachmentsContainerName
  properties: {
    publicAccess: 'None'
  }
}

// ── Lifecycle Management Policy ────────────────────────────────

resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2025-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'TierAttachmentsByAge'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['${attachmentsContainerName}/']
            }
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 90
                }
                tierToArchive: {
                  daysAfterModificationGreaterThan: 365
                }
                delete: {
                  daysAfterModificationGreaterThan: 2555
                }
              }
            }
          }
        }
      ]
    }
  }
}

// ── Role Assignments ───────────────────────────────────────────

// Storage Blob Data Contributor — read/write blobs, create containers
resource blobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(blobAccessPrincipalId)) {
  name: guid(storageAccount.id, blobAccessPrincipalId, storageBlobDataContributorRoleId)
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
  name: guid(storageAccount.id, blobAccessPrincipalId, storageBlobDelegatorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDelegatorRoleId)
    principalId: blobAccessPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The resource ID of the created storage account.')
output resourceId string = storageAccount.id

@description('The name of the created storage account.')
output name string = storageAccount.name

@description('The primary blob service endpoint URL.')
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
