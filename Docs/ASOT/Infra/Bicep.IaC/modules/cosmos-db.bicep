// ──────────────────────────────────────────────────────────────
// Module: Azure Cosmos DB for NoSQL
// ──────────────────────────────────────────────────────────────
// Provisions a Cosmos DB account with the RVS database and all
// 10 containers used by the platform. Each container has a
// tailored partition key, indexing policy, and optional unique
// key constraints matching the data model spec in
// Docs/ASOT/RVS_Cosmosdb_data_model.md.
//
// Containers:
//   1. service-requests      (PK=/tenantId)
//   2. customer-profiles     (PK=/tenantId)
//   3. global-customer-accounts (PK=/email)
//   4. asset-ledger          (PK=/assetId)
//   5. dealerships           (PK=/tenantId)
//   6. locations             (PK=/tenantId)
//   7. slug-lookups          (PK=/slug)
//   8. tenant-configs        (PK=/tenantId)
//   9. lookup-sets           (PK=/category)
//  10. rv-warranty-rules     (PK=/manufacturer)
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Cosmos DB account name (e.g. cosmos-rvs-data-dev-wus3-s01-001).')
param name string

@description('Azure region for the Cosmos DB account.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Default consistency level.')
@allowed([
  'Session'
  'Eventual'
  'ConsistentPrefix'
  'Strong'
  'BoundedStaleness'
])
param defaultConsistencyLevel string = 'Session'

@description('Continuous backup tier. Use Continuous7Days for dev, Continuous30Days for prod.')
@allowed([
  'Continuous7Days'
  'Continuous30Days'
])
param backupTier string = 'Continuous7Days'

@description('Enable automatic failover (recommended for prod).')
param enableAutomaticFailover bool = false

@description('Database name.')
param databaseName string = 'rvsdb'

@description('Shared database-level autoscale max throughput in RU/s. Set to 0 to use per-container throughput instead. Recommended: 1000 for dev (shared), 4000+ for prod.')
param sharedThroughput int = 1000

@description('The target environment (dev, staging, or prod). Controls network access rules.')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string = 'dev'

// ── Variables ─────────────────────────────────────────────────

var isProduction = environmentName == 'prod'

// ── Cosmos DB Account ─────────────────────────────────────────

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: name
  location: location
  kind: 'GlobalDocumentDB'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: defaultConsistencyLevel
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: isProduction
      }
    ]
    enableAutomaticFailover: enableAutomaticFailover
    backupPolicy: {
      type: 'Continuous'
      continuousModeProperties: {
        tier: backupTier
      }
    }
    publicNetworkAccess: 'Enabled'
    enableFreeTier: false
    isVirtualNetworkFilterEnabled: false
    disableLocalAuth: false
  }
}

// ── Database ──────────────────────────────────────────────────

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
    options: sharedThroughput > 0
      ? {
          autoscaleSettings: {
            maxThroughput: sharedThroughput
          }
        }
      : {}
  }
}

// ── Containers ────────────────────────────────────────────────

// 1. service-requests — Primary transactional container
resource serviceRequests 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'service-requests'
  properties: {
    resource: {
      id: 'service-requests'
      partitionKey: {
        paths: ['/tenantId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/tenantId/?' }
          { path: '/status/?' }
          { path: '/locationId/?' }
          { path: '/customerProfileId/?' }
          { path: '/issueCategory/?' }
          { path: '/createdAtUtc/?' }
          { path: '/scheduledDateUtc/?' }
          { path: '/type/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
        compositeIndexes: [
          [
            { path: '/status', order: 'ascending' }
            { path: '/createdAtUtc', order: 'descending' }
          ]
          [
            { path: '/locationId', order: 'ascending' }
            { path: '/status', order: 'ascending' }
          ]
        ]
      }
    }
  }
}

// 2. customer-profiles — Tenant-scoped customer shadow profiles
resource customerProfiles 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'customer-profiles'
  properties: {
    resource: {
      id: 'customer-profiles'
      partitionKey: {
        paths: ['/tenantId']
        kind: 'Hash'
      }
      uniqueKeyPolicy: {
        uniqueKeys: [
          { paths: ['/tenantId', '/email'] }
        ]
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/tenantId/?' }
          { path: '/email/?' }
          { path: '/globalCustomerAcctId/?' }
          { path: '/type/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
      }
    }
  }
}

