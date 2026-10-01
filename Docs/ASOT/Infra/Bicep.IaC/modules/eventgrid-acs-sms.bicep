// ──────────────────────────────────────────────────────────────
// Module: Event Grid system topic + subscription for ACS SMS
// ──────────────────────────────────────────────────────────────
// Delivers the two inbound ACS SMS events to the API (issue #665):
//   • Microsoft.Communication.SMSReceived              — carrier keywords (STOP/START/UNSTOP)
//   • Microsoft.Communication.SMSDeliveryReportReceived — delivery status for invites
//
// The webhook is anonymous — Event Grid presents no bearer token — so the
// subscription's endpoint URL carries a shared secret that the API checks on
// every request. Key Vault is the only source of truth for it (#678): the
// secret EventGrid--Inbound--Key is created by hand, the API's Key Vault
// configuration provider binds it to EventGrid:Inbound:Key, and each
// .bicepparam reads the same secret back with az.getSecret to build the URL
// below. Nothing writes it from Bicep and nobody passes it on a command line.
//
// ORDERING: creating a subscription triggers Event Grid's validation
// handshake against the live endpoint, and the API has to answer it with the
// secret already loaded. The API reads Key Vault at startup only, so on a
// first bring-up, and after any rotation, the order is:
//   1. az keyvault secret set  EventGrid--Inbound--Key
//   2. az webapp restart       (the API picks up the value only on start)
//   3. deploy main.bicep       (az.getSecret reads the value from step 1)
// The runbook with exact commands, including a brand-new environment whose
// vault does not exist yet, is in RVS_Infrastructure.md.
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

@description('Shared secret the subscription presents as the key query parameter. Read from EventGrid--Inbound--Key in Key Vault by the .bicepparam (az.getSecret), so it always matches what the API loaded at its last restart.')
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
