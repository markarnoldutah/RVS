# RVS Cloud Architecture Assessment — Azure SaaS WAF Analysis

**Date:** March 18, 2026
**Author:** GitHub Copilot (Claude Sonnet 4.6)
**Business Model:** B2B SaaS — pooled multi-tenancy targeting RV dealerships (corporations as tenants). Startup MVP phase (10–20 customers, solo developer).

---

## What Is Already Well-Designed

Before the gaps — these decisions are architecturally sound and should be preserved:

- **Tenant = Corporation (not Location)** is the right isolation boundary. It keeps Blue Compass as one Auth0 Organization, one Cosmos partition, and enables cross-location analytics cheaply.
- **Three-container identity split** (`serviceRequests`/`tenantId` + `globalCustomerAccts`/`email` + `assetLedger`/`assetId`) correctly serves three distinct access patterns without forcing a bad partition key on any of them.
- **Cosmos Gateway mode** for server-side caching of `slugLookup`, `tenantConfigs`, and `lookupSets` is elegant and free — no Redis needed at this scale.
- **CustomerSnapshotEmbedded denormalization** eliminates joins from the dealer dashboard read path (~1 RU/view).
- **Azure OpenAI → rule-based fallback** pattern on `ICategorizationService` correctly treats the external dependency as optional, not critical-path.
- **Email-hash prefix in magic-link token** (`base64url(SHA256(email)[0..8]):random_bytes`) avoids cross-partition scan on token validation — this is a sharp design.
- **Append-only AssetLedger** is the right strategic bet. Proprietary cross-dealer service event data is the structural data moat.
- **Auth0 hybrid MVP strategy** (`app_metadata` → full Organizations on commercialization) is pragmatic and preserves backend compatibility.

---

## Security

### 🔴 CRITICAL — SFTP Private Keys in Cosmos DB

FR-013 specifies SFTP credentials including private key path stored in `TenantConfig`. Storing a private key in a database document — even Cosmos DB — violates Zero Trust and fails any SOC 2 or basic security audit.

**Recommendation:** Store SFTP credentials in **Azure Key Vault**, referenced by `secretUri` in `TenantConfig`. Use `DefaultAzureCredential` (already used everywhere in the stack) to retrieve them. SFTP host, port, and username can remain in `TenantConfig`; the key must not.

```json
// TenantConfig — safe fields only in Cosmos
"sftpConfig": {
  "host": "sftp.dealer.com",
  "port": 22,
  "username": "rvs_export",
  "remoteDirectory": "/incoming",
  "privateKeySecretUri": "https://rvs-kv.vault.azure.net/secrets/sftp-key-{tenantId}"
}
```

### 🔴 CRITICAL — No Web Application Firewall on Public Intake Endpoints

`POST api/intake/{locationSlug}/service-requests` and `POST api/intake/{locationSlug}/service-requests/{id}/attachments` are `[AllowAnonymous]` and accept file uploads. These are the highest-risk surfaces in the system. ASP.NET `RateLimiter` alone is not a WAF — it does not inspect request content, block known attack patterns, or provide bot protection.

**Recommendation:** Place **Azure Front Door with WAF Policy** in front of the App Service. This gives:
- Managed DDoS protection (L7)
- OWASP Core Rule Set for injection/XSS/traversal protection on upload endpoints
- Global CDN for the Blazor WASM static assets (critical — see Performance section)
- Edge-level rate limiting as a first line before traffic hits the API

At MVP scale, Azure Front Door Standard starts at ~$35/month — well within budget given the upload risk.

### 🟡 HIGH — No Azure Key Vault for Any Secrets

Auth0 client secret, Azure OpenAI API endpoint, and any future third-party credentials (SendGrid API key, etc.) should not be in `appsettings.json` or environment variables on App Service. The stack already uses `DefaultAzureCredential` everywhere, which means Key Vault integration requires only a reference config change.

**Recommendation:** Add an Azure Key Vault resource. Wire all secrets using `AddAzureKeyVault` in `Program.cs`. App Service managed identity receives `Key Vault Secrets User` RBAC. Zero credential rotation risk, zero secret exposure in source control.

### 🟡 HIGH — Blob Container Access Model Not Explicitly Defined

The stored `blobUri` in `ServiceRequestAttachmentEmbedded` is a direct blob URI. If the `rvs-attachments` container is accidentally set to public access (easy mistake on initial setup), all customer photos and videos are world-readable without a SAS token.

