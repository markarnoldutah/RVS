# RVS ASOT Gap Analysis & Optimization Review

**Date:** March 18, 2026
**Scope:** `RVS_Context.md`, `RVS_PRD.md`, `RVS_Core_Architecture_Version3.md`, `RVS_Auth0_Identity_Version2.md`, `RVS_Auth0_roles&perms.md`, `RVS_SaaS_Architecture_Assessment.md`

---

## Document Health Issues

### `RVS_Auth0_roles&perms.md` Conflicts With V2 (Critical)

The V1 roles document is materially inconsistent with `RVS_Auth0_Identity_Version2.md` and should be archived or deleted. Key conflicts:

| Dimension | V1 (`roles&perms.md`) | V2 (`Identity_Version2.md`) |
|---|---|---|
| Role count | 6 roles | 8 roles (+`dealer:corporate-admin`, +`dealer:regional-manager`) |
| `attachments:upload` permission | Missing | Present (technicians + advisors) |
| `dealerships:qr-code` permission | Separate permission | Folded into `CanReadLocations` |
| `ClaimsService` sample code | No location scoping methods | Full `GetLocationIds()`, `HasAccessToLocation()` |
| JWT samples | No `locationIds` claim | Full regional manager + SLC examples |

Anyone reading the ASOT set reads incompatible permission matrices and divergent `ClaimsService` code. This is the most immediate cleanup to make.

---

### `RVS_Context.md` Is a First-Generation Document

This document predates the multi-location tenancy model entirely. A developer or investor using it as a platform overview would be working from severely outdated assumptions:

- No mention of the Blue Compass problem or corporation-as-tenant model
- No mention of `dealer:corporate-admin` or `dealer:regional-manager` roles
- Phase vocabulary doesn't match the PRD. PRD Phase 1 = MVP; Context doc's "Phase 1" = "Intake Portal" with no analytics, no roles, no auth
- `AssetLedger` data moat not mentioned
- Auth0 identity strategy not mentioned
- Nine-container Cosmos design not mentioned

This document either needs a complete rewrite or should be replaced with a short "What is RVS" summary that links to the PRD.

---

### Stale Cross-References in Core Architecture

`RVS_Core_Architecture_Version3.md` cross-references `RVS_Auth0_Identity.md` (eight places) — the file that doesn't exist. The actual file is `RVS_Auth0_Identity_Version2.md`. Every link is broken.

---

### ASOT Date Stamp on Auth0 V2 Document

`RVS_Auth0_Identity_Version2.md` is dated March 10, 2026 — eight days before the V3 architecture and SaaS Assessment (March 18). The V3 architecture resolved several technician mobile app gaps (Section 17.1) that include Auth0 permission changes (`service-requests:search` added for technicians, `attachments:upload` added). The Auth0 V2 document needs its date bumped and a reconciled permission matrix to confirm it reflects those changes.

---

## Architecture Gaps Not Covered in Any ASOT Document

### SFTP / DMS Export — No Implementation Section

FR-013 is marked High priority, and the SaaS Assessment calls out the Key Vault requirement. But no document answers the fundamental design questions:

- **Hosted Service vs. Azure Function?** A `BackgroundService` inside `RVS.API` is simple but puts periodic I/O pressure on the API process. An Azure Function (timer trigger) is isolated and independently scalable, but adds deployment complexity.
- **Per-tenant scheduling:** Is the SFTP push at a global time (e.g., 2 AM for all tenants) or tenant-configurable? The `TenantConfig` doesn't currently have a `SftpScheduleExpression` field.
- **Failure handling:** If the dealer's SFTP endpoint is unreachable, how many retries before an alert fires? Is the failed export retried next day or discarded?
- **CSV streaming vs. batching:** For a tenant with 500+ service requests in the export window, streaming pagination matters for memory.
- **Key Vault reference pattern:** The SaaS Assessment shows the `privateKeySecretUri` field but the `TenantConfig` entity hasn't been updated to reflect it. The entity presumably still has the raw key field.

---

### VIN Decode + Camera Scan — No Resiliency Design

FR-003 references the NHTSA vPIC API (`https://vpic.nhtsa.dot.gov/api/`) as a free public service. This is the only hard external dependency that is not behind the `ICategorizationService` fallback abstraction. There is no documented answer to:

