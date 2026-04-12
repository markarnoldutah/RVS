// ──────────────────────────────────────────────────────────────
// Module: Application Insights
// ──────────────────────────────────────────────────────────────
// Creates a workspace-based Application Insights resource linked
// to a Log Analytics workspace. Optionally creates a standard
// availability test (URL ping) on the API /health endpoint.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Azure region for the Application Insights resource.')
param location string

@description('Resource name for the Application Insights instance (e.g. appi-rvs-api-staging-wus3-s01-001).')
param appInsightsName string

@description('Tags to apply to all resources created by this module.')
param tags object = {}

@description('Resource ID of the Log Analytics workspace to link.')
param logAnalyticsWorkspaceId string

@description('When true, creates a standard availability test (URL ping) against the healthCheckUrl.')
param deployAvailabilityTest bool = false

@description('The full URL of the /health endpoint to test (e.g. https://app-rvs-api-staging-wus3-s01-001.azurewebsites.net/health).')
param healthCheckUrl string = ''

// ── Resources ─────────────────────────────────────────────────

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource availabilityTest 'Microsoft.Insights/webtests@2022-06-15' = if (deployAvailabilityTest && !empty(healthCheckUrl)) {
  name: 'avail-${appInsightsName}'
  location: location
  tags: union(tags, {
    'hidden-link:${appInsights.id}': 'Resource'
  })
  kind: 'standard'
  properties: {
    SyntheticMonitorId: 'avail-${appInsightsName}'
    Name: '${appInsightsName} Health Check'
    Enabled: true
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [
      { Id: 'us-va-ash-azr' }
      { Id: 'us-ca-sjc-azr' }
      { Id: 'us-tx-sn1-azr' }
    ]
    Request: {
      RequestUrl: healthCheckUrl
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the Application Insights instance.')
output resourceId string = appInsights.id

@description('Name of the Application Insights instance.')
output name string = appInsights.name

@description('Application Insights instrumentation key.')
output instrumentationKey string = appInsights.properties.InstrumentationKey

@description('Application Insights connection string.')
output connectionString string = appInsights.properties.ConnectionString
