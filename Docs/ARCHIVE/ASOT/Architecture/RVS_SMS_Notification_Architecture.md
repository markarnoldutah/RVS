# RVS Unified Notification Architecture — Azure Communication Services (Email + SMS)

**Authoritative Source of Truth (ASOT) — April 9, 2026**
**Status:** Phase 1 Foundation Implemented — ACS Email + SMS services, orchestrator, domain interfaces, message embedding

This document defines the architecture for all transactional notifications in RVS using **Azure Communication Services (ACS)** as the single provider for both **email** and **SMS**. It replaces the previous SendGrid-based email pipeline and covers the SMS expansion. ACS provides a unified Azure-native platform for dealer-to-customer communications, magic link delivery (email or SMS), and inbound message handling.

> **Key Decision (April 9, 2026):** RVS uses ACS for both email and SMS. SendGrid is eliminated. All notification channels are consolidated under a single Azure service with managed identity authentication, unified billing, and native observability. No marketing, reminder, or re-engagement campaigns are supported — all messages are transactional only, which eliminates the IP reputation and deliverability concerns that would otherwise favor a mature email-specific provider like SendGrid.

---

## Context

The PRD (`RVS_PRD.md` FR-016) defines notifications as a core capability covering email confirmations, status updates, and SMS delivery of magic links. The notification architecture is:

- **Interface:** `INotificationService` in `RVS.Domain/Integrations/`
- **Production:** `AcsEmailNotificationService` — transactional email via ACS Email REST API
- **Development:** `NoOpNotificationService` — logs only, no external calls
- **SMS:** `AcsSmsNotificationService` — transactional SMS via ACS SMS SDK (same ACS resource)
- **Orchestration:** `INotificationOrchestrator` routes to email or SMS based on customer either/or preference
- **Trigger:** `IntakeOrchestrationService` fires notifications as fire-and-forget in Step 7

> **Supersedes:** The previous architecture used SendGrid for email. That has been replaced by ACS Email to consolidate all notification channels under a single Azure service. SendGrid is no longer used anywhere in the RVS platform.

### Use Cases for Notifications

1. **Service request confirmation** — Email or text confirmation when an intake is submitted
2. **Magic link delivery** — Customer opts to receive their status page link via email or SMS
3. **Status change alerts** — Notify customer when SR moves to `InProgress`, `Completed`, etc.
4. **Dealer-to-customer messaging** — Service advisor sends a scheduling question, parts update, or follow-up from the Manager app; message is linked to the service request
5. **Customer-to-dealer replies** — Customer replies to dealer texts; replies are captured and linked to the originating service request (inbound SMS)

### Notification Preference: Opt-Out Model

By default both email and SMS notifications are sent. Customers can opt out of either channel during intake:

| SmsOptOut | EmailOptOut | Email | SMS | When |
|---|---|---|---|---|
| `false` | `false` | ✅ | ✅ | Default — both channels active |
| `true` | `false` | ✅ | ❌ | Customer opted out of text messages |
| `false` | `true` | ❌ | ✅ | Customer opted out of email |
| `true` | `true` | ❌ | ❌ | Customer opted out of all notifications |

The opt-out choices are saved on both `CustomerProfile` and `GlobalCustomerAcct` entities. The intake wizard contact step (Step 2) presents two checkboxes: "Do not send text messages" and "Do not send email".

---

## 1. Provider Comparison: Why ACS for Both Email and SMS

### 1.1 Email Provider Comparison: SendGrid vs ACS Email

| Capability | **SendGrid** | **ACS Email** |
|---|---|---|
| **Transactional email** | Mature, high deliverability | GA — reliable for transactional workloads |
| **Marketing campaigns** | Advanced segmentation, A/B testing | Not supported — transactional only |
| **IP reputation management** | Dedicated IPs, IP warming tools | Microsoft-managed shared IP pools |
| **Authentication** | API key (secret rotation required) | **Azure Managed Identity** — no secrets |
| **Observability** | SendGrid dashboard + webhooks | **Azure Monitor / App Insights** — native |
| **Portal integration** | Separate Twilio/SendGrid portal | **Azure portal** — same subscription, same RBAC |
| **Billing** | Separate Twilio account/invoice | **Azure invoice** — consolidated |
| **Domain verification** | SPF + DKIM via SendGrid UI | SPF + DKIM auto-configured on domain verification |
| **DMARC** | Manual DNS record | Manual DNS record (same) |
| **Templates** | Dynamic Templates UI, Handlebars syntax | Code-managed HTML templates |
| **SMS integration** | Via Twilio (separate service) | **Same ACS resource** — unified SDK |
| **.NET SDK** | `SendGrid` NuGet (third-party) | `Azure.Communication.Email` NuGet (first-party Azure SDK) |