- What happens if vPIC is down at intake time? (Form blocked? User sees error? VIN pre-population skipped?)
- Is vPIC called client-side by Blazor or server-side by the API? If server-side, which service/endpoint wraps it?
- Is there an interface (`IVinDecodeService`) with a stub fallback for offline dev/test?
- What is the VIN validation rule? NHTSA check digit (North American VINs only) vs. length-only check for EU/international RVs?
- The `BarcodeDetector` API has ~70% browser support. The `zxing-js` fallback is mentioned but not specified as an npm package choice.

---

### Speech-to-Text + AI Clean-Up — No Endpoint or Flow Design

FR-005 is a High-priority feature, but Section 16 (Azure OpenAI) covers only two methods: `CategorizeAsync` and `GenerateDiagnosticQuestionsAsync`. The speech-to-text clean-up path is entirely undocumented:

- Is there a third `ICategorizationService` method (`CleanTranscriptAsync`), or is a new interface needed?
- Is the raw transcript sent to the API for AI cleanup, or does this happen in-browser using a client-side prompt to Azure OpenAI?
- The two-phase intake flow in Section 16.6 shows Phase 1 (diagnostic questions) and Phase 2 (submission), but speech-to-text cleanup happens *during* issue description entry — it's an unstated Phase 0.5 API call.
- Token cost for transcript cleanup is not included in the Section 16.5 cost model.

---

### Telemetry / Application Insights Architecture — P0 Item With No Design Doc

The SaaS Assessment rated Application Insights as P0 / non-negotiable before onboarding paying customers. The assessment gives a bullet-point list of what to track (tenant-tagged dimensions, RU consumption, OpenAI latency). But no document answers:

- Where is the `TelemetryInitializer` registered and what standard dimensions does it stamp?
- Which service requests emit `CustomEvent` vs. just `Dependency` traces?
- What is the alert policy for `429 / RU throttling` on the Cosmos containers?
- Is there a structured log schema (correlation IDs, operation types)?

Without this, the P0 action item has been acknowledged but not designed.

---

### Billing / Metering — The Business Gap

The SaaS Assessment identifies billing as "the most significant business gap." It's still not a PRD functional requirement. The following are completely unanswered architecturally:

- `TenantConfig` has no `PlanTier`, `MaxMonthlyServiceRequests`, or `StripeCustomerId` fields
- There's no documented mechanism for counting monthly SR volume per tenant (change feed? atomic counter in Tables? daily aggregate job?)
- There's no decision on *where* plan enforcement happens. The Assessment suggests `TenantAccessGateMiddleware`, but the middleware currently only checks `IsActive` — adding plan enforcement would require RU-consuming count queries on every intake
- Stripe webhook handling (invoice.paid, subscription.deleted) has no endpoint, no interface
- Free trial period logic is undefined

---

### Azure Infrastructure Architecture — P0 With No Document

The SaaS Assessment calls IaC the highest-effort P0 item. No Azure resource topology exists in the ASOT set. The following decisions are implicit but undocumented:

- App Service Plan tier (B1? P1v3? When does it scale up?)
- Single-region vs. multi-region Cosmos DB account (paired region on write, available at GA?)
- Key Vault access model (RBAC vs. Access Policies — Microsoft has deprecated Access Policies)
- Storage account redundancy tier (LRS vs. ZRS vs. GRS for attachments)
- Static Web App tier for Blazor WASM (Free vs. Standard — Standard needed for Auth0 custom auth)
- Deployment slot configuration for API (staging → production swap strategy)
- Environment separation model (one subscription per environment vs. resource group per environment)

---

### Blazor WASM Frontend Architecture — No Document at All

The ASOT set has zero frontend architecture. The FrontEnd folder covers feature requirements but no component model or Blazor architecture. Missing:

- Component hierarchy (which pages, which shared components, which feature folders)
- State management strategy (Fluxor? Component cascading? Simple service-based binding?)
- Auth0 PKCE flow in Blazor WASM (requires `Microsoft.Authentication.WebAssembly.Msal` or `Auth0.OidcClient` — not the same as server-side JWT)
- Anonymous intake route vs. authenticated dashboard: how does the single Blazor app serve both without forcing Auth0 on anonymous customers?
- VIN camera scan: `BarcodeDetector` browser API from Blazor WASM requires JavaScript interop — no `IJSRuntime` wrapper is designed
- Offline queue for technician mobile (IndexedDB from Blazor requires `Blazored.LocalStorage` or similar — no package decision made)