**Recommendation:** Explicitly set `PublicAccess = BlobContainerPublicAccessType.None` on container creation in `BlobRepository.cs`. Add an IaC check or startup assertion that validates the container ACL. The SAS-only access pattern already documented in the architecture is correct — just make the enforcement explicit in code and infrastructure.

---

## Reliability

### 🟡 HIGH — No Application Insights / Health Telemetry

There is no observability infrastructure in the current architecture. For a SaaS product, an outage without telemetry means discovering problems from customer complaints rather than alerts.

**Recommendation:** Add **Azure Application Insights** with these minimum instrumentation points:
- Tenant ID and location ID as custom dimensions on every request (via `TelemetryInitializer` reading from `ClaimsService`)
- Cosmos RU consumption tracked as custom metric per operation
- Azure OpenAI latency and fallback events tracked
- Exception rate by tenant for SLA monitoring

This is especially important given the SaaS WAF principle *"Be explicit about SLAs you offer your customers."* You cannot enforce what you cannot measure.

### 🟡 HIGH — No Health Check Endpoints

App Service does not know the difference between the API process running and the API actually being healthy (Cosmos connection valid, Auth0 reachable). A misconfigured Cosmos connection string would serve 503s while App Service reports healthy.

**Recommendation:** Add `app.MapHealthChecks("/health")` with checks for Cosmos DB connectivity and blob storage. Add `/health/ready` for readiness (used during deploy) and `/health/live` for liveness. Exclude from `TenantAccessGateMiddleware` allowlist.

### 🟡 MEDIUM — Notification Service Fire-and-Forget Has No Dead Letter Handling

`INotificationService.SendIntakeConfirmationAsync` is called fire-and-forget in Step 6 of the intake flow. If the email provider (SendGrid) is down, the customer never receives their magic-link confirmation email. There is no retry, no audit trail of failed sends.

**Recommendation:** For MVP, log every notification failure with tenant ID, service request ID, and customer email (hashed) to Application Insights. Add a simple retry with exponential backoff using `Microsoft.Extensions.Http.Resilience` on the email client. In Phase 2, consider Azure Service Bus for durable notification queuing.

### 🟢 LOW — Single Availability Zone Deployment

Not a concern until post-MVP, but the architecture should explicitly target **App Service with zone redundancy** when moving to production. Cosmos DB autoscale containers already benefit from Cosmos's built-in multi-replica distribution.

---

## Performance Efficiency

### 🔴 CRITICAL — Blazor WASM Has No CDN

The Blazor WASM app served directly from App Service means every user downloads the full .NET runtime, app assemblies, and static assets from a single App Service instance with no caching. The initial download for a Blazor WASM app is 5–15 MB. For technicians on mobile in service bays, this is a poor experience.

**Recommendation:** Deploy Blazor WASM to **Azure Static Web Apps** (Free tier) or serve via **Azure Front Door CDN**. Static Web Apps + Auth0 is well-supported. The API remains on App Service. This is a significant UX improvement and costs nothing additional.

### 🟡 HIGH — Video/Photo Upload Through API Will Not Scale

The architecture notes "[Future: SAS URI direct upload for large videos]" but the MVP routes all 25 MB uploads through the API. A 25 MB video upload holds an App Service worker thread for 3–8 seconds on mobile LTE and blocks other requests. At 20 concurrent intakes with video, the App Service single-instance becomes the chokepoint.

**Recommendation:** Implement **SAS pre-signed upload** from day one for media files. The flow:
1. Client calls `POST .../attachments/upload-url` → API returns a SAS URI with 10-minute write expiry scoped to `{tenantId}/{serviceRequestId}/{attachmentId}`
2. Client uploads directly to Blob Storage (no API thread held)
3. Client calls `POST .../attachments/confirm` with the attachment metadata

This is ~50 lines of additional code and eliminates the upload bottleneck entirely.

### 🟡 MEDIUM — No CDN for Blob Storage (Photo/Video Access)

Dealer dashboard attachment previews use time-limited SAS URIs pointing directly to Blob Storage. At higher volume with multi-location groups, photos served from a single-region Blob Storage account will be slow for geographically distributed staff.

**Recommendation:** When adding Azure Front Door (see Security above), configure it as a CDN origin for the `rvs-attachments` container. SAS tokens remain valid; Front Door caches the response. No architecture change needed — just add the CDN origin and update `GenerateSasUriAsync` to return the CDN hostname.

---

## Cost Optimization