> **Key insight for RVS:** The deliverability advantages of SendGrid (dedicated IPs, IP warming, reputation management) are relevant for **marketing and bulk email**. RVS sends only **transactional email** to recipients who are actively expecting it (they just submitted a form). For this use case, ACS Email's Microsoft-managed IP pools deliver reliably (>99% inbox placement) without any IP warming or reputation management overhead.

### 1.2 SMS Provider Comparison: Twilio vs ACS SMS

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

### 1.3 Cost Comparison

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

### 1.4 Multi-Tenant Number Strategy

| Strategy | Pros | Cons | Recommendation |
|---|---|---|---|
| **Shared toll-free number** (all tenants share one number) | Cheapest ($2/mo total), simplest provisioning | No dealer branding, confusing for customers, reply routing complexity | **MVP — Start here** |
| **Per-tenant toll-free number** (each dealer gets their own) | Dealer branding, clear reply routing, customer sees consistent number | $2/mo per tenant, provisioning automation needed | **Phase 2 — Upgrade path** |
| **Per-tenant short code** | Highest throughput, branded | $1,000/mo per tenant, 8-12 week provisioning | Not viable for SMB pricing |

**Recommendation:** Start with a single shared toll-free number. Route inbound replies using message context (the SR ID or dealer slug embedded in outbound messages, or a lookup from the sender's phone number to their most recent SR). Migrate to per-tenant numbers when tenant count justifies the provisioning automation investment.

---

## 2. Recommendation: ACS for Unified Email + SMS

### 2.1 Decision

**Use Azure Communication Services (ACS) for all transactional notifications in RVS — both email and SMS.**

### 2.2 Rationale

| Factor | Weight | ACS Advantage |
|---|---|---|
| **Azure ecosystem alignment** | High | RVS is 100% Azure (App Service, Cosmos DB, Blob Storage, Key Vault, App Insights). ACS is a first-party Azure service with native integration. |
| **Managed Identity authentication** | High | No API keys or secrets to rotate for either email or SMS. ACS uses the same managed identity that already authenticates to Cosmos DB and Key Vault. Zero additional secret management. Eliminates the `sendgrid-api-key` secret from Key Vault. |
| **Single provider for email + SMS** | High | One ACS resource, one SDK, one authentication model, one billing line. No separate SendGrid account + Twilio/ACS account. |
| **Consolidated billing** | Medium | Email and SMS costs appear on the same Azure invoice as Cosmos DB, App Service, and Blob Storage. No separate vendor accounts or invoices. |
| **Observability** | High | ACS emits diagnostic logs to Azure Monitor for both email and SMS. Delivery events flow to the same App Insights workspace with `tenantId` dimensions. No webhook infrastructure to build for email delivery tracking. |
| **Transactional-only workload** | High | RVS sends zero marketing, reminder, or re-engagement emails. For 1:1 transactional email to engaged recipients, ACS Email's Microsoft-managed IP pools deliver reliably (>99% inbox placement). The IP reputation advantages of SendGrid are irrelevant for this use case. |
| **Inbound SMS via Event Grid** | Medium | Event Grid subscriptions route inbound SMS to the same App Service (or an Azure Function) without exposing a public webhook endpoint. |
| **Per-message cost** | Low | Identical to SendGrid for email and identical to Twilio for SMS. No cost advantage either way. |
| **10DLC maturity** | Low (negative) | Twilio's 10DLC tooling is more mature. However, RVS will start with toll-free numbers (10DLC not needed at MVP scale). |

### 2.3 ACS Email — Deliverability for Transactional Email

For RVS's transactional-only email workload, ACS Email provides reliable delivery because:

- **Recipient expectation is high** — the customer just submitted an intake form and is actively waiting for the confirmation email. Spam complaint rate is effectively 0%.
- **No IP warming needed** — transactional email to engaged recipients delivers reliably even on shared IP pools. ACS Email handles this automatically.
- **Automatic SPF + DKIM** — ACS Email configures SPF and DKIM automatically when the sending domain is verified. A DMARC TXT record on DNS completes the authentication trifecta.
- **Low volume, steady cadence** — RVS sends tens to low hundreds of emails per day per dealer. No burst patterns that trigger ISP suspicion.
- **Exempt from CAN-SPAM unsubscribe** — transactional email is exempt from unsubscribe requirements.

**Setup requirements:**
1. Verify sending domain in ACS (e.g., `notifications.rvserviceflow.com`) — sets up SPF/DKIM automatically
2. Add DMARC TXT record on DNS (e.g., `v=DMARC1; p=quarantine; rua=mailto:dmarc@rvserviceflow.com`)
3. Use `noreply@notifications.rvserviceflow.com` as the From address

### 2.4 When to Reconsider

Twilio + SendGrid would be the better choice if any of the following become requirements:

1. **Marketing/bulk email** — If RVS ever adds marketing campaigns, newsletters, or re-engagement emails, SendGrid's dedicated IPs, IP warming, and engagement analytics would be necessary. **This is explicitly out of scope.**
2. **MMS support** — If dealers need to send photos or media via text (e.g., "Here is a photo of the damage we found"), Twilio supports MMS; ACS does not. Workaround: Send a link to a Blob Storage SAS URL in the SMS body.
3. **Multi-channel conversations** — If RVS builds a unified inbox combining SMS, WhatsApp, and Facebook Messenger, Twilio Conversations provides this out of the box. ACS would require manual integration of each channel.
4. **Local phone numbers** — If dealers require a local area code number for SMS (not toll-free), Twilio supports local numbers; ACS does not offer local numbers for SMS.
5. **Advanced 10DLC requirements** — If high-volume 10DLC campaigns with per-brand registration become critical, Twilio's mature TCR integration is an advantage.

### 2.5 Migration Risk Assessment

The `INotificationService` abstraction in `RVS.Domain/Integrations/` already decouples the notification provider from the business logic. Replacing `SendGridNotificationService` with `AcsEmailNotificationService` requires only a new implementation — no service or controller changes. Similarly, `ISmsNotificationService` is provider-agnostic. This is the same pattern used for `ICategorizationService` (Azure OpenAI / rule-based fallback) and `ISpeechToTextService` (Azure Speech / mock). **Provider lock-in risk is low.**

---

## 3. Architecture Design

### 3.1 Notification Service Architecture

Both email and SMS are served by a single ACS resource. Each channel has its own interface to maintain single-responsibility and allow independent configuration.

```
INotificationService (email — updated from SendGrid to ACS Email)
+-- AcsEmailNotificationService (production — Azure Communication Services Email)
+-- NoOpNotificationService (development)

ISmsNotificationService (SMS — Azure Communication Services SMS)
+-- AcsSmsNotificationService (production — Azure Communication Services SMS)
+-- NoOpSmsNotificationService (development)

INotificationOrchestrator (channel routing)
+-- Decides email or SMS based on customer preference and tenant config
+-- Injects INotificationService + ISmsNotificationService
+-- Called by IntakeOrchestrationService and dealer messaging endpoints
```

**Key design decisions:**

- **Separate interfaces for email and SMS** — Email and SMS have different delivery semantics (email supports HTML templates; SMS requires delivery receipts, opt-out compliance, character limits, and rate limiting).
- **Single ACS resource** — Both `AcsEmailNotificationService` and `AcsSmsNotificationService` authenticate to the same ACS resource via managed identity. One resource, one billing line, one set of diagnostic logs.
- **`INotificationOrchestrator`** is the single entry point for all notification dispatch. It reads the customer's notification preference (`email`, `sms`) and routes accordingly.
- **Tenant-level SMS opt-in** — SMS is a paid add-on feature. `TenantConfig` gains a `SmsConfig` section that controls whether SMS is enabled for a tenant and which ACS resource/number to use. Email is always enabled.

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
  "notificationPreference": "sms",
  "phoneNumber": "+18015551234",
  "smsOptInAtUtc": "2026-04-08T12:00:00Z",
  "smsOptOutAtUtc": null
}
```

| Value | Behavior |
|---|---|
| `email` | Email only (default, current behavior) |
| `sms` | SMS only |

**Intake wizard change:** On the contact information step, the customer selects either email or SMS as their single notification channel. Selecting SMS reveals a phone number field; the opt-in is explicit and timestamped for TCPA compliance.

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

// ACS Email domain verification (for transactional email)
resource acsEmailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: 'acs-rvs-email-${environment}'
  location: 'global'
  properties: {
    dataLocation: 'United States'
  }
}

// Managed Identity role assignment (Contributor — covers both Email and SMS)
resource acsRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acs
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'b24988ac-6180-42a0-ab88-20f7382dd24c') // Contributor
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

ACS supports both connection strings and Azure AD (managed identity). **Use managed identity** to align with the existing Key Vault and Cosmos DB authentication pattern. Both email and SMS clients share the same ACS endpoint.

```csharp
// Program.cs registration — unified ACS endpoint for email + SMS
var acsEndpoint = builder.Configuration["AzureCommunicationServices:Endpoint"];
if (!string.IsNullOrEmpty(acsEndpoint))
{
    var credential = new DefaultAzureCredential();
    var acsUri = new Uri(acsEndpoint);

    // Email (replaces SendGrid)
    builder.Services.AddSingleton(new EmailClient(acsUri, credential));
    builder.Services.AddScoped<INotificationService, AcsEmailNotificationService>();

    // SMS
    builder.Services.AddSingleton(new SmsClient(acsUri, credential));
    builder.Services.AddScoped<ISmsNotificationService, AcsSmsNotificationService>();
}
else
{
    builder.Services.AddSingleton<INotificationService, NoOpNotificationService>();
    builder.Services.AddScoped<ISmsNotificationService, NoOpSmsNotificationService>();
}
```

> **Note:** This replaces the previous SendGrid `HttpClient` registration. The `sendgrid-api-key` secret in Key Vault is no longer needed and can be removed.

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
    "Email": {
      "FromAddress": "noreply@notifications.rvserviceflow.com",
      "SenderDisplayName": "RV Service Flow"
    },
    "Sms": {
      "FromPhoneNumber": "+18005551234",
      "MaxMessagesPerTenantPerHour": 100
    },
    "MagicLinkBaseUrl": "https://rvintake.com/status/"
  }
}
```