// 3. global-customer-accounts — Cross-tenant customer identity
resource globalCustomerAccounts 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'global-customer-accounts'
  properties: {
    resource: {
      id: 'global-customer-accounts'
      partitionKey: {
        paths: ['/email']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/email/?' }
          { path: '/magicLinkToken/?' }
          { path: '/type/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
      }
    }
  }
}

// 4. asset-ledger — Append-only cross-tenant service history
resource assetLedger 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'asset-ledger'
  properties: {
    resource: {
      id: 'asset-ledger'
      partitionKey: {
        paths: ['/assetId']
        kind: 'Hash'
      }
      uniqueKeyPolicy: {
        uniqueKeys: [
          { paths: ['/assetId', '/serviceRequestId'] }
        ]
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/assetId/?' }
          { path: '/tenantId/?' }
          { path: '/serviceRequestId/?' }
          { path: '/submittedAtUtc/?' }
          { path: '/status/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
      }
    }
  }
}

// 5. dealerships — Multi-document: Dealership + Tenant entities
resource dealerships 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'dealerships'
  properties: {
    resource: {
      id: 'dealerships'
      partitionKey: {
        paths: ['/tenantId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/tenantId/?' }
          { path: '/type/?' }
          { path: '/slug/?' }
          { path: '/name/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
        compositeIndexes: [
          [
            { path: '/type', order: 'ascending' }
            { path: '/name', order: 'ascending' }
          ]
        ]
      }
    }
  }
}

// 6. locations — Service locations within dealerships
resource locations 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'locations'
  properties: {
    resource: {
      id: 'locations'
      partitionKey: {
        paths: ['/tenantId']
        kind: 'Hash'
      }
      uniqueKeyPolicy: {
        uniqueKeys: [
          { paths: ['/tenantId', '/slug'] }
        ]
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/tenantId/?' }
          { path: '/slug/?' }
          { path: '/type/?' }
          { path: '/name/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
      }
    }
  }
}

// 7. slug-lookups — O(1) URL slug resolution index
resource slugLookups 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'slug-lookups'
  properties: {
    resource: {
      id: 'slug-lookups'
      partitionKey: {
        paths: ['/slug']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/slug/?' }
          { path: '/tenantId/?' }
          { path: '/locationId/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
      }
    }
  }
}

// 8. tenant-configs — Per-tenant configuration
resource tenantConfigs 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'tenant-configs'
  properties: {
    resource: {
      id: 'tenant-configs'
      partitionKey: {
        paths: ['/tenantId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/tenantId/?' }
          { path: '/type/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
      }
    }
  }
}

// 9. lookup-sets — Global reference data (categories, service types)
resource lookupSets 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'lookup-sets'
  properties: {
    resource: {
      id: 'lookup-sets'
      partitionKey: {
        paths: ['/category']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/category/?' }
          { path: '/tenantId/?' }
          { path: '/type/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
        compositeIndexes: [
          [
            { path: '/category', order: 'ascending' }
            { path: '/name', order: 'ascending' }
          ]
        ]
      }
    }
  }
}

// 10. rv-warranty-rules — Global RV manufacturer warranty reference data
resource rvWarrantyRules 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'rv-warranty-rules'
  properties: {
    resource: {
      id: 'rv-warranty-rules'
      partitionKey: {
        paths: ['/manufacturer']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          { path: '/manufacturer/?' }
          { path: '/brandDivision/?' }
          { path: '/type/?' }
        ]
        excludedPaths: [
          { path: '/*' }
          { path: '/"_etag"/?' }
        ]
      }
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The resource ID of the Cosmos DB account.')
output resourceId string = cosmosAccount.id

@description('The name of the Cosmos DB account.')
output name string = cosmosAccount.name

@description('The Cosmos DB account endpoint URL.')
output endpoint string = cosmosAccount.properties.documentEndpoint

@description('The principal ID of the system-assigned managed identity.')
output principalId string = cosmosAccount.identity.principalId

@description('The database name.')
output databaseName string = database.name
