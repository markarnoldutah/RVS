// ──────────────────────────────────────────────────────────────
// Module: Application Insights
// ──────────────────────────────────────────────────────────────
// Creates a workspace-based Application Insights resource linked
// to a Log Analytics workspace. Optionally creates a standard
// availability test (URL ping) on the API /health endpoint, whose
// frequency and locations are parameters because each run is billed.
// monitor-alerts.bicep alerts on the test failing and on the test
// passing while App Insights records nothing (#602).
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

@description('Seconds between availability test runs, per location. 900 cuts runs 3× against 300; the dark-telemetry alert in monitor-alerts.bicep is sized to tolerate either.')
@allowed([
  300
  900
])
param availabilityTestFrequencySeconds int = 900

@description('Availability test location IDs (e.g. us-ca-sjc-azr). Each location is billed per run, so one location every 15 minutes is ~2.9K runs/month and three every 5 minutes is ~26K.')
@minLength(1)
param availabilityTestLocations array = [
  'us-ca-sjc-azr'
]

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
    Frequency: availabilityTestFrequencySeconds
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [for l in availabilityTestLocations: { Id: l }]
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

@description('Resource ID of the /health availability test. Empty when no test is deployed.')
output availabilityTestId string = (deployAvailabilityTest && !empty(healthCheckUrl)) ? availabilityTest.id : ''

@description('Application Insights instrumentation key.')
output instrumentationKey string = appInsights.properties.InstrumentationKey

@description('Application Insights connection string.')
output connectionString string = appInsights.properties.ConnectionString
