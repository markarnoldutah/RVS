# RVS Billing & Metering Architecture

**Authoritative Source of Truth (ASOT) — April 5, 2026**
**Status:** Design Guidance — No Code Changes Yet

This document defines the billing and metering architecture for RVS. It answers the questions raised as a P1 business gap in `RVS_Cloud_Arch_Assessment.md` and `RVS_SaaS_Architecture_Assessment.md`. No code changes are made by this document — it is the design spec that precedes implementation.

---

## Context

The PRD targets $199–$499/month tiered subscriptions. The current architecture has no mechanism to:

- Count usage per tenant
- Enforce plan limits
- Emit billing events to a payment processor
- Handle subscription lifecycle (trial → paid → cancelled)

The SaaS WAF principle *"Understand how your costs and revenue are related"* identifies unlimited usage with flat billing as a critical business risk. This document resolves that gap.

---

## 1. Plan Tiers

| Tier | Price | Service Requests / Month | Locations | AI Categorization | SFTP Export | Support |
|---|---|---|---|---|---|---|
| **Starter** | $199/mo | 500 | 1 | Included | Not included | Email (48h) |
| **Pro** | $349/mo | 2,000 | 5 | Included | Included | Email (24h) |
| **Enterprise** | $499/mo | Unlimited | Unlimited | Included | Included | Priority (4h) |

**Design decisions:**

- Soft limits (warning at 80%, block at 100% for Starter/Pro) vs. hard cutoff. Recommendation: soft block with 402 response and upgrade prompt.
- Enterprise is truly unlimited — do not implement a counter check for `Enterprise` tenants.
- All tiers include the intake portal, dealer dashboard, and asset ledger.

---

## 2. Metrics to Track

These are the billable and observable dimensions for RVS. All metrics should carry `tenantId` as a primary dimension.

### 2.1 Primary Billable Metric — Monthly Service Request Volume

| Metric | Description | Where Counted |
|---|---|---|
| `sr_created_count` | Number of `ServiceRequest` documents created this billing period | Increment on intake write (Cosmos change feed or atomic counter) |
| `sr_created_month` | Calendar month bucket (`2026-04`) | Derived from `CreatedAtUtc` |
| `tenant_plan_tier` | `Starter` / `Pro` / `Enterprise` | Read from `TenantConfig.BillingConfig.PlanTier` |
| `tenant_plan_limit` | Max SRs allowed per month for tier | Derived from plan tier |

**Why SR count and not location count?** Service requests are the primary value unit customers understand. Location count as a billable dimension creates upgrade friction for multi-location operators expanding gradually.

### 2.2 Secondary Observable Metrics (Non-Billable, Operational)

| Metric | Description | Purpose |
|---|---|---|
| `ai_categorization_calls` | Calls to Azure OpenAI per tenant per day | Cost attribution and budget alerting |
| `ai_categorization_fallback_rate` | Percentage of intakes that fell back to rule-based categorization | AI health monitoring |
| `attachment_storage_bytes` | Total blob bytes per tenant | Tier-based lifecycle policy enforcement |
| `api_request_count` | API calls per tenant per minute | Noisy neighbor detection, rate limit enforcement |
| `sftp_export_success` / `sftp_export_failure` | SFTP export outcomes per tenant | SLA monitoring |
| `magic_link_sends` | Email notification sends per tenant | Notification cost attribution |
| `cosmos_ru_consumed` | RU consumption attributed to tenant | Internal cost allocation |

### 2.3 Application Insights Custom Events

Every metric above should be emitted as an Application Insights `CustomEvent` or `CustomMetric` with at minimum these dimensions:

```json
{
  "tenantId": "blue-compass",
  "locationId": "slc-01",           // where applicable
  "operationType": "IntakeSubmit",
  "planTier": "Pro",
  "stampId": "stamp-01"
}
```

The `TelemetryInitializer` (to be designed separately in the Application Insights architecture doc) stamps `tenantId` and `planTier` on every request automatically.

---

## 3. Where Usage Is Stored

### 3.1 TenantConfig Extensions (Billing Fields)

`TenantConfig` requires the following new embedded object. **No code changes in this document** — this is the design spec only.