> **Note:** The previous `SendGrid` configuration section (`SendGrid:ApiKey`, `SendGrid:FromEmail`) is removed. All notification config is now under `AzureCommunicationServices`.

---

## 6. Cost Model for RVS

### 6.1 Azure Resource Costs

| Resource | Monthly Cost | Notes |
|---|---|---|
| ACS resource | Free | No base cost for the resource itself |
| ACS Email | $0.00025/email | First 1,000 emails/month free |
| Toll-free number (1) | $2.00 | Shared across all tenants in MVP |
| Outbound SMS | $0.0079/msg | Plus carrier surcharges (~$0.003/msg) |
| Inbound SMS | $0.0079/msg | Plus carrier surcharges |
| Event Grid | ~$0.60/million events | Negligible at RVS scale |

### 6.2 Projected Monthly Costs by Growth Phase (Email + SMS Combined)

| Phase | Tenants | SRs/Month | Emails | Outbound SMS | Inbound SMS | Est. Monthly Cost |
|---|---|---|---|---|---|---|
| **MVP** | 10 | 500 | 1,500 | 2,000 | 500 | **$29** ($2 number + $0.13 email + $22 SMS out + $5 SMS in) |
| **Early Growth** | 50 | 5,000 | 15,000 | 20,000 | 5,000 | **$278** ($2 number + $3.50 email + $217 SMS out + $55 SMS in) |
| **Scale** | 200 | 50,000 | 150,000 | 200,000 | 50,000 | **$2,754** ($2 number + $37 email + $2,172 SMS out + $543 SMS in) |
| **Enterprise (per-tenant numbers)** | 200 | 50,000 | 150,000 | 200,000 | 50,000 | **$3,154** ($400 numbers + $37 email + $2,172 + $543) |

