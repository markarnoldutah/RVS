// ──────────────────────────────────────────────────────────────
// Module: Event Grid system topic + subscription for ACS SMS
// ──────────────────────────────────────────────────────────────
// Delivers the two inbound ACS SMS events to the API (issue #665):
//   • Microsoft.Communication.SMSReceived              — carrier keywords (STOP/START/UNSTOP)
//   • Microsoft.Communication.SMSDeliveryReportReceived — delivery status for invites
//
// The webhook is anonymous — Event Grid presents no bearer token — so the
// subscription's endpoint URL carries a shared secret that the API checks on
// every request. The same secret is written to Key Vault as
// EventGrid--Inbound--Key, which the API's Key Vault configuration provider
// binds to EventGrid:Inbound:Key, so the two sides cannot drift.
//
// ORDERING: creating a subscription triggers Event Grid's validation
// handshake against the live endpoint, and the API has to answer it with the
// secret already loaded. This deployment writes that secret to Key Vault too,
// but in parallel with the subscription, so it cannot be what makes the API
// ready. On a first bring-up, seed the vault secret by hand and restart the API
// BEFORE running this deployment:
//   1. the API carrying /api/events/acs-sms is deployed (merge to main)
//   2. az keyvault secret set  EventGrid--Inbound--Key
//   3. az webapp restart       (the API reads Key Vault at startup only)
//   4. deploy this template with eventGridWebhookKey set
// The runbook with exact commands is in RVS_Infrastructure.md.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The name of the existing ACS resource that raises the SMS events.')
param acsName string

@description('Name for the Event Grid system topic.')
param systemTopicName string

@description('Tags applied to the system topic.')
param tags object

@description('The API hostname that receives the events, e.g. api-staging.rvserviceflow.com. No scheme.')
param apiHostName string

@description('Shared secret the subscription presents as the key query parameter. Must match EventGrid--Inbound--Key in Key Vault; the API refuses every request when it is unset.')
@secure()
param eventGridWebhookKey string

// ── Existing Resource References ──────────────────────────────

// 2025-05-01 does not exist; 2025-09-01 fails domain validation at deploy time.
#disable-next-line use-recent-api-versions
resource acsAccount 'Microsoft.Communication/communicationServices@2023-04-01' existing = {
  name: acsName
}

// ── System Topic ──────────────────────────────────────────────

@description('ACS is a global resource, so its system topic is global too.')
resource systemTopic 'Microsoft.EventGrid/systemTopics@2025-02-15' = {
  name: systemTopicName
  location: 'global'
  tags: tags
  properties: {
    source: acsAccount.id
    topicType: 'Microsoft.Communication.CommunicationServices'
  }
}

// ── Subscription ──────────────────────────────────────────────

@description('Only the two SMS event types RVS acts on. Chat, calling and email events are not subscribed.')
resource smsSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2025-02-15' = {
  parent: systemTopic
  name: 'acs-sms-to-api'
  properties: {
    destination: {
      endpointType: 'WebHook'
      properties: {
        // The key is a @secure() parameter; the linter cannot see that through string
        // interpolation, and Event Grid has no other way to authenticate to an anonymous
        // endpoint. The URL is write-only in ARM and never appears in an output.
        #disable-next-line use-secure-value-for-secure-inputs
        endpointUrl: 'https://${apiHostName}/api/events/acs-sms?key=${eventGridWebhookKey}'
        maxEventsPerBatch: 10
        preferredBatchSizeInKilobytes: 64
      }
    }
    filter: {
      includedEventTypes: [
        'Microsoft.Communication.SMSReceived'
        'Microsoft.Communication.SMSDeliveryReportReceived'
      ]
    }
    eventDeliverySchema: 'EventGridSchema'
    retryPolicy: {
      maxDeliveryAttempts: 10
      eventTimeToLiveInMinutes: 1440
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the Event Grid system topic.')
output systemTopicId string = systemTopic.id

@description('Name of the Event Grid system topic.')
output systemTopicName string = systemTopic.name