---

## Outstanding SaaS Assessment P0/P1 Items Not Incorporated Into ASOT Docs

The assessment was written March 18, 2026 — same date as V3 architecture. These items require explicit resolution in a subsequent document update:

| Assessment Item | Priority | Status in ASOT Docs |
|---|---|---|
| SFTP private keys → Key Vault | P0 | PRD 7.2 says "stored in Key Vault" but `TenantConfig` entity not updated; no implementation section |
| Key Vault for all secrets | P0 | PRD 7.2 intent stated; no `appsettings.json` → Key Vault reference pattern documented |
| Bicep IaC + GitHub Actions CI/CD | P0 | No document created |
| Application Insights + tenant telemetry | P0 | No architecture designed |
| Azure Front Door + WAF | P1 | No deployment topology document |
| Blazor WASM → Azure Static Web Apps | P1 | No frontend architecture document; still says "App Service" in pipeline |
| SAS pre-signed media upload | P1 | Core Architecture Section 10 says "Future" — PRD FR-007 still says "via API" (inconsistency) |
| Billing / metering (Stripe) | P1 | Not in Core Architecture or PRD as a functional requirement |
| Per-tenant rate limiting | P1 | Assessment has code snippet; not incorporated into Section 9 (Middleware Pipeline) |
| Autoscale all containers | P2 | Assessment recommends changing 4 containers; Table 4.1 still shows Manual 400 for those containers |
| Health check endpoints | P2 | Not in Section 9 (Middleware Pipeline) and not added to the middleware allowlist spec |

---

## Specific Technical Gaps and Edge Cases

### Race Condition in Intake Orchestration (No Compensation Strategy)

Steps 1–5 of the 6-step intake flow execute sequentially without a transaction. If Step 3 (ServiceRequest write) succeeds but Step 4 (AssetLedger write) fails, the system is in a partially inconsistent state: the SR exists, the customer is not notified, and the data moat has a gap. The architecture documents no compensation strategy:

- Should the intake endpoint retry Step 4 internally before returning?
- Should notification (Step 6) be gated on Steps 1–5 all succeeding?
- Is eventual consistency acceptable for the AssetLedger (change feed enrichment in Phase 5–6 implies yes)?

The answer is probably "accept eventual consistency," but this should be an explicit architectural decision with a documented rationale, not an unstated assumption.

---

### Magic Link Token Storage Inconsistency

PRD Section 7.2: *"Magic-link tokens are cryptographically random, time-limited, and stored hashed if the implementation requires additional security hardening."*

Core Architecture Section 13: Describes the token format and treats the stored value as the raw token (no mention of hashing).

These are inconsistent. If the token is stored hashed, `ValidateMagicLinkAsync` must hash the incoming token before comparison, which is not reflected in the documented method signature. If the token is stored as-is, the PRD hedging language is misleading. One of the two needs to be authoritative.

---

### Regional Manager `regionTag` Lifecycle Not Defined

The regional manager role scopes access by `regionTag` on their `app_metadata` matching against `Location.RegionTag`. The following lifecycle questions are unanswered:

- **Assignment:** Who sets `regionTag` on a user's `app_metadata`? The tenant provisioning flow (Section 12) only covers owner onboarding.
- **Consistency:** If a Location's `regionTag` changes from "west" to "southwest", do existing regional managers lose access to that location until their `app_metadata` is manually updated?
- **Token staleness:** `app_metadata` is embedded at login time. A mid-session tag change at Auth0 is not reflected until token renewal (1 hour). Is this acceptable?
- **Empty regionTag:** A `dealer:regional-manager` with no `regionTag` would pass `GetRegionTag()` returning null — but `HasAccessToLocation()` only checks `locationIds`, not `regionTag`. The documents don't clarify how the service layer enforces regional scope vs. explicit location scope for this role.

---

### Section 10A Audit Trail