> **Note:** Email volume estimates 3 emails per SR lifecycle: confirmation, in-progress update, completion notification. SMS carrier surcharges (~$0.003/msg) are included. Email cost is negligible compared to SMS — the first 1,000 emails/month are free.

### 6.3 Tenant Cost Attribution

SMS and email costs should be metered per tenant for cost allocation and billing tier enforcement:

| Metric | Where Tracked | Billing Impact |
|---|---|---|
| `email_outbound_count` | App Insights custom metric with `tenantId` dimension | Included in plan tier (email is always enabled) |
| `sms_outbound_count` | App Insights custom metric with `tenantId` dimension | Included in plan tier or per-message overage |
| `sms_inbound_count` | App Insights custom metric with `tenantId` dimension | Platform absorbs cost (encourages engagement) |
| `notification_delivery_failure_rate` | App Insights custom metric | Operational monitoring, no billing impact |

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

### Phase 1: Foundation — ACS Email + SMS (Estimated: 1-2 sprints)

- [ ] Provision ACS resource in dev environment (Bicep module) — includes Email and SMS capabilities
- [ ] Verify sending domain for ACS Email (`notifications.rvserviceflow.com`); add DMARC DNS record
- [ ] Purchase one US toll-free number for SMS
- [ ] Implement `AcsEmailNotificationService` (replaces `SendGridNotificationService`)
- [ ] Create `ISmsNotificationService` interface in `RVS.Domain/Integrations/`
- [ ] Implement `AcsSmsNotificationService` and `NoOpSmsNotificationService`
- [ ] Register both services in `Program.cs` with managed identity auth (ACS endpoint) or no-op (missing config)
- [ ] Remove `SendGridNotificationService`, `sendgrid-api-key` from Key Vault, and `SendGrid` config section
- [ ] Add `SmsConfig` to `TenantConfig` entity
- [ ] Unit tests for both email and SMS service layers (mock `EmailClient` and `SmsClient`)