```json
{
  "billingConfig": {
    "planTier": "Pro",
    "stripeCustomerId": "cus_abc123",
    "stripeSubscriptionId": "sub_xyz789",
    "billingPeriodStartDay": 1,
    "currentPeriodSrCount": 347,
    "currentPeriodStart": "2026-04-01T00:00:00Z",
    "trialEndsAtUtc": null,
    "maxMonthlyServiceRequests": 2000,
    "maxLocations": 5,
    "isSftpEnabled": true,
    "attachmentRetentionDays": 365
  }
}
```

| Field | Type | Description |
|---|---|---|
| `planTier` | `string` | `Starter`, `Pro`, or `Enterprise` |
| `stripeCustomerId` | `string` | Stripe Customer object ID for webhook correlation |
| `stripeSubscriptionId` | `string` | Stripe Subscription object ID |
| `billingPeriodStartDay` | `int` | Day of month the billing period resets (default: 1) |
| `currentPeriodSrCount` | `int` | Atomic counter — incremented on each SR creation |
| `currentPeriodStart` | `DateTimeOffset` | Start of current billing window |
| `trialEndsAtUtc` | `DateTimeOffset?` | Non-null during free trial; null when paid |
| `maxMonthlyServiceRequests` | `int` | Derived from plan tier; stored for fast limit checks |
| `maxLocations` | `int` | Max allowed active locations for tier |
| `isSftpEnabled` | `bool` | Feature flag derived from plan tier |
| `attachmentRetentionDays` | `int` | Blob lifecycle policy duration (Starter: 180, Pro: 365, Enterprise: 730) |

### 3.2 Counter Strategy — Atomic Increment in Cosmos

**Option A — Atomic counter in `TenantConfig`:** On each SR creation, issue a Cosmos DB patch operation: `IncrementAsync("/billingConfig/currentPeriodSrCount", 1)`. This is O(1) RU and avoids an aggregation query at enforcement time. The `currentPeriodStart` field controls when to reset the counter.

**Option B — Cosmos change feed aggregate job:** An Azure Function on a change feed reads new `ServiceRequest` documents and upserts a daily aggregate document (partitioned by `tenantId + date`). Monthly count is the sum of the 30 daily documents.

**Recommendation: Option A** for MVP. Atomic patch operations in Cosmos are cheap (~1 RU) and correct. Option B adds infrastructure complexity (Function + change feed processor) and introduces eventual consistency in the counter. Option A is consistent and requires no additional Azure resources.

**Counter reset:** A daily Azure Function (timer trigger, runs at 00:01 UTC on `billingPeriodStartDay`) resets `currentPeriodSrCount` to 0 and updates `currentPeriodStart`. This is the only place where the counter is zeroed.

---

## 4. Where Enforcement Happens

### 4.1 Intake Endpoint — Service Request Creation Limit

**Location:** `IntakeOrchestrationService.SubmitIntakeAsync` (before Step 3 — SR write).

**Logic:**

```
if tenant.BillingConfig.PlanTier != Enterprise
   AND tenant.BillingConfig.currentPeriodSrCount >= tenant.BillingConfig.maxMonthlyServiceRequests
THEN throw PlanLimitExceededException (→ HTTP 402)
```

The intake response body on 402 should include:

```json
{
  "message": "This dealership has reached its monthly service request limit. Please contact your service manager.",
  "errorId": "<guid>"
}
```

Do not expose the plan tier name, limit count, or upgrade URL to anonymous customers — the intake portal is customer-facing.

**Soft warning:** At 80% of limit, emit a `PlanLimitWarning` custom event to Application Insights. The dealer dashboard (Blazor.Manager) can poll a `GET /api/tenants/billing/usage` endpoint to show a banner.

### 4.2 Location Creation Limit

**Location:** `LocationService.CreateAsync` (authenticated dealer endpoint).

**Logic:**

```
if tenant.BillingConfig.PlanTier != Enterprise
   AND active_location_count >= tenant.BillingConfig.maxLocations
THEN throw PlanLimitExceededException (→ HTTP 402)
```

Response body should include an upgrade prompt since this is a dealer-facing (authenticated) endpoint:

```json
{
  "message": "Your plan allows a maximum of 1 location. Upgrade to Pro or Enterprise to add more locations.",
  "errorId": "<guid>"
}
```

### 4.3 SFTP Feature Flag

