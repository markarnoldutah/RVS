# RVS SMS Notification Architecture — Provider Evaluation & Implementation Plan

**Authoritative Source of Truth (ASOT) — April 8, 2026**
**Status:** Planning — No Code Changes Yet

This document evaluates SMS notification providers for RVS, recommends the right platform for the use case, and defines the architecture for integrating SMS into the existing notification pipeline. It addresses dealer-to-customer communications, magic link delivery via SMS, and inbound message handling.

---

## Context

The PRD (`RVS_PRD.md` FR-016) identifies SMS as a future enhancement beyond email notifications. The current notification pipeline is:

- **Interface:** `INotificationService` in `RVS.Domain/Integrations/`
- **Production:** `SendGridNotificationService` — transactional email via SendGrid REST API
- **Development:** `NoOpNotificationService` — logs only, no external calls
- **Orchestration:** `IntakeOrchestrationService` fires `SendServiceRequestConfirmationAsync` as fire-and-forget in Step 7

The Manager app gap analysis (`RVS_Features_Blazor.Manager.md`) defers "request additional information" to Phase 2, noting managers currently contact customers externally using `CustomerSnapshotEmbedded` contact details.

### Use Cases for SMS

1. **Magic link delivery** — Customer opts to receive their status page link via text instead of (or in addition to) email
2. **Service request confirmation** — Text confirmation when an intake is submitted
3. **Status change alerts** — Notify customer when SR moves to `InProgress`, `Completed`, etc.
4. **Dealer-to-customer messaging** — Service advisor sends a scheduling question, parts update, or follow-up from the Manager app; message is linked to the service request
5. **Customer-to-dealer replies** — Customer replies to dealer texts; replies are captured and linked to the originating service request (inbound SMS)

---

## 1. Provider Comparison: Twilio vs Azure Communication Services

### 1.1 Feature Matrix

| Capability | **Twilio** | **Azure Communication Services (ACS)** |
|---|---|---|
| **SMS Send (US)** | Toll-free, local, short code, 10DLC | Toll-free, short code; 10DLC via preview |
| **SMS Receive (Inbound)** | Webhook-based, mature | Event Grid-based, generally available |
| **Two-way conversations** | Native Conversations API | Manual routing via Event Grid + custom logic |
| **MMS (photos/media)** | Supported (US/Canada) | Not supported (text only) |
| **Toll-free verification** | Self-service portal | Self-service via Azure portal |
| **10DLC registration** | Mature, self-service TCR integration | Preview; limited tooling |
| **Short code provisioning** | Self-service, 8-12 week lead time | Available, similar lead time |
| **Delivery receipts** | Webhook callbacks per message | Event Grid delivery status events |
| **International SMS** | 180+ countries | 180+ countries |
| **Opt-out management** | Built-in STOP/START handling for toll-free | Built-in opt-out for toll-free and short code |
| **Phone number types** | Local, toll-free, short code, alphanumeric sender ID | Toll-free, short code, alphanumeric sender ID (no local numbers for SMS) |
| **SDK quality (.NET)** | `Twilio` NuGet, mature, strongly typed | `Azure.Communication.Sms` NuGet, first-party Azure SDK |
| **REST API** | Mature, extensive documentation | Standard Azure REST with AAD/connection string auth |
| **Identity integration** | API key or OAuth 2.0 token | **Azure Managed Identity** — no secrets to manage |
| **Observability** | Twilio Console + webhook logs | **Azure Monitor / App Insights** — native integration |
| **Azure portal integration** | Separate portal, separate billing | **Single pane of glass** — same subscription, same RBAC |
| **Marketplace billing** | Separate Twilio account | **Azure Marketplace** — consolidated Azure invoice |
| **Compliance (HIPAA BAA)** | Available on Enterprise plan | Included with Azure compliance |
| **Email sending** | Via SendGrid (Twilio subsidiary) | Azure Communication Services Email (GA) |
| **Voice calling** | Programmable Voice | Calling SDK (PSTN + VoIP) |
| **Chat / messaging** | Twilio Conversations (multi-channel) | ACS Chat (in-app messaging) |

### 1.2 Cost Comparison

Pricing as of April 2026. All prices USD.

#### Phone Number Costs