### 🟡 HIGH — No Billing/Metering Infrastructure

The PRD targets $199–$499/month tiers but the architecture has no mechanism to meter usage, enforce plan limits, emit billing events, or integrate with a payment processor. This is the most significant business gap — the product cannot charge customers without it.

**Recommendation:** Design a **lightweight usage-metering layer** early before tenants accumulate:

| Component | Approach |
|---|---|
| Billing provider | Stripe Billing with Product/Price objects per tier |
| Usage tracking | `TenantConfig` stores `PlanTier` (Starter/Pro/Enterprise) + soft limits (max SR/month, max locations) |
| Overage enforcement | `TenantAccessGateMiddleware` checks monthly SR count against plan limits, returns 402 with upgrade prompt |
| Tenant provisioning | Bootstrap Stripe Customer + Subscription on tenant creation; store `StripeCustomerId` in `TenantConfig` |

The SaaS WAF principle *"Understand how your costs and revenue are related"* specifically calls out the risk of unlimited usage with flat billing.

### 🟡 MEDIUM — `globalCustomerAccts` Container on Manual 400 RU/s

All containers with predictable-but-variable load should use **Autoscale** (400–1,000 RU/s minimum). `globalCustomerAccts` (partitioned by `/email`) will spike during intake bursts and sit idle between them. Manual 400 RU/s risks throttling during intake peaks.

**Recommendation:** Change `globalCustomerAccts`, `dealerships`, `tenantConfigs`, and `lookupSets` to **Autoscale 400–1,000 RU/s**. This reduces per-container monthly floor from ~$25 (manual 400) to ~$5.84/month (autoscale 400 minimum billed at 10% of max). For 9 containers at MVP, the total floor drops from ~$225/month to ~$52/month.

### 🟡 MEDIUM — Blob Storage Lifecycle Management Not Defined

Customer photos and videos are retained indefinitely. Without lifecycle policies, storage costs grow linearly with service request volume.

**Recommendation:** Define per-tier blob retention in `TenantConfig` and implement an Azure Blob Storage **lifecycle management policy**:
- Hot tier: 0–90 days (active case period)
- Cool tier: 91–365 days
- Archive or delete: 365+ days (configurable per tenant plan)

### 🟢 LOW — Auth0 Cost Cliff at Commercialization

The plan to migrate from Free (`app_metadata`) to Essentials B2B ($150/month) at commercialization is pragmatic. However, at 5–6 paying tenants, the Auth0 cost alone represents 15–25% of ARR at the lowest price tier.

**Recommendation:** Define the trigger as: *"Migrate to Auth0 Organizations when tenant #6 signs or when a prospective tenant requires enterprise SSO (Azure AD/Google Workspace federation)."* The backend is already migration-neutral as designed.

---

## Operational Excellence

### 🔴 CRITICAL — No IaC / CI/CD Pipeline

There is no mention of Bicep/Terraform for infrastructure definition or GitHub Actions for deployment. For a SaaS product, the inability to recreate the environment quickly means:
- Dev environment drift from production is invisible
- Recovery from an infrastructure incident is manual and slow
- Tenant provisioning cannot be automated

**Recommendation:** Immediately create:
1. **Bicep templates** for all Azure resources (App Service, Cosmos DB account + containers, Blob Storage, Key Vault, Application Insights, Static Web Apps)
2. **GitHub Actions pipeline** with stages: `build → test → deploy-staging → deploy-prod` with manual approval on prod
3. **Tenant provisioning script** as a Bicep module that creates Cosmos seed data and Auth0 metadata entry given a new `tenantId`

The WAF SaaS principles *"Roll out changes safely"* and *"Adopt consistent processes"* both require this infrastructure.

### 🟡 HIGH — Tenant-Level Rate Limiting (Noisy Neighbor on Dealer API)

Current rate limiting applies only to `[AllowAnonymous]` endpoints. Authenticated dealer API endpoints have no per-tenant throughput controls. A Blue Compass automation script running batch `ServiceRequests/search` queries across 100 locations could consume a disproportionate share of Cosmos RU capacity, degrading response times for smaller tenants sharing the same containers.