**Location:** `SftpExportService` (when implemented).

**Logic:** Check `TenantConfig.BillingConfig.IsSftpEnabled` before scheduling an export. Return a structured error if not enabled. No enforcement at the middleware level — this is a feature flag, not a usage limit.

### 4.4 TenantAccessGateMiddleware — Trial Expiry

When `trialEndsAtUtc` is non-null and `DateTimeOffset.UtcNow > trialEndsAtUtc` and `stripeSubscriptionId` is null, the gateway returns 402 with a trial-expired message. This is the only scenario where the access gate returns 402 (vs. 403 for `IsActive = false`).

**Note:** Do not add plan limit enforcement to `TenantAccessGateMiddleware` itself. The middleware runs on every request and should not perform RU-consuming counter reads. Limit enforcement belongs in the service layer at the point of resource creation.

---

## 5. Stripe Integration Design

### 5.1 Stripe Objects

| Stripe Object | RVS Mapping |
|---|---|
| **Customer** | One per `tenantId`. Created on tenant provisioning. |
| **Product** | One per tier (`RVS Starter`, `RVS Pro`, `RVS Enterprise`). Created once, reused. |
| **Price** | One per Product (monthly recurring). Created once, reused. |
| **Subscription** | One per Customer. References Product Price. |
| **Webhook** | Stripe → `POST /api/billing/stripe/webhook` |

### 5.2 Tenant Provisioning Flow (Billing Bootstrap)

When a new tenant is provisioned (`POST /api/admin/tenants`):

1. Create Stripe Customer (`name: tenantName, email: adminEmail, metadata: { tenantId }`)
2. Create Stripe Subscription for selected tier, with trial period if applicable
3. Store `stripeCustomerId` and `stripeSubscriptionId` in `TenantConfig.BillingConfig`
4. Set `trialEndsAtUtc` to 30 days from now (configurable)
5. Set `currentPeriodSrCount = 0`, `currentPeriodStart = UtcNow`

### 5.3 Stripe Webhook Events

The webhook endpoint `POST /api/billing/stripe/webhook` handles:

| Event | Action |
|---|---|
| `invoice.payment_succeeded` | Reset `currentPeriodStart` for usage-based billing. Log payment. |
| `invoice.payment_failed` | Emit Application Insights alert. Send internal notification to platform admin. Do not immediately disable tenant — allow 3-day grace period. |
| `customer.subscription.updated` | Update `planTier`, `maxMonthlyServiceRequests`, `maxLocations`, `isSftpEnabled` in `TenantConfig`. |
| `customer.subscription.deleted` | Set `TenantConfig.AccessGate.LoginsEnabled = false`, `DisabledReason = "Canceled"`. |
| `customer.subscription.trial_will_end` | Emit Application Insights event. Trigger in-app notification to tenant admin. |

**Security:** Validate every webhook with Stripe's webhook signature (`Stripe-Signature` header). Reject without processing if signature fails. Use a dedicated Stripe webhook secret stored in Azure Key Vault (not `appsettings.json`).

### 5.4 Interface Design (No Code Yet)

```csharp
// Domain/Interfaces/IStripeService.cs (to be created)
public interface IStripeService
{
    Task<string> CreateCustomerAsync(string tenantId, string tenantName, string adminEmail, CancellationToken ct = default);
    Task<string> CreateSubscriptionAsync(string stripeCustomerId, string planTier, int trialDays, CancellationToken ct = default);
    Task UpdateSubscriptionPlanAsync(string stripeSubscriptionId, string newPlanTier, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);
}
```

```csharp
// API/Controllers/BillingController.cs (to be created)
// POST /api/billing/stripe/webhook   [AllowAnonymous] — Stripe signs the payload
// GET  /api/tenants/billing/usage    [Authorize] — Current period SR count and limit for dashboard
```

---

## 6. Free Trial Design

- Default trial: 30 days from tenant provisioning, no credit card required
- Trial meter: Full SR tracking applies during trial (counts against `maxMonthlyServiceRequests`). Starter limits apply during trial.
- Trial-to-paid: When customer enters card in Stripe Customer Portal, subscription activates immediately; `trialEndsAtUtc` is cleared.
- Trial expired: `TenantAccessGateMiddleware` returns 402. Tenant admin sees upgrade prompt. Intake portal shows friendly error (not the internal 402 body).
- Platform admin override: `TenantConfig.BillingConfig.trialEndsAtUtc` can be manually extended. This is the "sales concession" lever.

