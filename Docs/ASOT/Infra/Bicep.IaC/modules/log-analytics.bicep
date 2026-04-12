// ──────────────────────────────────────────────────────────────
// Module: Log Analytics Workspace
// ──────────────────────────────────────────────────────────────
// Creates a Log Analytics workspace that serves as the backing
// store for Application Insights telemetry. One workspace per
// environment (staging / prod).
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Azure region for the Log Analytics workspace.')
param location string

@description('Resource name for the Log Analytics workspace (e.g. law-rvs-obs-staging-wus3-s01-001).')
param workspaceName string

@description('Tags to apply to the workspace.')
param tags object = {}

@description('Log retention in days. Free tier allows 7; pay-as-you-go allows 30-730.')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

// ── Resources ─────────────────────────────────────────────────

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the Log Analytics workspace.')
output resourceId string = workspace.id

@description('Name of the Log Analytics workspace.')
output name string = workspace.name

@description('Workspace ID (GUID) used for linking Application Insights.')
output workspaceId string = workspace.properties.customerId
