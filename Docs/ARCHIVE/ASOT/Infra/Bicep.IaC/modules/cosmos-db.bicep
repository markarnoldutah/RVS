// ──────────────────────────────────────────────────────────────
// Module: Azure Cosmos DB (NoSQL) — Serverless
// ──────────────────────────────────────────────────────────────
// Creates a Cosmos DB account in Serverless capacity mode with
// all 10 RVS application containers and their index policies.
//
// Upgrade path: switch capacityMode to 'Provisioned' and set
// autoscaleMaxThroughput via parameter changes (requires account
// recreation — Serverless cannot be changed in-place).
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Azure region for the Cosmos DB account.')
param location string

@description('Cosmos DB account name (e.g. cosmos-rvs-data-staging-wus3-s01-001).')
param accountName string

@description('Tags to apply to all resources created by this module.')
param tags object = {}

@description('The database name.')
param databaseName string = 'rvs-db'

@description('Cosmos DB capacity mode. Serverless = pay-per-request (MVP). Provisioned = autoscale throughput.')
@allowed([
  'Serverless'
  'Provisioned'
])
param capacityMode string = 'Serverless'

@description('Maximum autoscale throughput (RU/s) when capacityMode is Provisioned. Ignored for Serverless. Range 1000–1000000.')
@minValue(1000)
@maxValue(1000000)
param autoscaleMaxThroughput int = 4000

// ── Variables ─────────────────────────────────────────────────

var isServerless = capacityMode == 'Serverless'

var capabilities = isServerless
  ? [
      { name: 'EnableServerless' }
    ]
  : []

// ── Resources ─────────────────────────────────────────────────

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: accountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    capabilities: capabilities
    publicNetworkAccess: 'Enabled'
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    disableLocalAuth: false
    minimalTlsVersion: 'Tls12'
    backupPolicy: {
      type: 'Continuous'
      continuousModeProperties: {
        tier: 'Continuous7Days'
      }
    }
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
    options: isServerless
      ? {}
      : {
          autoscaleSettings: {
            maxThroughput: autoscaleMaxThroughput
          }
        }
  }
}

// ── Containers ────────────────────────────────────────────────

// 1. service-requests — most queried, PK=/tenantId
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
          { path: '/_etag/?' }
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

// 2. customer-profiles — PK=/tenantId, unique on [/tenantId, /email]
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
          { path: '/_etag/?' }
        ]
      }
    }
  }
}

// 3. global-customer-accounts — PK=/email
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
          { path: '/_etag/?' }
        ]
      }
    }
  }
}

// 4. asset-ledger — PK=/assetId, unique on [/assetId, /serviceRequestId]
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
          { path: '/_etag/?' }
        ]
      }
    }
  }
}

// 5. dealerships — PK=/tenantId (also holds Tenant docs via type discriminator)
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
          { path: '/_etag/?' }
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

// 6. locations — PK=/tenantId, unique on [/tenantId, /slug]
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
          { path: '/_etag/?' }
        ]
      }
    }
  }
}

// 7. slug-lookups — PK=/slug (point reads by slug)
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
          { path: '/_etag/?' }
        ]
      }
    }
  }
}

// 8. tenant-configs — PK=/tenantId (point reads by tenantId)
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
          { path: '/_etag/?' }
        ]
      }
    }
  }
}

// 9. lookup-sets — PK=/category
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
          { path: '/_etag/?' }
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

// 10. rv-warranty-rules — PK=/manufacturer (global reference data)
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
          { path: '/_etag/?' }
        ]
      }
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the Cosmos DB account.')
output resourceId string = cosmosAccount.id

@description('Name of the Cosmos DB account.')
output name string = cosmosAccount.name

@description('Cosmos DB account endpoint URI.')
output endpoint string = cosmosAccount.properties.documentEndpoint

@description('Name of the database.')
output databaseName string = database.name

@description('Principal ID of the system-assigned managed identity.')
output principalId string = cosmosAccount.identity.principalId
