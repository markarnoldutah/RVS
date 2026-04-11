// ──────────────────────────────────────────────────────────────
// RG Scaffold – RVS Azure Resource Group Pre-provisioning
// ──────────────────────────────────────────────────────────────
// Creates all 9 resource groups (3 environments × 3 regions)
// in a single subscription-scoped deployment so that subsequent
// environment-specific deployments find their target RGs ready.
//
//   Regions  : westus2, westus3, northcentralus (ncus)
//   Environments : dev, staging, prod
//
// Usage:
//   az deployment sub create --location westus3 \
//     --template-file Docs/ASOT/Infra/Bicep.IaC/rg-scaffold.bicep \
//     --name "rvs-rg-scaffold-$(Get-Date -Format 'yyyyMMddHHmm')"
// ──────────────────────────────────────────────────────────────
targetScope = 'subscription'

// ── Parameters ────────────────────────────────────────────────

@description('Owning team or distribution list for the Owner tag.')
param owner string = 'platform-team@rvserviceflow.com'

@description('Cost center or billing code for the CostCenter tag.')
param costCenter string = 'Engineering'

// ── Variables ─────────────────────────────────────────────────

var environmentDisplayName = {
  dev:     'Development'
  staging: 'Staging'
  prod:    'Production'
}

// All 9 resource groups: 3 environments × 3 regions
var rgDefs = [
  { name: 'rg-rvs-dev-westus2',     location: 'westus2',        regionCode: 'wus2', env: 'dev'     }
  { name: 'rg-rvs-dev-westus3',     location: 'westus3',        regionCode: 'wus3', env: 'dev'     }
  { name: 'rg-rvs-dev-ncus',        location: 'northcentralus', regionCode: 'ncus', env: 'dev'     }
  { name: 'rg-rvs-staging-westus2', location: 'westus2',        regionCode: 'wus2', env: 'staging' }
  { name: 'rg-rvs-staging-westus3', location: 'westus3',        regionCode: 'wus3', env: 'staging' }
  { name: 'rg-rvs-staging-ncus',    location: 'northcentralus', regionCode: 'ncus', env: 'staging' }
  { name: 'rg-rvs-prod-westus2',    location: 'westus2',        regionCode: 'wus2', env: 'prod'    }
  { name: 'rg-rvs-prod-westus3',    location: 'westus3',        regionCode: 'wus3', env: 'prod'    }
  { name: 'rg-rvs-prod-ncus',       location: 'northcentralus', regionCode: 'ncus', env: 'prod'    }
]

// ── Resource Groups ───────────────────────────────────────────

resource resourceGroups 'Microsoft.Resources/resourceGroups@2024-07-01' = [for rg in rgDefs: {
  name:     rg.name
  location: rg.location
  tags: {
    Application:     'rvs'
    Environment:     environmentDisplayName[rg.env]
    EnvironmentCode: rg.env
    Region:          rg.location
    RegionCode:      rg.regionCode
    Owner:           owner
    CostCenter:      costCenter
    ManagedBy:       'Bicep'
  }
}]

// ── Outputs ───────────────────────────────────────────────────

@description('Names of all provisioned resource groups.')
output resourceGroupNames array = [for i in range(0, length(rgDefs)): resourceGroups[i].name]
