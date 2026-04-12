// ──────────────────────────────────────────────────────────────
// Module: Azure App Service Plan (Linux)
// ──────────────────────────────────────────────────────────────
// Linux App Service Plan hosting the ASP.NET Core 10 REST API.
// SKU varies by environment: B1 (dev), P1v3 (staging), P2v3 (prod).
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('App Service Plan name (e.g. plan-rvs-api-dev-wus3-s01-001).')
param name string

@description('Azure region for the App Service Plan.')
param location string

@description('Tags to apply to the resource.')
param tags object = {}

@description('SKU name. B1 for dev, P1v3 for staging, P2v3 for prod.')
@allowed([
  'B1'
  'P1v3'
  'P2v3'
  'P3v3'
])
param skuName string = 'B1'

@description('Number of worker instances.')
@minValue(1)
param skuCapacity int = 1

@description('Enable zone redundancy (production only, requires P1v3+).')
param zoneRedundant bool = false

// ── Resources ─────────────────────────────────────────────────

resource appServicePlan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: name
  location: location
  tags: tags
  kind: 'linux'
  properties: {
    reserved: true
    zoneRedundant: zoneRedundant
  }
  sku: {
    name: skuName
    capacity: skuCapacity
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The resource ID of the App Service Plan.')
output resourceId string = appServicePlan.id

@description('The name of the App Service Plan.')
output name string = appServicePlan.name
