// ──────────────────────────────────────────────────────────────
// Module: Standard resource naming and tagging helper
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Resource type prefix based on Azure naming abbreviations (for example oai, kv, rg).')
param resourceTypePrefix string

@description('Platform/application short name.')
param appName string = 'rvs'

@description('Workload or bounded context name (for example ai, api, shared).')
param workload string

@description('Deployment environment.')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string

@description('Azure region name, for example westus2.')
param location string

@description('Deployment stamp identifier for scale units.')
param stamp string = 's01'

@description('Resource instance number.')
param instance string = '001'

@description('Owning team or distribution list.')
param owner string = 'platform-team@example.com'

@description('Cost center or billing code.')
param costCenter string = 'Engineering'

@description('Tenant isolation model for the deployed resource.')
@allowed([
  'shared'
  'dedicated'
])
param tenantModel string = 'shared'

// ── Variables ─────────────────────────────────────────────────

var environmentDisplayName = {
  dev: 'Development'
  staging: 'Staging'
  prod: 'Production'
}

var regionCodeByLocation = {
  westus2: 'wus2'
  eastus2: 'eus2'
  westeurope: 'weu'
}

var normalizedLocation = toLower(replace(location, ' ', ''))
var regionCode = regionCodeByLocation[normalizedLocation] ?? normalizedLocation

var resourceName = '${toLower(resourceTypePrefix)}-${toLower(appName)}-${toLower(workload)}-${environmentName}-${regionCode}-${toLower(stamp)}-${instance}'

var tags = {
  Application: toLower(appName)
  Environment: environmentDisplayName[environmentName]
  EnvironmentCode: environmentName
  Workload: toLower(workload)
  Region: normalizedLocation
  RegionCode: regionCode
  Stamp: toLower(stamp)
  Owner: owner
  CostCenter: costCenter
  TenantModel: tenantModel
  ManagedBy: 'Bicep'
}

// ── Outputs ───────────────────────────────────────────────────

@description('Computed resource name from the standard naming pattern.')
output resourceName string = resourceName

@description('Computed Azure region short code.')
output regionCode string = regionCode

@description('Standardized tags to apply to created resources.')
output tags object = tags
