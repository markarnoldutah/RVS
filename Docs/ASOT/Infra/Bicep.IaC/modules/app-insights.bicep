// ──────────────────────────────────────────────────────────────
// Module: Application Insights
// ──────────────────────────────────────────────────────────────
// Workspace-based Application Insights for APM telemetry,
// distributed tracing, and custom dimensions (tenantId, userId).
// Optionally creates a standard availability test against
// the API health endpoint.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Application Insights resource name (e.g. appi-rvs-api-dev-wus3-s01-001).')
param name string

@description('Azure region for the resource.')
param location string

@description('Resource ID of the Log Analytics Workspace for workspace-based mode.')
param workspaceResourceId string

@description('Application type.')
@allowed([
  'web'
  'other'
])
param applicationType string = 'web'

@description('Tags to apply to the resource.')
param tags object = {}

@description('When true, creates a standard availability test against the health endpoint.')
param enableAvailabilityTest bool = false

@description('URL to test for availability (e.g. https://app-rvs-api-dev.azurewebsites.net/health/live).')
param availabilityTestUrl string = ''

@description('Frequency of the availability test in seconds.')
@allowed([
  300
  600
  900
])
param availabilityTestFrequency int = 300

// ── Resources ─────────────────────────────────────────────────

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: applicationType
    WorkspaceResourceId: workspaceResourceId
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource availabilityTest 'Microsoft.Insights/webtests@2022-06-15' = if (enableAvailabilityTest && !empty(availabilityTestUrl)) {
  name: 'avail-${name}'
  location: location
  tags: union(tags, {
    'hidden-link:${appInsights.id}': 'Resource'
  })
  kind: 'standard'
  properties: {
    SyntheticMonitorId: 'avail-${name}'
    Name: '${name} Health Check'
    Enabled: true
    Frequency: availabilityTestFrequency
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [
      { Id: 'us-va-ash-azr' }
      { Id: 'us-ca-sjc-azr' }
      { Id: 'us-il-ch1-azr' }
    ]
    Request: {
      RequestUrl: availabilityTestUrl
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

@description('The resource ID of Application Insights.')
output resourceId string = appInsights.id

@description('The connection string for SDK configuration.')
output connectionString string = appInsights.properties.ConnectionString

@description('The instrumentation key (legacy, prefer connection string).')
output instrumentationKey string = appInsights.properties.InstrumentationKey

@description('The name of the Application Insights resource.')
output name string = appInsights.name