This is the classic [Noisy Neighbor antipattern](https://learn.microsoft.com/azure/architecture/antipatterns/noisy-neighbor/noisy-neighbor) for pooled SaaS.

**Recommendation:** Add a **per-tenant rate limit policy** on dealer API endpoints using `System.Threading.RateLimiting`:

```csharp
// In Program.cs — per-tenant sliding window
options.AddPolicy("PerTenantPolicy", context =>
{
    var tenantId = context.User?.FindFirst("tenantId")?.Value ?? "anonymous";
    return RateLimitPartition.GetSlidingWindowLimiter(tenantId, _ =>
        new SlidingWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) });
});
```

Starter tenants: 100 req/min. Pro: 300 req/min. Enterprise: 1,000 req/min. Returns `429` with `Retry-After`. Also functions as a future tier-upgrade sales lever.

### 🟡 MEDIUM — No Structured Tenant Context in Logs

Without tenant-tagged logs, diagnosing "a tenant reported an issue at 2pm" requires guessing which log entries belong to which dealership. This becomes painful at 10+ tenants.

**Recommendation:** Add a scoped logging enricher via `ILogger.BeginScope()` in controllers:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["TenantId"] = tenantId,
    ["LocationId"] = locationId  // where applicable
}))
{
    // controller action body
}
```

This tags all downstream log entries with tenant context automatically when using Application Insights structured logging.

---

## SaaS-Specific Architecture Notes

### No Deployment Stamp Foundation (Deferred — Correct for MVP)

The current single-stamp architecture is appropriate for MVP. Deployment Stamps are [not recommended for simple solutions that don't need high-scale scaleout](https://learn.microsoft.com/azure/architecture/patterns/deployment-stamp#when-to-use-this-pattern). However, two stubs should be set up now to avoid expensive migrations later:

1. **Stamp identifier in config** — Add `"StampId": "stamp-01"` to `appsettings.json`. Any cross-stamp concern (future central analytics warehouse, tenant routing) will need this.
2. **No hard-coded single-tenant assumptions** — Already clean (all operations require `tenantId`). Preserve this discipline strictly.

### `GlobalCustomerAcct` Cross-Tenant Data Access Security

`GlobalCustomerAcct` crosses tenant boundaries by design (same customer, multiple dealerships). The magic-link status page returns data from multiple tenants. This requires explicit validation:

- The customer status page endpoint must **never** return any tenant-specific data except what belongs to the requesting customer (email-hash verified)
- The `LinkedProfiles` list on `GlobalCustomerAcct` exposes `tenantId`, `DealershipName`, etc. — ensure none of these leak into the customer status page response beyond the safe `CustomerServiceRequestSummaryDto`

Already implied by the DTO design but should be validated in code review.

---

## Prioritized Action Plan

| Priority | Action | WAF Pillar | Effort |
|---|---|---|---|
| 🔴 P0 | Move SFTP private keys to Azure Key Vault | Security | 2 hours |
| 🔴 P0 | Add Azure Key Vault for all secrets (`appsettings` → Key Vault refs) | Security | 4 hours |
| 🔴 P0 | Create Bicep IaC + GitHub Actions CI/CD pipeline | Ops | 1 day |
| 🔴 P0 | Add Application Insights with tenant-tagged telemetry | Ops/Reliability | 4 hours |
| 🟡 P1 | Add Azure Front Door (WAF policy + CDN) | Security/Performance | 1 day |
| 🟡 P1 | Deploy Blazor WASM to Azure Static Web Apps | Performance | 4 hours |
| 🟡 P1 | Implement SAS pre-signed upload for media attachments | Performance | 1 day |
| 🟡 P1 | Design billing/metering infrastructure (Stripe + plan limits in TenantConfig) | Cost/Business | 2 days |
| 🟡 P1 | Add per-tenant rate limiting on dealer API endpoints | Reliability (Noisy Neighbor) | 4 hours |
| 🟡 P2 | Switch all containers to Autoscale 400 RU/s baseline | Cost | 1 hour |
| 🟡 P2 | Add health check endpoints (`/health/live`, `/health/ready`) | Reliability | 2 hours |
| 🟡 P2 | Add blob lifecycle management policies | Cost | 2 hours |
| 🟢 P3 | Notification service retry + failure audit log | Reliability | 4 hours |
| 🟢 P3 | Tenant-context structured logging enricher | Ops | 2 hours |

P0 items (Key Vault, IaC, App Insights) are non-negotiable before onboarding paying customers — they are the minimum bar for a production SaaS workload. Everything else phases with growth.

---

*Assessment based on ASOT docs: RVS_Core_Architecture_Version3.md, RVS_Auth0_Identity_Version2.md, RVS_PRD.md, RVS_Context.md — March 18, 2026*
