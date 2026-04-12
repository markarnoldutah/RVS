// ──────────────────────────────────────────────────────────────
// Module: Log Analytics Workspace
// ──────────────────────────────────────────────────────────────
// Centralized log ingestion for Application Insights, diagnostic
// logs, and Azure Monitor. All telemetry data flows through this
// workspace for unified querying and alerting.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The Log Analytics Workspace name following the naming convention (e.g. law-rvs-obs-dev-wus3-s01-001).')
param name string

@description('Azure region for the workspace.')
param location string

@description('Data retention period in days.')
@minValue(30)
@maxValue(730)
param retentionInDays int = 90

@description('Daily ingestion cap in GB. Use -1 for unlimited (not recommended for dev).')
param dailyQuotaGb int = 5

@description('Tags to apply to the workspace.')
param tags object = {}

// ── Resources ─────────────────────────────────────────────────

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    workspaceCapping: dailyQuotaGb > 0
      ? {
          dailyQuotaGb: dailyQuotaGb
        }
      : null
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The resource ID of the Log Analytics Workspace.')
output resourceId string = workspace.id

@description('The workspace customer ID (GUID) used for agent configuration.')
output workspaceId string = workspace.properties.customerId

@description('The name of the Log Analytics Workspace.')
output name string = workspace.name
