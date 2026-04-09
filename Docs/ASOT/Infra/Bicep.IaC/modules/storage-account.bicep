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

// ── Resources ─────────────────────────────────────────────────

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
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
    allowSharedKeyAccess: true
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

// ── Outputs ───────────────────────────────────────────────────

@description('The resource ID of the created storage account.')
output resourceId string = storageAccount.id

@description('The name of the created storage account.')
output name string = storageAccount.name

@description('The primary blob service endpoint URL.')
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