Technicians update `ServiceEvent` fields (FailureMode, RepairAction, PartsUsed, LaborHours) via `service-requests:update-service-event`. The only audit stamp is `ServiceRequest.UpdatedByUserId` / `UpdatedAtUtc` — which records the *last* update but not *who changed what and when*.

For warranty claims, dealer disputes, or litigation involving a specific repair action, attribution and history of `ServiceEvent` field changes may be necessary. The current design loses this history on every overwrite. No decision is documented on whether:
- A `ServiceEventChangeLog` embedded list is needed
- The `AssetLedger` enrichment in Phase 5–6 serves as a sufficient audit trail substitute
- The business requirement explicitly does not need field-level history

---

### CORS Policy Origins Not Defined Per Environment

Section 9 registers `UseCors("AllowBlazorClient")` but nowhere in the ASOT documents are the exact origins defined per environment. This matters because:

- Dev origin is typically `https://localhost:7xxx`
- Staging origin is `https://staging.dashboard.rvserviceflow.com`
- Production origin is `https://dashboard.rvserviceflow.com`
- The intake form is `https://app.rvserviceflow.com` (different subdomain from dashboard)

With no defined origin policy, the CORS configuration is a potential security misconfiguration waiting to happen during deployment.

---

### `AllKnownAssetIds` Cap of 200 Items — Enforcement Undefined

`GlobalCustomerAcct.AllKnownAssetIds` is capped at 200 items (most-recent first), with overflow recoverable from `AssetLedger`. This cap is undocumented in the PRD and has no associated business logic described:

- When item 201 is added, is the oldest silently dropped?
- Is there a notification or log event?
- Is the 200 limit validated in the service layer or is it a documentation-only convention?

A customer who is a fleet owner (rental company, livery service) could plausibly hit this limit. The behavior should be explicitly defined.

---

## Optimization Opportunities

### Consolidate `ICategorizationService` Scope

Currently `ICategorizationService` performs two logically distinct operations:

1. Transcript cleanup (implicit, undocumented — FR-005)
2. Diagnostic question generation — `GenerateDiagnosticQuestionsAsync`
3. Issue categorization + technician summary — `CategorizeAsync`

All three share the same Azure OpenAI dependency. Grouping them under one interface and one client is efficient, but the omission of #1 from the interface definition means the speech-to-text flow is either undocumented or will be added ad-hoc during implementation. Explicitly add a `CleanTranscriptAsync` method to the interface and document the fallback behavior (return raw transcript unchanged if AI is unavailable).

---

### `GlobalCustomerAcct.LinkedProfiles` Growing List

`LinkedProfiles` is an embedded list of `LinkedProfileReferenceEmbedded` with no documented size cap. A customer who submits at 50 different dealerships over their lifetime would have 50 embedded profile references in one document. Unlike `AllKnownAssetIds`, no cap or mitigation is defined. For a frequent RVer — the exact power user you want — this could become a large document with unbounded growth. A similar cap-and-recover approach (linked to `AssetLedger` for history) should be applied.

---

### `slugLookup` Container Invalidation Race

Section 7.3 describes slug rename as: "the old slug entry is deleted and the new one is written atomically before the `Location` document is updated." Cosmos DB has no multi-document transactions across containers. This is two separate operations. A window exists between the old slug deletion and the new slug write where no valid slug resolves to the location. And if the new slug write fails after the deletion, the location becomes permanently unreachable.

The correct order is **WriteNewSlug → UpdateLocation → DeleteOldSlug**, so there is always at least one valid slug. This eliminates the unreachability window entirely and degrades safely on failure at any step.

---

### `ServiceRequestSearchRequestDto` — Unindexed `CustomerName` Filter

The search DTO supports filtering by `CustomerName` but the `serviceRequests` indexing policy (Section 4.4) has no `customer/firstName/?` or `customer/lastName/?` index path and no composite index for name search. At query time this becomes a full-partition scan. Either:
- Add a composite index on `[tenantId ASC, customer/lastName ASC, customer/firstName ASC]`
- Document that `CustomerName` triggers a full scan and is intentionally not indexed (acceptable at MVP volume)

---

### Autoscale Container Config Not Updated After Assessment