| Number Type | **Twilio** | **ACS** |
|---|---|---|
| Toll-free number (monthly) | $2.00/mo | $2.00/mo |
| Short code (monthly) | $1,000/mo | $1,000/mo |
| Local number (monthly) | $1.15/mo | N/A (not available for SMS) |

#### Per-Message Costs (US Domestic)

| Direction | **Twilio** | **ACS** |
|---|---|---|
| Outbound SMS (toll-free) | $0.0079/msg | $0.0079/msg |
| Inbound SMS (toll-free) | $0.0079/msg | $0.0079/msg |
| Outbound SMS (short code) | $0.0079/msg | $0.0079/msg |
| Inbound SMS (short code) | $0.0079/msg | $0.0079/msg |
| Carrier surcharges (toll-free) | ~$0.003/msg (variable) | ~$0.003/msg (variable) |
| Carrier surcharges (10DLC) | ~$0.003-$0.006/msg | ~$0.003-$0.006/msg |

> **Key insight:** Per-message costs are nearly identical. The differentiation is in platform features, operational complexity, and ecosystem integration — not raw SMS pricing.

#### Registration Costs

| Registration | **Twilio** | **ACS** |
|---|---|---|
| 10DLC brand registration | $4 (one-time) | $4 (one-time, via TCR) |
| 10DLC campaign registration | $15/mo | $15/mo (via TCR) |
| Toll-free verification | Free (self-service) | Free (self-service) |

#### RVS Volume Estimate (MVP to Growth)

| Scenario | Monthly Messages | Monthly Cost (either provider) |
|---|---|---|
| **MVP (10 dealers, 500 SRs/mo)** | ~2,000 outbound | ~$16 + $2 number |
| **Growth (50 dealers, 5,000 SRs/mo)** | ~20,000 outbound + ~5,000 inbound | ~$197 + $2 number |
| **Scale (200 dealers, 50,000 SRs/mo)** | ~200,000 outbound + ~50,000 inbound | ~$1,975 + $2 number |

> **Note:** "Messages per SR" estimate assumes 4 outbound messages per SR lifecycle: confirmation, status update, dealer follow-up, completion notification. Inbound volume assumes ~25% of customers reply.

### 1.3 Multi-Tenant Number Strategy

| Strategy | Pros | Cons | Recommendation |
|---|---|---|---|
| **Shared toll-free number** (all tenants share one number) | Cheapest ($2/mo total), simplest provisioning | No dealer branding, confusing for customers, reply routing complexity | **MVP — Start here** |
| **Per-tenant toll-free number** (each dealer gets their own) | Dealer branding, clear reply routing, customer sees consistent number | $2/mo per tenant, provisioning automation needed | **Phase 2 — Upgrade path** |
| **Per-tenant short code** | Highest throughput, branded | $1,000/mo per tenant, 8-12 week provisioning | Not viable for SMB pricing |