---

## 7. Dealer Dashboard — Usage Visibility

A new API endpoint `GET /api/tenants/billing/usage` returns the current billing status for the authenticated tenant. Blazor.Manager renders a usage banner on the dashboard when approaching limits.

**Response DTO (to be designed):**

```json
{
  "planTier": "Pro",
  "currentPeriodSrCount": 1680,
  "maxMonthlyServiceRequests": 2000,
  "usagePercent": 84,
  "currentPeriodStart": "2026-04-01T00:00:00Z",
  "trialEndsAtUtc": null,
  "isTrialActive": false,
  "locationCount": 3,
  "maxLocations": 5
}
```

**Banner behavior in Blazor.Manager:**

| Condition | Banner Type | Message |
|---|---|---|
| `usagePercent >= 80 && < 100` | Warning | "You've used 84% of your monthly service requests. Upgrade to avoid interruption." |
| `usagePercent >= 100` | Error | "Monthly limit reached. New service requests are paused until {nextResetDate}." |
| `isTrialActive && trialEndsAtUtc within 7 days` | Info | "Your free trial ends in 5 days. Add a payment method to continue." |
| Trial expired | Error | Tenant access gated — redirect to upgrade page |

---

## 8. Implementation Priority and Sequencing

This is guidance only. Actual implementation sequencing is the developer's decision.

| Step | Task | Effort | Risk |
|---|---|---|---|
| 1 | Add `BillingConfig` embedded object to `TenantConfig` entity | Low | Low |
| 2 | Add atomic SR counter increment in `IntakeOrchestrationService` | Low | Low |
| 3 | Add plan limit enforcement in `IntakeOrchestrationService` | Low | Medium |
| 4 | Add `GET /api/tenants/billing/usage` endpoint | Low | Low |
| 5 | Add trial expiry check in `TenantAccessGateMiddleware` | Low | Low |
| 6 | Stripe account setup and Product/Price object creation | Low | Low |
| 7 | `IStripeService` implementation and tenant provisioning integration | Medium | Medium |
| 8 | Stripe webhook endpoint with signature validation | Medium | Medium |
| 9 | Billing usage banner in Blazor.Manager | Low | Low |
| 10 | Counter reset Azure Function | Medium | Low |

**Prerequisite for Steps 6–8:** Azure Key Vault must be provisioned and wired (separate IaC work stream).

---

## 9. Open Questions

The following questions require a product decision before implementation:

1. **Overage policy:** Does Starter/Pro block at 100% or allow overage billed at a per-SR rate? Current recommendation is hard block with upgrade prompt.
2. **Annual pricing:** Is there a discount for annual commitment? If yes, the `billingPeriodStartDay` approach must be extended to support annual windows.
3. **Multiple subscriptions per tenant:** Could Blue Compass (one tenant, many locations) have different plan tiers per location? Recommendation: No — plan tier is per corporation (tenant), not per location. This is a core tenancy model decision.
4. **Stripe Customer Portal:** Will tenants self-serve plan upgrades via Stripe's hosted portal? If yes, `customer.subscription.updated` webhook is the only integration needed. If no, RVS must build its own upgrade UI.
5. **Dunning policy:** How many payment retry attempts before tenant is disabled? Recommendation: Follow Stripe's default dunning (3 attempts over 7 days), then set `AccessGate.LoginsEnabled = false`.

---

## References

- [Azure Well-Architected SaaS — Understand how your costs and revenue are related](https://learn.microsoft.com/azure/well-architected/saas/design-principles)
- [SaaS and multitenant solution architecture on Azure](https://learn.microsoft.com/azure/architecture/guide/saas-multitenant-solution-architecture/)
- `RVS_SaaS_Architecture_Assessment.md` — Cost Optimization section (Billing/Metering gap)
- `RVS_Cloud_Arch_Assessment.md` — Billing/Metering business gap section
- `RVS_Core_Architecture_Version3.1.md` — `TenantConfig` entity, `TenantAccessGateMiddleware`
- `RVS_PRD.md` — Section 9.18 (tenant provisioning), pricing targets