The SaaS Assessment recommended switching `globalCustomerAccts`, `dealerships`, `tenantConfigs`, and `lookupSets` from Manual 400 RU to Autoscale 400–1,000. The Core Architecture Section 4.1 container table still shows those containers as `Manual 400`. Either apply the recommendation and update the table, or explicitly document the deferral with a rationale.

---

### `TenantAccessGateMiddleware` Scale Limit Undocumented

The middleware reads `TenantConfig` on every authenticated request. The claim that "gateway caching makes this effectively free" holds for `ConnectionMode.Gateway` but relies on the gateway cache window (typically 5 minutes). This is fine at 10–20 tenants. At >1,000 tenants with diversified request patterns, gateway cache hit rate drops and Cosmos RU costs from this middleware become measurable. A `TenantConfigCache` (IMemoryCache, scoped per deploy) should be documented as the Phase 2 mitigation before that scale is reached.

---

## Prioritized Action Plan

| Priority | Issue | Action |
|---|---|---|
| 🔴 P0 | `RVS_Auth0_roles&perms.md` (V1) conflicts with V2 | Delete or clearly archive |
| 🔴 P0 | `RVS_Context.md` is first-generation, misrepresents current design | Rewrite or retire |
| 🔴 P0 | No billing/metering in PRD or Core Architecture | Add billing FR to PRD; add metering section to Core Architecture |
| 🔴 P0 | No IaC / deployment topology document | Create Azure Infrastructure Architecture doc |
| 🔴 P0 | Magic link token storage inconsistency between PRD and Core Arch | Decide hashed or raw; align both docs |
| 🟡 P1 | All cross-references say `RVS_Auth0_Identity.md` (not V2) | Fix 8 references in Core Architecture |
| 🟡 P1 | Auth0 V2 dated March 10 — post-V3 permission changes not reflected | Sync date and permission matrix |
| 🟡 P1 | No SFTP/DMS export design section | Add dedicated section to Core Architecture |
| 🟡 P1 | VIN decode + camera scan resiliency undefined | Add `IVinDecodeService` abstraction and failure behavior |
| 🟡 P1 | Speech-to-text AI cleanup endpoint not documented | Add `CleanTranscriptAsync` to `ICategorizationService` spec |
| 🟡 P1 | Slug rename sequencing has unreachability window | Document corrected order: write new → update location → delete old |
| 🟡 P1 | `CustomerName` filter unindexed in `serviceRequests` policy | Add index path or explicitly document full-scan |
| 🟡 P1 | Intake orchestration race condition undocumented | Explicitly decide on eventual consistency acceptance |
| 🟡 P1 | `GlobalCustomerAcct.LinkedProfiles` has no size cap | Define cap and recover strategy matching `AllKnownAssetIds` |
| 🟡 P1 | No Blazor frontend architecture document | Create frontend ASOT doc |
| 🟡 P2 | CORS origins per environment not defined | Document `AllowBlazorClient` policy origins |
| 🟡 P2 | Container RU table not updated post-Assessment | Update Table 4.1 to Autoscale for 4 containers |
| 🟡 P2 | Regional manager `regionTag` lifecycle undefined | Document assignment, staleness, and service-layer enforcement |
| 🟡 P2 | Section 10A has no field-level audit trail | Define business requirement; decide log approach |
| 🟢 P3 | Application Insights architecture needs design | Add telemetry strategy section |
| 🟢 P3 | `AllKnownAssetIds` cap behavior not enforced in service layer | Validate/document enforcement |
| 🟢 P3 | `TenantAccessGateMiddleware` scale limit undocumented | Add Phase 2 cache mitigation note |

---

## Summary

The three most impactful immediate actions are:

1. **Delete the V1 Auth0 document** to eliminate the conflicting permission matrix — any developer onboarding today would be confused by the two incompatible role models.
2. **Add billing as a functional requirement** before any implementation begins. It shapes `TenantConfig`, `TenantAccessGateMiddleware`, tenant provisioning, and the analytics counter design. Retrofitting metering into a live system with accumulated tenant data is far more expensive than designing it in from the start.
3. **Create the Azure Infrastructure Architecture document.** Every deployment decision — Key Vault access model, storage redundancy, Static Web App tier, environment separation — is currently undocumented. This is the missing context for all P0 security and reliability items identified in the SaaS Assessment.

---

*Review based on ASOT documents as of March 18, 2026.*