**Recommendation:** Start with a single shared toll-free number. Route inbound replies using message context (the SR ID or dealer slug embedded in outbound messages, or a lookup from the sender's phone number to their most recent SR). Migrate to per-tenant numbers when tenant count justifies the provisioning automation investment.

---

## 2. Recommendation: Azure Communication Services

### 2.1 Decision

**Use Azure Communication Services (ACS) for SMS notifications in RVS.**

### 2.2 Rationale

| Factor | Weight | ACS Advantage |
|---|---|---|
| **Azure ecosystem alignment** | High | RVS is 100% Azure (App Service, Cosmos DB, Blob Storage, Key Vault, App Insights). ACS is a first-party Azure service with native integration. |
| **Managed Identity authentication** | High | No API keys or secrets to rotate. ACS uses the same managed identity that already authenticates to Cosmos DB and Key Vault. Zero additional secret management. |
| **Consolidated billing** | Medium | SMS costs appear on the same Azure invoice as Cosmos DB, App Service, and Blob Storage. No separate Twilio account, no separate payment method, no separate invoice reconciliation. |
| **Observability** | High | ACS emits diagnostic logs to Azure Monitor. SMS delivery events flow to the same App Insights workspace that already tracks API telemetry with `tenantId` dimensions. No webhook infrastructure to build. |
| **Inbound SMS via Event Grid** | Medium | Event Grid subscriptions route inbound SMS to the same App Service (or an Azure Function) without exposing a public webhook endpoint. Event Grid integrates with the existing Azure infrastructure (Key Vault event handlers, Cosmos change feed patterns). |
| **Email unification path** | Medium | ACS also provides transactional email (GA). Future opportunity to migrate from SendGrid to ACS Email, consolidating all notification channels under one service. |
| **Per-message cost** | Low | Identical to Twilio for US domestic SMS. No cost advantage either way. |
| **10DLC maturity** | Low (negative) | Twilio's 10DLC tooling is more mature. However, RVS will start with toll-free numbers (10DLC not needed at MVP scale). |

### 2.3 When to Reconsider Twilio

Twilio would be the better choice if any of the following become requirements:

1. **MMS support** — If dealers need to send photos or media via text (e.g., "Here is a photo of the damage we found"), Twilio supports MMS; ACS does not. Workaround: Send a link to a Blob Storage SAS URL in the SMS body.
2. **Multi-channel conversations** — If RVS builds a unified inbox combining SMS, WhatsApp, and Facebook Messenger, Twilio Conversations provides this out of the box. ACS would require manual integration of each channel.
3. **Local phone numbers** — If dealers require a local area code number for SMS (not toll-free), Twilio supports local numbers; ACS does not offer local numbers for SMS.
4. **Advanced 10DLC requirements** — If high-volume 10DLC campaigns with per-brand registration become critical, Twilio's mature TCR integration is an advantage.

### 2.4 Migration Risk Assessment

The `INotificationService` abstraction in `RVS.Domain/Integrations/` already decouples the notification provider from the business logic. Migrating from ACS to Twilio (or vice versa) requires only a new `ISmsNotificationService` implementation — no service or controller changes. This is the same pattern used for `ICategorizationService` (Azure OpenAI / rule-based fallback) and `ISpeechToTextService` (Azure Speech / mock). **Provider lock-in risk is low.**

---

## 3. Architecture Design

### 3.1 Notification Service Evolution

The current `INotificationService` is email-only. SMS requires a new interface to maintain single-responsibility and allow independent channel configuration.

```
INotificationService (existing — email)
+-- SendGridNotificationService (production)
+-- NoOpNotificationService (development)

ISmsNotificationService (new — SMS)
+-- AcsSmsNotificationService (production — Azure Communication Services)
+-- NoOpSmsNotificationService (development)

INotificationOrchestrator (new — channel routing)
+-- Decides email vs SMS vs both based on customer preference and tenant config
+-- Injects INotificationService + ISmsNotificationService
+-- Called by IntakeOrchestrationService and dealer messaging endpoints
```

**Key design decisions:**

- **Do not merge SMS into `INotificationService`** — Email and SMS have different delivery semantics (email is fire-and-forget; SMS requires delivery receipts, opt-out compliance, and rate limiting).
- **`INotificationOrchestrator`** is the single entry point for all notification dispatch. It reads the customer's notification preference (`email`, `sms`, `both`) and routes accordingly.
- **Tenant-level SMS opt-in** — SMS is a paid add-on feature. `TenantConfig` gains a `SmsConfig` section that controls whether SMS is enabled for a tenant and which ACS resource/number to use.

### 3.2 Domain Interface: ISmsNotificationService

```csharp
// RVS.Domain/Integrations/ISmsNotificationService.cs
namespace RVS.Domain.Integrations;

public interface ISmsNotificationService
{
    Task SendSmsAsync(string toPhoneNumber, string message, CancellationToken ct = default);

    Task SendMagicLinkSmsAsync(string toPhoneNumber, string magicLinkUrl, CancellationToken ct = default);

    Task SendServiceRequestConfirmationSmsAsync(
        string toPhoneNumber, string serviceRequestId, string dealershipName,
        CancellationToken ct = default);

    Task SendStatusChangeSmsAsync(
        string toPhoneNumber, string serviceRequestId, string newStatus,
        CancellationToken ct = default);

    Task SendDealerMessageSmsAsync(
        string toPhoneNumber, string serviceRequestId, string dealershipName,
        string messageText, CancellationToken ct = default);
}
```

### 3.3 Dealer-to-Customer Messaging Flow

```
Manager App                    API                         ACS                Customer
    |                           |                           |                    |
    | POST /api/dealers/{id}/   |                           |                    |
    |   service-requests/{srId}/|                           |                    |
    |   messages                |                           |                    |
    | ------------------------->|                           |                    |
    |                           | 1. Validate tenant+SR     |                    |
    |                           | 2. Create MessageRecord   |                    |
    |                           |    (Cosmos, embedded in SR)|                   |
    |                           | 3. Send SMS via ACS       |                    |
    |                           | ------------------------->|                    |
    |                           |                           | SMS delivered      |
    |                           |                           | ------------------>|
    |                           |                           |                    |
    |                           |                 Customer replies via SMS       |
    |                           |                           | <------------------|
    |                           | Event Grid webhook        |                    |
    |                           | <-------------------------|                    |
    |                           | 4. Match reply to SR      |                    |
    |                           | 5. Append to MessageRecord|                    |
    |                           |                           |                    |
    | GET messages (polling)    |                           |                    |
    | ------------------------->|                           |                    |
    | <-------------------------|                           |                    |
    | (shows threaded convo)    |                           |                    |
```

### 3.4 Message Storage Model

Messages are stored as an embedded array in the `ServiceRequest` document, following the existing embedding strategy (`DiagnosticResponseEmbedded`, `ServiceEventEmbedded`).

```json
{
  "id": "sr-001",
  "tenantId": "blue-compass",
  "messages": [
    {
      "id": "msg-001",
      "direction": "outbound",
      "channel": "sms",
      "senderType": "dealer",
      "senderUserId": "user-advisor-01",
      "senderDisplayName": "Sarah (Service Advisor)",
      "recipientPhone": "+18015551234",
      "body": "Hi John, your slide motor part arrived. Can you bring in the RV Thursday or Friday?",
      "sentAtUtc": "2026-04-08T14:30:00Z",
      "deliveryStatus": "delivered",
      "deliveryStatusUpdatedAtUtc": "2026-04-08T14:30:05Z"
    },
    {
      "id": "msg-002",
      "direction": "inbound",
      "channel": "sms",
      "senderType": "customer",
      "senderPhone": "+18015551234",
      "body": "Thursday works! I will be there by 9am.",
      "receivedAtUtc": "2026-04-08T14:35:00Z"
    }
  ]
}
```

**Design rationale:**

- **Embedded in SR** (not a separate container) — Messages are always read in the context of a service request. Embedding avoids a join and keeps the hot-path read at ~1 RU.
- **Array size limit** — Cap at 50 messages per SR. Dealer conversations for a single repair rarely exceed 10-20 messages. If exceeded, truncate oldest messages (or link to a Blob-stored archive).
- **No separate `messages` container** — At RVS scale (hundreds of dealers, not millions of consumers), message volume per SR is low. A separate container adds cross-partition query complexity without benefit.

### 3.5 Inbound SMS Routing

When a customer replies to an SMS, ACS delivers the message via Azure Event Grid. The challenge is routing the reply to the correct service request.

**Routing strategy (recommended):**

1. **Phone number lookup** — Maintain a lightweight mapping of `{customerPhone} -> {mostRecentSrId, tenantId}` in a Cosmos container or Table Storage. Updated each time an outbound SMS is sent.
2. **Fallback: keyword matching** — If the customer includes an SR number in their reply (e.g., "SR-1082: Thursday works"), extract and route directly.
3. **Ambiguity handling** — If a customer has multiple active SRs and the reply cannot be routed, append to the most recent SR and flag for advisor review.

```
Event Grid (ACS SMS Received)
    |
    v
Azure Function or API Endpoint
    |
    +-- Extract sender phone number
    +-- Query phone-to-SR mapping
    |   +-- Match found -> Append message to SR
    |   +-- No match -> Log as unroutable, alert advisor
    +-- Return 200 OK to Event Grid
```

### 3.6 Magic Link via SMS

When a customer provides a phone number during intake and opts for SMS notifications:

1. `IntakeOrchestrationService` Step 7 calls `INotificationOrchestrator` instead of `INotificationService` directly.
2. The orchestrator checks customer preference (stored in `CustomerProfile.NotificationPreference`).
3. If preference includes `sms`, sends magic link via `ISmsNotificationService.SendMagicLinkSmsAsync`.

**Message template:**

```
RV Service Flow: Your service request at {DealershipName} is confirmed.
View status: {MagicLinkUrl}
Reply STOP to opt out.
```

> **Character limit:** SMS messages are limited to 160 characters per segment. Magic link URLs should use a URL shortener or the platform's own short domain (e.g., `rvs.fyi/{token-prefix}`). Multi-segment messages incur 2x cost.

### 3.7 Customer Notification Preference

Add a `notificationPreference` field to `CustomerProfile` and `GlobalCustomerAcct`:

```json
{
  "notificationPreference": "both",
  "phoneNumber": "+18015551234",
  "smsOptInAtUtc": "2026-04-08T12:00:00Z",
  "smsOptOutAtUtc": null
}
```

| Value | Behavior |
|---|---|
| `email` | Email only (default, current behavior) |
| `sms` | SMS only |
| `both` | Email and SMS |

**Intake wizard change:** Add an optional toggle on the contact information step: "Also send me text updates about this service request" with a phone number field. Opt-in is explicit and timestamped for TCPA compliance.

---

## 4. Compliance & Regulatory Requirements

### 4.1 TCPA (Telephone Consumer Protection Act)

The TCPA governs unsolicited text messages in the United States. Non-compliance penalties are **$500-$1,500 per message**.

| Requirement | RVS Implementation |
|---|---|
| **Express written consent** | Customer explicitly opts in during intake wizard (checkbox + phone number). Consent timestamp stored in `CustomerProfile.SmsOptInAtUtc`. |
| **Clear disclosure** | Opt-in text: "By providing your phone number and checking this box, you consent to receive text messages from {DealershipName} via RV Service Flow regarding your service request. Message and data rates may apply. Reply STOP to opt out at any time." |
| **Opt-out mechanism** | Toll-free numbers have built-in STOP/START handling (both Twilio and ACS). On STOP receipt, update `CustomerProfile.SmsOptOutAtUtc` and stop all outbound SMS. |
| **Message frequency disclosure** | "You will receive approximately 2-5 messages per service request." |
| **Transactional vs marketing** | All RVS messages are transactional (service-related). No marketing messages. Transactional messages have more lenient TCPA rules but still require consent. |

### 4.2 10DLC Registration

10DLC (10-digit long code) is the carrier-mandated registration system for A2P (application-to-person) messaging on local phone numbers. **Not required for toll-free numbers**, which is why the MVP recommendation uses toll-free.

If RVS later adopts local numbers or 10DLC:

- **Brand registration** ($4 one-time) — Register "RV Service Flow" as an A2P brand with The Campaign Registry (TCR)
- **Campaign registration** ($15/mo) — Register each message use case (e.g., "Service request notifications")
- **Vetting** — TCR reviews brand reputation; higher trust scores get higher throughput limits

### 4.3 Multi-Tenant Compliance Responsibility

| Responsibility | Owner | Implementation |
|---|---|---|
| Platform-level TCPA compliance (opt-in/out mechanics) | **RVS (platform)** | Built into `ISmsNotificationService` — refuse to send if `SmsOptOutAtUtc` is set |
| Dealer-specific consent language | **Dealer (tenant)** | Configurable consent text in `TenantConfig.SmsConfig.ConsentText` |
| Dealer abuse prevention | **RVS (platform)** | Rate limit outbound SMS per tenant per hour; log all sends with `tenantId` for audit |
| Opt-out synchronization | **RVS (platform)** | ACS STOP handling updates `CustomerProfile` automatically via Event Grid |

---

## 5. Azure Communication Services — Implementation Details

### 5.1 Azure Resource Provisioning

```bicep
// Bicep module: modules/communication-services.bicep
resource acs 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: 'acs-rvs-${environment}'
  location: 'global'  // ACS is a global resource
  properties: {
    dataLocation: 'United States'  // Data residency
  }
}

// Managed Identity role assignment (SMS Contributor)
resource acsRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acs
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '6d236292-7e38-xxxx-xxxx-xxxxxxxxxxxx') // Communication Services SMS Contributor
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
```

**Resource naming:** `acs-rvs-dev`, `acs-rvs-staging`, `acs-rvs-prod` (per `Azure_Resource_Naming_Conventions.md`)

### 5.2 Phone Number Provisioning

Phone numbers are provisioned via the Azure portal or Bicep/ARM. For MVP:

1. Purchase one US toll-free number in the production ACS resource
2. Enable SMS send + receive capabilities
3. Configure Event Grid subscription for inbound SMS

### 5.3 Authentication

ACS supports both connection strings and Azure AD (managed identity). **Use managed identity** to align with the existing Key Vault and Cosmos DB authentication pattern.

```csharp
// Program.cs registration
var acsEndpoint = builder.Configuration["AzureCommunicationServices:Endpoint"];
if (!string.IsNullOrEmpty(acsEndpoint))
{
    builder.Services.AddSingleton(
        new SmsClient(new Uri(acsEndpoint), new DefaultAzureCredential()));
    builder.Services.AddScoped<ISmsNotificationService, AcsSmsNotificationService>();
}
else
{
    builder.Services.AddScoped<ISmsNotificationService, NoOpSmsNotificationService>();
}
```

### 5.4 Outbound SMS Implementation Sketch

```csharp
// RVS.API/Integrations/AcsSmsNotificationService.cs
public sealed class AcsSmsNotificationService : ISmsNotificationService
{
    private readonly SmsClient _smsClient;
    private readonly ILogger<AcsSmsNotificationService> _logger;
    private readonly string _fromNumber; // Toll-free number from config

    public async Task SendSmsAsync(
        string toPhoneNumber, string message, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var response = await _smsClient.SendAsync(
            from: _fromNumber,
            to: toPhoneNumber,
            message: message,
            cancellationToken: ct);

        if (!response.Value.Successful)
        {
            _logger.LogWarning(
                "ACS SMS send failed to {Recipient}: {ErrorMessage}",
                toPhoneNumber, response.Value.ErrorMessage);
        }
    }
}
```

### 5.5 Inbound SMS via Event Grid

```csharp
// Event Grid subscription configuration
// Topic: ACS resource -> Event Type: Microsoft.Communication.SMSReceived
// Endpoint: https://api.rvserviceflow.com/api/webhooks/sms-received

// RVS.API/Controllers/WebhooksController.cs
[HttpPost("webhooks/sms-received")]
[AllowAnonymous] // Event Grid validation; secured by Event Grid subscription validation
public async Task<IActionResult> HandleInboundSms(
    [FromBody] EventGridEvent[] events)
{
    foreach (var evt in events)
    {
        if (evt.EventType == "Microsoft.Communication.SMSReceived")
        {
            var data = evt.Data.ToObjectFromJson<SmsReceivedEventData>();
            await _smsRoutingService.RouteInboundSmsAsync(
                data.From, data.To, data.Message, data.ReceivedTimestamp);
        }
    }
    return Ok();
}
```

### 5.6 Configuration

```json
{
  "AzureCommunicationServices": {
    "Endpoint": "https://acs-rvs-prod.communication.azure.com",
    "FromPhoneNumber": "+18005551234",
    "MaxMessagesPerTenantPerHour": 100,
    "MagicLinkBaseUrl": "https://app.rvserviceflow.com/status/"
  }
}
```

---

## 6. Cost Model for RVS

### 6.1 Azure Resource Costs

| Resource | Monthly Cost | Notes |
|---|---|---|
| ACS resource | Free | No base cost for the resource itself |
| Toll-free number (1) | $2.00 | Shared across all tenants in MVP |
| Outbound SMS | $0.0079/msg | Plus carrier surcharges (~$0.003/msg) |
| Inbound SMS | $0.0079/msg | Plus carrier surcharges |
| Event Grid | ~$0.60/million events | Negligible at RVS scale |

### 6.2 Projected Monthly Costs by Growth Phase

| Phase | Tenants | SRs/Month | Outbound SMS | Inbound SMS | Est. Monthly Cost |
|---|---|---|---|---|---|
| **MVP** | 10 | 500 | 2,000 | 500 | **$29** ($2 number + $22 outbound + $5 inbound) |
| **Early Growth** | 50 | 5,000 | 20,000 | 5,000 | **$274** ($2 number + $217 outbound + $55 inbound) |
| **Scale** | 200 | 50,000 | 200,000 | 50,000 | **$2,717** ($2 number + $2,172 outbound + $543 inbound) |
| **Enterprise (per-tenant numbers)** | 200 | 50,000 | 200,000 | 50,000 | **$3,117** ($400 numbers + $2,172 + $543) |

> **Note:** Carrier surcharges (~$0.003/msg) are included in the per-message estimates above. Actual surcharges vary by carrier and may change.

### 6.3 Tenant Cost Attribution

SMS costs should be metered per tenant for cost allocation and billing tier enforcement:

| Metric | Where Tracked | Billing Impact |
|---|---|---|
| `sms_outbound_count` | App Insights custom metric with `tenantId` dimension | Included in plan tier or per-message overage |
| `sms_inbound_count` | App Insights custom metric with `tenantId` dimension | Platform absorbs cost (encourages engagement) |
| `sms_delivery_failure_rate` | App Insights custom metric | Operational monitoring, no billing impact |

**Billing model recommendation:** Include a base SMS allocation in each plan tier:

| Tier | Included SMS/Month | Overage Rate |
|---|---|---|
| Starter | 200 | $0.02/msg |
| Pro | 1,000 | $0.015/msg |
| Enterprise | 5,000 | $0.01/msg |

This aligns with the existing `BillingConfig` metering architecture in `RVS_Billing_Metering_Architecture.md`.

---

## 7. Multi-Tenant Considerations

### 7.1 Tenant-Level SMS Configuration

Add to `TenantConfig`:

```json
{
  "smsConfig": {
    "enabled": false,
    "phoneNumber": null,
    "consentText": "By providing your phone number, you consent to receive text messages...",
    "maxOutboundPerHour": 50,
    "smsAllocationPerMonth": 200,
    "currentMonthSmsCount": 47
  }
}
```

- **`enabled`** — Feature flag per tenant. Defaults to `false`. Enabled when tenant upgrades or opts in.
- **`phoneNumber`** — `null` = use shared platform number. Non-null = tenant-specific number (Phase 2).
- **`consentText`** — Customizable consent language for TCPA compliance.
- **`maxOutboundPerHour`** — Rate limit to prevent noisy neighbor and abuse.

### 7.2 Noisy Neighbor Prevention

| Control | Implementation |
|---|---|
| Per-tenant hourly rate limit | `TenantConfig.SmsConfig.MaxOutboundPerHour` enforced in `AcsSmsNotificationService` |
| Per-tenant monthly allocation | `TenantConfig.SmsConfig.SmsAllocationPerMonth` — soft block at 100%, warning at 80% |
| Global throughput limit | ACS toll-free number has carrier-imposed throughput (~1 msg/sec). Queue outbound messages and process sequentially. |
| Abuse detection | Log all outbound SMS with `tenantId`. Alert on anomalous volume (> 3x average). |

### 7.3 Deployment Stamps Consideration

Per `RVS_Stamp_Scaleout.md`, when RVS scales to multiple deployment stamps:

- **ACS resource is global** — One ACS resource can serve all stamps. No per-stamp ACS provisioning needed.
- **Phone numbers are ACS-level** — The same toll-free number works across stamps.
- **Event Grid routing** — Inbound SMS routes to the correct stamp via the phone-to-SR mapping (which includes `stampId`).
- **Stamp-level rate limits** — Each stamp enforces its own tenant rate limits independently.

---

## 8. Implementation Roadmap

### Phase 1: Foundation (Estimated: 1-2 sprints)

- [ ] Provision ACS resource in dev environment (Bicep module)
- [ ] Purchase one US toll-free number
- [ ] Create `ISmsNotificationService` interface in `RVS.Domain/Integrations/`
- [ ] Implement `AcsSmsNotificationService` and `NoOpSmsNotificationService`
- [ ] Register in `Program.cs` with managed identity auth (ACS endpoint) or no-op (missing config)
- [ ] Add `SendSmsAsync` and `SendMagicLinkSmsAsync` methods
- [ ] Add `SmsConfig` to `TenantConfig` entity
- [ ] Unit tests for service layer (mock `SmsClient`)

### Phase 2: Customer-Facing Notifications (Estimated: 1 sprint)

- [ ] Add notification preference to `CustomerProfile` and `GlobalCustomerAcct`
- [ ] Add phone number + opt-in checkbox to Intake wizard contact step
- [ ] Create `INotificationOrchestrator` to route email/SMS/both
- [ ] Update `IntakeOrchestrationService` Step 7 to use orchestrator
- [ ] Send magic link via SMS when preference includes SMS
- [ ] Send SR confirmation via SMS
- [ ] Send status change alerts via SMS

### Phase 3: Dealer-to-Customer Messaging (Estimated: 2 sprints)

- [ ] Add `MessageEmbedded` array to `ServiceRequest` entity
- [ ] Create `POST /api/dealers/{id}/service-requests/{srId}/messages` endpoint
- [ ] Create `GET /api/dealers/{id}/service-requests/{srId}/messages` endpoint
- [ ] Implement outbound dealer-to-customer SMS via ACS
- [ ] Configure Event Grid subscription for inbound SMS
- [ ] Implement inbound SMS routing (phone-to-SR lookup)
- [ ] Build message thread UI in Manager app (service request detail panel)
- [ ] TCPA compliance: opt-out handling, consent tracking, rate limiting

### Phase 4: Advanced Features (Future)

- [ ] Per-tenant phone numbers (provisioning automation)
- [ ] SMS billing metering integration with `BillingConfig`
- [ ] URL shortener for magic link SMS (character limit optimization)
- [ ] MMS support evaluation (if ACS adds support, or Twilio migration)
- [ ] Conversation analytics (response times, resolution rates)
- [ ] ACS Email evaluation (replace SendGrid for unified notification provider)

---

## 9. Open Questions

| # | Question | Owner | Decision Needed By |
|---|---|---|---|
| 1 | Should SMS be a paid add-on or included in all plan tiers? | Product | Before Phase 2 implementation |
| 2 | Should the MVP use a shared number or per-tenant numbers from day one? | Engineering | Before Phase 1 (recommendation: shared) |
| 3 | What is the URL shortener strategy for magic link SMS? Custom domain or third-party? | Engineering | Before Phase 2 |
| 4 | Should inbound SMS routing use an Azure Function (event-driven) or an API endpoint (webhook)? | Engineering | Before Phase 3 |
| 5 | Should message history be embedded in SR or stored in a separate container? | Engineering | Before Phase 3 (recommendation: embedded) |
| 6 | What is the tenant migration path when upgrading from shared to per-tenant numbers? | Engineering | Before Phase 4 |
| 7 | Should ACS Email replace SendGrid to unify all notifications under one provider? | Engineering | Phase 4+ evaluation |

---

## 10. Summary

**Provider decision:** Azure Communication Services (ACS) — aligned with the all-Azure infrastructure, managed identity authentication, consolidated billing, and native observability.

**Key cost insight:** Per-message SMS costs are identical between Twilio and ACS (~$0.011/msg including carrier surcharges). The decision is driven by operational simplicity, not price.

**Architecture approach:**

- New `ISmsNotificationService` interface (parallel to existing `INotificationService`)
- `INotificationOrchestrator` routes to email, SMS, or both based on customer preference
- Messages embedded in `ServiceRequest` documents (consistent with existing embedding strategy)
- Inbound SMS routed via Event Grid and phone number lookup to SR association
- TCPA compliance built into the platform (opt-in/opt-out, consent tracking, rate limiting)

**MVP cost:** ~$29/month for 10 dealers and 500 SRs. Scales linearly with message volume.

**No code changes in this document.** This is the design spec that precedes implementation.

---

**Document Version:** 1.0
**Last Updated:** April 8, 2026
**Author:** GitHub Copilot (Azure SaaS Architect)
**Status:** Planning — Authoritative Source of Truth (ASOT)
**Cross-References:**

- `RVS_PRD.md` FR-016 (Notifications — "SMS is a future enhancement")
- `RVS_Technical_PRD.md` Section 10.6 (SendGrid Email Notifications)
- `RVS_Consolidated_Architecture.md` Section 7 (Intake Orchestration — Step 7 Notification)
- `RVS_Billing_Metering_Architecture.md` (Metering and cost attribution model)
- `RVS_Azure_Infrastructure_Architecture.md` (Resource topology and Bicep patterns)
- `RVS_MagicLink_Storage_Guidance.md` (Magic link token design)
- `RVS.Domain/Integrations/INotificationService.cs` (Current notification interface)
- `RVS.API/Integrations/SendGridNotificationService.cs` (Current email implementation)
- `RVS_Features_Blazor.Manager.md` (Phase 2 "request additional info" gap)