### Phase 2: Customer-Facing Notifications with Either/Or Channel Choice (Estimated: 1 sprint)

- [ ] Add notification preference (`email`, `sms`) to `CustomerProfile` and `GlobalCustomerAcct`
- [ ] Add phone number + either/or channel selector to Intake wizard contact step (email is default; selecting SMS shows phone field)
- [ ] Create `INotificationOrchestrator` to route to email or SMS based on customer preference
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
- [ ] MMS support evaluation (if ACS adds support, or Twilio migration for SMS only)
- [ ] Conversation analytics (response times, resolution rates)
- [ ] ACS Email styled HTML templates (replace plain-text MVP templates)

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
| 7 | ~~Should ACS Email replace SendGrid to unify all notifications under one provider?~~ **Resolved:** Yes. ACS Email replaces SendGrid starting Phase 1. Transactional-only workload does not require SendGrid's IP reputation features. | Engineering | Resolved |

---

## 10. Summary

**Provider decision:** Azure Communication Services (ACS) for both email and SMS — aligned with the all-Azure infrastructure, managed identity authentication, consolidated billing, and native observability. SendGrid is eliminated.

**Key cost insight:** Per-message costs are identical between SendGrid and ACS Email, and identical between Twilio and ACS SMS. The decision is driven by operational simplicity, unified provider, and the fact that RVS's transactional-only workload does not benefit from SendGrid's deliverability features.

**Architecture approach:**

- `INotificationService` implementation replaced: `SendGridNotificationService` → `AcsEmailNotificationService`
- New `ISmsNotificationService` interface for SMS channel
- `INotificationOrchestrator` routes to email or SMS based on customer either/or preference
- Messages embedded in `ServiceRequest` documents (consistent with existing embedding strategy)
- Inbound SMS routed via Event Grid and phone number lookup to SR association
- TCPA compliance built into the platform (opt-in/opt-out, consent tracking, rate limiting)
- Single ACS resource serves both email and SMS; one managed identity, one billing line

**MVP cost:** ~$29/month for 10 dealers and 500 SRs (email cost is negligible; SMS is the primary cost driver). Scales linearly with message volume.

**No code changes in this document.** This is the design spec that precedes implementation.

---

**Document Version:** 2.0
**Last Updated:** April 9, 2026
**Author:** GitHub Copilot (Azure SaaS Architect)
**Status:** Planning — Authoritative Source of Truth (ASOT)
**Cross-References:**

- `RVS_PRD.md` FR-016 (Notifications — email or SMS with customer either/or choice)
- `RVS_Technical_PRD.md` Section 10.6 (ACS Email Notifications — replaces SendGrid)
- `RVS_Consolidated_Architecture.md` Section 7 (Intake Orchestration — Step 7 Notification)
- `RVS_Billing_Metering_Architecture.md` (Metering and cost attribution model)
- `RVS_Azure_Infrastructure_Architecture.md` (Resource topology and Bicep patterns)
- `RVS_MagicLink_Storage_Guidance.md` (Magic link token design)
- `RVS.Domain/Integrations/INotificationService.cs` (Email notification interface)
- `RVS.API/Integrations/AcsEmailNotificationService.cs` (ACS Email implementation — replaces SendGridNotificationService)
- `RVS_Features_Blazor.Manager.md` (Phase 2 "request additional info" gap)
