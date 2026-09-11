// ──────────────────────────────────────────────────────────────
// Module: Monitor Alerts — packet-pipeline critical events (#494)
// ──────────────────────────────────────────────────────────────
// Turns the packet-pipeline LogCritical events, which today only
// land in Application Insights, into actionable Azure Monitor
// alerts routed to an ops action group. Covers:
//
//   EventId 439002  AllRecipientsBounced        (LocationService)        — page
//   EventId 438001  PacketEmailDeliveryExhausted (PacketGenerationSvc)   — page
//   EventId 434001  PacketGenerationExhausted    (PacketGenerationSvc)   — page
//   EventId 521001  PacketEmailOversized         (PacketGenerationSvc)   — page
//   EventId 439001  RecipientHardBounced         (LocationService)       — warn
//
// Each event is emitted with structured log properties (LocationId /
// ServiceRequestId + TenantId). The Application Insights ILogger
// provider projects those into `customDimensions`, and the alert
// queries surface them as split dimensions so the alert payload
// carries the tenant and the offending location / request.
//
// The 434001 path logs with an exception argument, so its record
// lands in `exceptions` rather than `traces`; every rule therefore
// queries `union traces, exceptions` and is robust to a LogCritical
// gaining an exception argument later.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Azure region for the scheduled-query-rule resources. Must match the region of the Application Insights scope.')
param location string

@description('Resource ID of the workspace-based Application Insights component the packet pipeline logs to.')
param appInsightsResourceId string

@description('Target environment (staging or prod). Drives the action-group short name and the alert display names.')
@allowed([
  'staging'
  'prod'
])
param environmentName string

@description('Tags applied to every resource this module creates.')
param tags object = {}

@description('Email receivers for the ops action group. Each item: { name: string, email: string }. Empty = an action group with no receivers — add them in the portal or pass on the CLI, the same way the Auth0 values are handled.')
param opsEmailReceivers array = []

// ── Variables ─────────────────────────────────────────────────

// Page-worthy: something is silently not being delivered and nobody is told.
// evaluationFrequency == windowSize == 5 minutes is the practical near-real-time
// floor for log-search alerts and avoids partial-bucket flapping.
var criticalEvents = [
  {
    slug: 'recipients-bounced'
    eventId: '439002'
    idColumn: 'LocationId'
    description: 'EventId 439002 AllRecipientsBounced — a hard bounce removed a location\'s last active packet recipient. No packets can be delivered for that location until an address is fixed in its packet settings. Runbook: RVS Infra/Bicep.IaC/README.md "Monitoring & alerts".'
  }
  {
    slug: 'email-delivery-exhausted'
    eventId: '438001'
    idColumn: 'ServiceRequestId'
    description: 'EventId 438001 PacketEmailDeliveryExhausted — packet email delivery exhausted its 3 retries for a service request. The shop has a request with no packet in its inbox.'
  }
  {
    slug: 'generation-exhausted'
    eventId: '434001'
    idColumn: 'ServiceRequestId'
    description: 'EventId 434001 PacketGenerationExhausted — packet generation exhausted its 3 attempts for a service request. No packet was produced.'
  }
  {
    slug: 'email-oversized'
    eventId: '521001'
    idColumn: 'ServiceRequestId'
    description: 'EventId 521001 PacketEmailOversized — the packet email went out without its PDF because the PDF no longer fits the ACS size budget. Since #566 validates the budget at startup, this means the PDF grew unexpectedly: a renderer regression, an oversized embedded asset (e.g. a per-location logo), or a pathological HTML body. Treat any occurrence as a bug to chase.'
  }
]

var actionGroupShortName = environmentName == 'prod' ? 'rvs-ops-prod' : 'rvs-ops-stg'

// ── Resources ─────────────────────────────────────────────────

resource opsActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-rvs-ops-${environmentName}-wus3'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: actionGroupShortName
    enabled: true
    emailReceivers: [
      for r in opsEmailReceivers: {
        name: r.name
        emailAddress: r.email
        useCommonAlertSchema: true
      }
    ]
  }
}

resource criticalRules 'Microsoft.Insights/scheduledQueryRules@2026-03-01' = [
  for e in criticalEvents: {
    name: 'sqr-rvs-packet-${e.slug}-${environmentName}-wus3'
    location: location
    tags: tags
    kind: 'LogAlert'
    properties: {
      displayName: '[RVS ${environmentName}] Packet pipeline critical — ${e.slug} (EventId ${e.eventId})'
      description: e.description
      severity: 1
      enabled: true
      scopes: [
        appInsightsResourceId
      ]
      evaluationFrequency: 'PT5M'
      windowSize: 'PT5M'
      autoMitigate: true
      criteria: {
        allOf: [
          {
            query: 'union traces, exceptions | where tostring(customDimensions.EventId) == "${e.eventId}" | extend ${e.idColumn} = tostring(customDimensions.${e.idColumn}), TenantId = tostring(customDimensions.TenantId) | project ${e.idColumn}, TenantId'
            timeAggregation: 'Count'
            operator: 'GreaterThan'
            threshold: 0
            dimensions: [
              {
                name: e.idColumn
                operator: 'Include'
                values: [
                  '*'
                ]
              }
              {
                name: 'TenantId'
                operator: 'Include'
                values: [
                  '*'
                ]
              }
            ]
            failingPeriods: {
              numberOfEvaluationPeriods: 1
              minFailingPeriodsToAlert: 1
            }
          }
        ]
      }
      actions: {
        actionGroups: [
          opsActionGroup.id
        ]
      }
    }
  }
]

// EventId 439001 — one recipient disabled, others still receive packets. Lower
// tier by design: not a delivery failure, just a "fix the address before it
// becomes the last one (439002)" signal. Severity 3, a 6-hour window evaluated
// hourly so it reads as a digest rather than a page, routed to the same ops
// action group.
resource recipientBounceWarning 'Microsoft.Insights/scheduledQueryRules@2026-03-01' = {
  name: 'sqr-rvs-packet-recipient-bounced-warn-${environmentName}-wus3'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    displayName: '[RVS ${environmentName}] Packet recipient hard-bounced (EventId 439001)'
    description: 'EventId 439001 RecipientHardBounced — a hard bounce disabled one packet recipient for a location; other recipients still receive packets. Warning tier: digest to ops, not a page. Fix or replace the address in the location\'s packet settings before it becomes the last active recipient (439002).'
    severity: 3
    enabled: true
    scopes: [
      appInsightsResourceId
    ]
    evaluationFrequency: 'PT1H'
    windowSize: 'PT6H'
    autoMitigate: true
    criteria: {
      allOf: [
        {
          query: 'union traces, exceptions | where tostring(customDimensions.EventId) == "439001" | extend LocationId = tostring(customDimensions.LocationId), TenantId = tostring(customDimensions.TenantId) | project LocationId, TenantId'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          dimensions: [
            {
              name: 'LocationId'
              operator: 'Include'
              values: [
                '*'
              ]
            }
            {
              name: 'TenantId'
              operator: 'Include'
              values: [
                '*'
              ]
            }
          ]
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        opsActionGroup.id
      ]
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the ops action group.')
output actionGroupId string = opsActionGroup.id

@description('Name of the ops action group.')
output actionGroupName string = opsActionGroup.name

@description('Names of the critical (page) scheduled-query alert rules.')
output criticalRuleNames array = [for (e, i) in criticalEvents: criticalRules[i].name]

@description('Name of the warning-tier (digest) scheduled-query alert rule.')
output warningRuleName string = recipientBounceWarning.name
