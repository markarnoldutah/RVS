# RV Service Flow (RVS) — Technical PRD

**Version:** 3.4
**Date:** April 30, 2026
**Status:** Draft (post-pivot, v3.4 — Enterprise Scale technical foundations)
**Supersedes:** v3.3 (April 30, 2026, earlier same-day), v3.0 (same-day), v2.0 (same-day), v1.0 (March 20, 2026)
**Derived from:** RVS_Core_Architecture_Version3.1.md (ASOT)

> **What changed in v3.4 (same-day refinement of v3.3):** Two new FR sections added to the Functional Requirements (§5.8 and §5.9) covering Enterprise Scale technical foundations introduced in `RVS_Premium_PRD.md` v1.2 (FR-ES-007, FR-ES-011, FR-ES-012):
> - **§5.8 OEM Revenue-Share Calculation** — six new FRs (FR-OEM-01 through FR-OEM-06): aggregate definition and contribution measurement, quarterly calculation job, revenue-share approval workflow, customer-facing dashboard, cancellation/forfeiture handling, anti-gaming measures
> - **§5.9 Custom SLA Monitoring** — six new FRs (FR-SLA-01 through FR-SLA-06): per-tenant SLA configuration, uptime measurement methodology, automatic service credit calculation, P1 incident escalation chain, post-incident review obligation, quarterly conformance reporting
> - Existing §5.8 (Self-Service User Provisioning) renumbered to §5.10; existing §5.9 (Verification Gate) renumbered to §5.11
> - **§3.4 Phase 3 Ship Criteria updated** to include FR-SLA-01 through FR-SLA-06 framework operational, FR-OEM-01 through FR-OEM-06 conditional on OEM pilot revenue-share clause, and backend engineer #2 onboarded for solutions engineer FTE allocation
> - Phase 3 timing unchanged (months 13-18); the new FRs ship alongside first ES customer onboarding

> **What changed in v3.3 (same-day refinement of v3.0):** Pricing model restructured. Material changes from v3.0:
> - **FR-BILL-04 rewritten** for volume-banded per-location billing (Pro: $79/$69/$59 by 1-9/10-24/25-49 loc; Premium: $119/$109/$99 by same bands; Solo flat $39; Enterprise Scale custom $150-$300/loc)
> - **FR-BILL-05 removed** — per-user surcharge metering eliminated (no per-user pricing at any tier in v3.3)
> - **FR-BILL-07 added** — support tier add-on billing (Priority $500/mo opt-in for Pro and Premium)
> - **FR-BILL-08 added** — transition discount mechanics (50% off first 3 months for Pro→Premium and Premium→Enterprise Scale upgrades; subscription only, not implementation fees)
> - **FR-BILL-09 added** — implementation fee billing infrastructure with banded fees ($5K/$7.5K/$10K Premium; $15K/$25K/$40K Enterprise Scale)
> - **FR-USER-03 reworded** — user activity tracking now optional/observability-only (no longer feeds per-user billing)
> - **FR-USER-04 removed** — technician role exemption no longer needed
> - **FR-TENANT-06 updated** to reflect banded billing logic (no surcharge concept)
> - **Phase 1 ship criteria updated** to reflect Pro SR cap of 600/loc/mo and banded billing requirements
> - Engineering scope: ~1 sprint saved on per-user metering removal; ~1 sprint added on support tier infrastructure (Phase 2); banded billing logic is minor addition to Sprint 13-14

> **What changed in v3.0 (earlier same-day):** v2.0 was written under a Free + Enterprise pricing model. v3.0 reflected the four-tier model — Solo / Professional / Premium / Enterprise Scale — plus the OEM Data Licensing track. Core architecture (Cosmos schema, Auth0 model, middleware, AI patterns, performance targets) was unchanged. Stripe billing endpoints reintroduced; self-service user provisioning added; verification gate added; benchmarking rate-limiting middleware added.

---

## Companion Documents

| Document | Purpose |
|---|---|
| [RVS_Context.md](RVS_Context.md) | Platform overview, four-tier business model, strategic context (v3.0) |
| [RVS_Competitive_Strategy.md](RVS_Competitive_Strategy.md) | Competitive positioning, Yes/No filter, sales objection handling (v3.0) |
| [RVS_PRD.md](RVS_PRD.md) | Solo + Professional tier product requirements (v3.0) |
| [RVS_Premium_PRD.md](RVS_Premium_PRD.md) | Premium + Enterprise Scale tier product requirements |
| [RVS_data_moat.md](RVS_data_moat.md) | Asset ledger, taxonomy, anonymization, anti-corpus-theft architecture, ToS language (v3.0) |
| [RVS_OEM_GoToMarket.md](RVS_OEM_GoToMarket.md) | OEM strategy, target accounts, deal structures |
| [RVS_Implementation_Plan_v2.md](RVS_Implementation_Plan_v2.md) | Three-phase execution plan (Solo+Pro → Premium → Enterprise Scale + OEM) |
| [RVS_Auth0_Identity_Version2.md](Auth0/RVS_Auth0_Identity_Version2.md) | RBAC model, JWT structure, ClaimsService, `app_metadata` tenant scoping |
| [.github/copilot-instructions.md](../../.github/copilot-instructions.md) | Coding conventions, project patterns |

---

## 1. Document Purpose

This Technical PRD translates the architecture decisions captured in `RVS_Core_Architecture_Version3.1.md` into explicit, testable technical requirements. It specifies:

- Non-functional requirements and acceptance thresholds
- Per-endpoint API contracts (request/response shapes, auth, errors)
- Data model specifications and constraints
- Security requirements and threat mitigations
- Integration contracts for external services
- Observability, testing, and deployment requirements
- Explicit constraints, known gaps, and deferred items

This document is authoritative for implementation. Anything not covered here defers to the architecture ASOT.

---

## 2. System Overview

### 2.1 Platform Summary

RVS is a B2B SaaS platform for RV dealership service management. It digitizes the customer intake workflow that currently relies on phone calls and manual notes.

**Stack:**
- **Backend:** ASP.NET Core (.NET 10, C# 14), RESTful API, OpenAPI/Swagger
- **Database:** Azure Cosmos DB (SQL API, 9 containers)
- **Storage:** Azure Blob Storage (attachments)
- **Identity:** Auth0 (JWT Bearer; `app_metadata` tenant scoping)
- **AI:** Azure OpenAI (`gpt-4o-mini`) for issue categorization, diagnostic questions, transcript cleanup, and category suggestion
- **Notifications:** Azure Communication Services (ACS) for both email and SMS (behind `INotificationService` + `ISmsNotificationService`)
- **Frontend:** Blazor WebAssembly (Blazor.Intake + Blazor.Manager), MAUI Blazor Hybrid (MAUI.Tech); UI component library: **MudBlazor 9.x** (Material Design 3)

### 2.2 Multi-Tenancy Model

```
Tenant  = Corporation (e.g., Blue Compass RV)
         → 1 Cosmos DB partition (key: tenantId from app_metadata)
         → 1..N physical Locations per corporation

Customer = Anonymous (MVP); no Auth0 account required
         → Shadow profiles auto-created in: GlobalCustomerAcct + CustomerProfile
```

**Partition key invariant:** Every dealer operation is single-partition on `tenantId`. Cross-partition queries are structurally prevented in the data access layer.

### 2.3 Applications

| App | Framework | Users |
|---|---|---|
| `RVS.Blazor.Intake` | Blazor WebAssembly (Standalone PWA) | RV owners (anonymous) |
| `RVS.Blazor.Manager` | Blazor WebAssembly (Standalone) | Advisors, managers, corporate admins |
| `RVS.MAUI.Tech` | MAUI Blazor Hybrid (iOS + Android) | Technicians (offline-first) |
| `RVS.UI.Shared` | Razor Class Library | Shared components, API clients |

---

## 3. Goals and Success Metrics

### 3.1 Technical Goals

| Goal | Target | Measurement |
|---|---|---|
| Intake end-to-end latency (P95) | < 3 seconds (API round-trip from form submit to 201) | APM trace on `POST api/intake/{slug}/service-requests` |
| Intake form completion time | < 3 minutes average | Client-side telemetry (step timings) |
| Technician job close interaction | < 5 seconds (P95 for `PUT` from offline queue flush) | APM trace |
| API availability | ≥ 99.5% monthly uptime (MVP) | Azure App Insights availability tests |
| Anonymous intake error rate | < 1% of submissions result in 5xx | APM error rate dashboard |
| Cosmos RU cost per intake | ≤ 12 RU (cold path) | Cosmos diagnostics on intake controller |
| Magic-link status page load | < 500 ms TTFB (P99) | Lighthouse / App Insights |
| AI diagnostic question latency | < 1.5 seconds (P95) | APM trace on `ICategorizationService.SuggestDiagnosticQuestionsAsync` |
| AI category suggestion latency | < 1.0 seconds (P95) | APM trace on `POST api/intake/{slug}/ai/suggest-category` |
| Attachment upload (25 MB file) | < 10 seconds (P95; direct-to-blob via SAS) | Client telemetry |

### 3.2 Phase 1 Ship Criteria (Solo + Professional tiers)

- 5 design partner dealerships across Solo and Professional tiers complete full intake → advisor dashboard → technician update cycle without bugs
- At least 2 of the 5 design partners are operating Professional tier on multi-location coordination features (cross-location queue, regional manager, cross-loc analytics)
- Intake form usable on Safari iOS, Chrome Android, Chrome/Edge Windows
- Zero `platform:admin` intervention required to onboard a new dealership (self-serve Solo signup with Stripe live)
- 30-day trial converts to paid Stripe subscription cleanly with no manual intervention
- Self-service user provisioning via Auth0 Management API works end-to-end (invitation, password setup, role activation, deactivation)
- All OWASP Top 10 vectors addressed (see Section 9)
- **Asset ledger writes happen on every intake submission with structured Section 10A taxonomy enforced**
- **Section 10A taxonomy adherence rate ≥ 95% across submitted entries**
- **Outbound notification webhook delivers within 30s P99 of triggering event**
- **Solo tier monthly SR cap (300/loc/mo) enforced, returning HTTP 402 on breach**
- **Professional tier monthly SR cap (600/loc/mo) enforced, returning HTTP 402 on breach**
- **Volume-banded per-location billing (FR-BILL-04) operational for Solo (flat $39/loc) and Pro (banded $79/$69/$59 by 1-9/10-24/25-49 loc); each Stripe invoice reflects correct band-applicable rate**
- **Verification gate workflow operational with manual review queue functional and documented SLA (24-72 hours)**
- **Audit log infrastructure capturing required event types** even in Phase 1, ahead of benchmarking ship in Phase 2
- Industry benchmarking deferred to Phase 2 (no API surface in Phase 1; placeholder UI only)

### 3.3 Phase 2 Ship Criteria (Premium tier and anonymization)

- Cross-location analytics dashboard in production with positive design partner feedback (carries from Pro tier; refines for Premium use)
- SAML SSO validated with at least 2 different IdPs (Okta + Entra ID, ideally) via Auth0 Organizations
- SCIM provisioning validated end-to-end (user create, update, deactivate from upstream IdP)
- IP allowlisting enforced via middleware with bypass for anonymous intake endpoints
- Audit log captures all required event types with passing manual audit (every API call to a `[Authorize]` endpoint produces an audit entry)
- DMS bidirectional integration with at least one partner (IDS or Lightspeed) in production at a Premium design partner
- DMS reconciliation dashboard surfaces real discrepancies and supports manual resolution
- Anonymization pipeline operational with variable k-anonymity enforced (k=5 generic up to k=25 OEM-relevant)
- Tiered industry benchmarking (basic at Solo, advanced at Pro, full custom at Premium) live with rate limiting enforced
- Verification gate fully operational with verification queue averaging <72 hour completion time
- Premium tier banded per-location billing operational ($119/$109/$99 by 1-9/10-24/25-49 loc) with each Stripe invoice reflecting correct band-applicable rate
- Premium tier monthly SR cap (1,000/loc/mo) enforced, returning HTTP 402 on breach
- Premium banded implementation fees billed correctly at contract signing ($5K/$7.5K/$10K by location band)
- Priority support tier add-on operational ($500/mo opt-in for Pro and Premium customers via dashboard)
- Pro→Premium transition discount mechanic (50% off first 3 months, subscription only) functional via Stripe coupon
- Auth0 Free → Auth0 Organizations migration tested and documented for Solo/Pro → Premium upgrade path
- ToS, MSA, and DPA templates with liquidated damages clauses reviewed by counsel
- 1 paying Premium customer signed
- Asset ledger ≥ 10,000 events with ≥ 95% taxonomy adherence

### 3.4 Phase 3 Ship Criteria (Enterprise Scale tier and OEM pilot readiness)

- 3+ paying Premium customers
- Multi-DMS support (both IDS and Lightspeed) validated at a Premium customer
- Custom analytics builder in production
- 24/7 support model operational (Critical support tier active for ES customers)
- SOC 2 Type I attestation obtained
- First Enterprise Scale customer signed (sales-led)
- First OEM pilot under contract
- Asset ledger ≥ 50,000 events with ≥ 5% installed-base coverage of one major OEM
- **Per-tenant SLA configuration framework operational (FR-SLA-01 through FR-SLA-06) with first ES customer's custom SLA terms enforced and quarterly conformance reporting delivered**
- **OEM aggregate definition and contribution measurement system operational (FR-OEM-01 through FR-OEM-06) — required if OEM pilot includes revenue-share clause; deferred if first OEM pilot does not trigger revenue-share threshold**
- **Backend engineer #2 onboarded and absorbing dedicated solutions engineer FTE allocation per ES customer (FR-ES-011)**

---

## 4. Personas and Interaction Surfaces

### 4.1 Personas

| Persona | Auth | Primary Surface | Key Actions | Tier |
|---|---|---|---|---|
| RV Owner | Anonymous (magic-link only) | `Blazor.Intake` WASM | Submit intake, check status | All tiers |
| Service Advisor | Auth0 JWT | `Blazor.Manager` | Search/filter SRs, update status, add notes | All tiers |
| Service Manager | Auth0 JWT | `Blazor.Manager` | Queue management, batch outcomes (Service Board UI is Pro+) | All tiers |
| Regional Manager | Auth0 JWT | `Blazor.Manager` | `regionTag`-scoped cross-location view, regional analytics | **Professional+** |
| Corporate Admin | Auth0 JWT | `Blazor.Manager` | User management, all locations, all analytics, audit log query | **Premium+** (Solo/Pro use `dealer:owner` for similar duties) |
| Technician | Auth0 JWT | `MAUI.Tech` | View assigned jobs, record Section 10A, photo capture | All tiers (unlimited at every tier) |
| Platform Admin | Auth0 JWT | Direct API / future admin UI | Tenant provisioning, verification queue, anomaly review, Enterprise Scale contract setup, global lookups | Internal |

The role data model (Auth0 roles, ClaimsService) is unchanged across tiers. The difference is which roles can be *assigned* and which UI surfaces are *exposed* per tenant tier.

### 4.2 Intake URL Structure

```
https://rvintake.com/{locationSlug}          — Location intake portal
https://rvintake.com/status/{token}           — Customer status page
https://rvintake.com/                         — Platform landing (dealer search)
```

---

## 5. Functional Requirements

### 5.1 Customer Intake Flow

**FR-INTAKE-01 — Anonymous access**
The intake wizard at `/intake/{locationSlug}` MUST require no authentication. The `POST api/intake/{slug}/service-requests` endpoint MUST be annotated `[AllowAnonymous]`. Any authenticated call attempting to bypass rate limiting MUST be rejected.

**FR-INTAKE-02 — Intake orchestration sequence (7 steps)**
On receipt of a valid `ServiceRequestCreateRequestDto`, the API MUST execute the following atomically-ordered sequence:
1. Resolve `GlobalCustomerAcct` by email (create if absent)
2. Resolve or create `CustomerProfile` within `tenantId`
3. Resolve asset ownership (deactivate prior owner if VIN transferred)
4. Create `ServiceRequest` with embedded customer snapshot, AI categorization, and technician summary
5. Append `AssetLedgerEntry` (write-once, mandatory; persistent failure is a P1 alert per FR-LEDGER-01)
6. Update linkages (increment request count, stable magic-link token — generated once, reused; regenerated only when absent or expired)
7. Fire-and-forget customer notifications: (a) ACS email/SMS via `INotificationService` if `NotificationConfig.Provider = RvsNative`; (b) outbound webhook fire if `NotificationConfig.WebhookUrl` is configured (regardless of provider)

Steps 1–6 MUST complete before returning `201`. Step 7 MUST NOT block the response.

**FR-INTAKE-02A — Tier-based intake quota enforcement**
Before Step 1, the API MUST check `TenantConfig.BillingConfig.CurrentMonthSrCount` against the per-location cap derived from tier (Solo: 300/loc/mo, Pro: 1,000/loc/mo, Premium and Enterprise Scale: unlimited). If the cap is reached for a Solo or Pro tenant, the API MUST return `402` with `ProblemDetails` body `{ type: "rvs:tier-quota-exceeded", title: "Monthly intake limit reached", status: 402, detail: "This dealership is temporarily unable to accept new requests. Please contact the service center directly." }`. The customer-facing message MUST NOT mention pricing or tiers. On successful intake, the counter MUST increment atomically (Cosmos patch on `TenantConfig`). Counter resets on the first day of each calendar month via scheduled function.

**FR-INTAKE-03 — VIN decoding**
The intake API MUST call the NHTSA vPIC API (`https://vpic.nhtsa.dot.gov/api/`) to decode make, manufacturer, model year, and asset type from a submitted VIN. Decoded values MUST be stored in `AssetInfoEmbedded`. On NHTSA API failure or invalid VIN, the intake submission MUST still succeed with partial asset info (customer-supplied make/model/year).

**FR-INTAKE-04 — Attachment handling**
- Accepted MIME types: `image/jpeg`, `image/png`, `video/mp4`, `audio/m4a`, `audio/wav`
- Maximum attachments per SR: 10
- Maximum file size: configurable per location in `IntakeFormConfigEmbedded.MaxFileSizeMb` (default: 25 MB)
- Upload mechanism: customer-facing intake uses direct Azure Blob SAS upload (customer → Blob Storage, binary never transits API)
- Authenticated staff upload (technicians, advisors): API receives binary, streams to Blob
- Access: time-limited read SAS URIs (1-hour expiry), generated per request, never stored

**FR-INTAKE-05 — Diagnostic questions (AI wizard)**
- `POST api/intake/{slug}/diagnostic-questions` MUST call `ICategorizationService.SuggestDiagnosticQuestionsAsync` returning 2–4 contextual questions
- If Azure OpenAI is unavailable: return hardcoded fallback questions for the requested category (no 500)
- `diagnosticResponses` submitted with the SR MUST be embedded in `ServiceRequestEmbedded.DiagnosticResponses`
- The categorization step (Step 4) MUST use `diagnosticResponses` if present to improve accuracy

**FR-INTAKE-05A — Description-first category suggestion**
- `POST api/intake/{slug}/ai/suggest-category` MUST call `ICategorizationService.CategorizeAsync` using the reviewed issue description text.
- The endpoint MUST return a typed AI envelope response (`AiOperationResponseDto<IssueCategorySuggestionResultDto>`) including `confidence`, `provider`, `warnings`, and `correlationId`.
- The suggested category MUST be treated as assistive only. The intake UI MUST allow customer override before submission.
- Submit-time orchestration MUST still perform final categorization as a defense-in-depth check.

**FR-INTAKE-08 — Capability assessment at Step 5 → Step 6 boundary**
- `POST api/intake/{locationSlug}/assess-capabilities` MUST accept `CapabilityAssessmentRequestDto { issueDescription (1..2000 chars, required), issueCategory? }` and return `CapabilityAssessmentResponseDto { matched, issueCategory, requiredCapabilities[], missingCapabilities[], locationPhone? }`.
- The endpoint MUST be `[AllowAnonymous]` and MUST be subject to the same per-IP rate-limit policy as the other anonymous intake AI endpoints.
- The orchestration MUST: (a) resolve the location via `slugLookup`; (b) call `ICategorizationService` against `issueDescription` unless `issueCategory` is supplied; (c) translate the resolved category to a set of capability codes via the deterministic `IssueCategoryCapabilityMap`; (d) compare against `Location.EnabledCapabilities`; (e) return `matched = true` when every required code is enabled OR when `requiredCapabilities` is empty (unmapped category, e.g., "General"); (f) populate `locationPhone` from the location's contact info so the Intake UI can render it in the customer-facing alert.
- The endpoint MUST always return `200 OK` with a populated `CapabilityAssessmentResponseDto`. Capability mismatch is a business outcome, not an error. AI categorization failures MUST fall back to the rule-based categorizer (already required by FR-INTAKE-05); if both fail, the response MUST set `matched = true` so capability assessment never blocks Step 6.
- The endpoint MUST NOT persist any customer data, MUST NOT mutate Cosmos state, and MUST NOT log free-text issue descriptions.
- The Intake wizard MUST call this endpoint between Step 5 (Issue Description) and Step 6 (Review and Submit). When `matched = false`, Step 6 MUST render a non-blocking alert at the top: *"This location isn't typically able to help with this kind of issue. Please contact the service center directly to confirm at {locationPhone}."* The alert MUST NOT prevent submission.

**FR-INTAKE-06 — Returning customer prefill**
On customer submission with a known email address, the intake API MUST:
- Return `isReturningCustomer: true` and `priorRequestCount` in the 201 response
- Prefill first name, last name, and phone in the intake form (fetched via `GET api/intake/{slug}?token={magicLinkToken}`)
- Offer known active VINs for one-tap selection (from `GlobalCustomerAcct.AssetsOwned`)

**FR-INTAKE-07 — Slug validation**
If `{locationSlug}` does not match a record in `slugLookup`, the API MUST return `404` with a `ProblemDetails` body. If the matched tenant's `TenantConfig.AccessGate.IsEnabled = false` or status is `Disabled`, the API MUST return `403`.

### 5.2 Customer Status Page

**FR-STATUS-01 — Magic-link validation**
`GET api/status/{token}` MUST:
1. Parse the email-hash prefix from the token to derive the partition key
2. Execute a single-partition point read on `globalCustomerAccts`
3. Validate token matches, is not expired, and `isActive = true`
4. Return `404` for invalid/missing tokens; `410 Gone` for expired tokens

**FR-STATUS-02 — Cross-dealer SR visibility**
The authenticated response MUST return all active service requests linked to the customer's `GlobalCustomerAcct` across all corporations. Response fields per SR: location name, dealership name, status, issue category, submission date, last updated date. No PII beyond first name and asset summaries MUST be exposed.

**FR-STATUS-03 — Rate limiting**
`api/status/{token}` MUST be limited to 10 requests/minute per IP. `api/intake/{slug}/service-requests` MUST be limited to 20 requests/minute per IP. Rate limit responses MUST return `429` with a `Retry-After` header.

### 5.3 Service Manager Dashboard

**FR-DASH-01 — SR search**
`POST api/dealerships/{id}/service-requests/search` MUST support filtering by: `status`, `issueCategory`, `locationId`, `assignedTechnicianId`, `assignedBayId`, `assetId`, `keyword` (customer name, VIN, description snippet), `dateFrom`/`dateTo`, `priority`. MUST return `PagedResult<ServiceRequestSummaryResponseDto>` with page size capped at 100.

**FR-DASH-02 — Batch outcome**
`PATCH api/dealerships/{id}/service-requests/batch-outcome` MUST apply a shared repair outcome to up to 25 service requests in one call. MUST validate all SR IDs belong to the caller's tenant before writing.

**FR-DASH-03 — Service Board updates**
**MVP:** The `Blazor.Manager` Service Board MUST use long polling (periodic `GET` or `POST search` calls) to detect status-change events. Polling interval MUST be configurable (default: 5 minutes). On technician SR update, the Service Board MUST reflect the change within one polling cycle.
**vNEXT:** A dedicated SignalR hub MUST replace long polling to push status-change events to all connected `Blazor.Manager` sessions within a tenant. On technician SR update, all connected sessions for that tenant MUST receive the update within 5 seconds.

**FR-DASH-04 — Analytics**
`GET api/dealerships/{id}/analytics/service-requests/summary` MUST return `ServiceRequestAnalyticsResponseDto` covering: total requests, by status, by category, by location, top failure modes, top repair actions, average repair time, top parts used, average days to complete. Supports optional `?from`, `?to`, `?locationId` query parameters.

### 5.4 Technician Mobile App

**FR-TECH-01 — Offline sync**
The `MAUI.Tech` app MUST queue failed `PUT api/dealerships/{id}/service-requests/{srId}` requests in SQLite when offline. On reconnect, queued requests MUST replay sequentially. Optimistic concurrency via `updatedAtUtc` — if the server version is newer, a conflict MUST be surfaced to the technician (not silently overwritten).

**FR-TECH-02 — VIN/QR scan to job open**
Scanning a VIN barcode or QR code MUST resolve to the matching `ServiceRequest` via `POST api/dealerships/{id}/service-requests/search` with `assetId` filter. The first matching open SR MUST open automatically.

**FR-TECH-03 — Section 10A fields with strict taxonomy enforcement**
The `PUT api/dealerships/{id}/service-requests/{srId}` endpoint MUST accept `ServiceEventEmbedded` fields: `ComponentType`, `FailureMode`, `RepairAction`, `PartsUsed`, `LaborHours`, `ServiceDateUtc`. Technicians with `dealer:technician` role MUST be able to update Section 10A fields without changing SR status.

**Strict taxonomy enforcement (v2.0 change):** `ComponentType`, `FailureMode`, and `RepairAction` MUST be validated against the active taxonomy version stored in `lookupSets`. Submissions containing values not present in the active controlled vocabulary MUST be rejected with `400 Bad Request` and a `ProblemDetails` body identifying the offending field(s) and the closest valid alternatives. Free-text override is NOT permitted. Technicians encountering uncategorizable cases MUST select `other-uncategorized` and supply a free-text supplement; the supplement is queued for taxonomy review and does NOT enter the structured ledger fields.

**Taxonomy versioning:** Each `AssetLedgerEntry` MUST record the `TaxonomyVersion` under which Section 10A was captured. The active version is published platform-wide; rolling forward a tenant to a new version MUST be an explicit operation (not automatic).

**FR-TECH-03A — AI-assisted Section 10A classification (Wave 2, Enterprise)**
For Enterprise tenants, `POST api/service-requests/{srId}/ai/suggest-section10a` MUST call an AI provider (`ICategorizationService.SuggestSection10AAsync`) to recommend `ComponentType`, `FailureMode`, and `RepairAction` values from the active taxonomy. The endpoint MUST return `AiOperationResponseDto<Section10ASuggestionResultDto>` with confidence per field. Suggestions are advisory only — the technician's chosen taxonomy values are what the ledger records.

**FR-TECH-04 — Authenticated attachment upload**
`POST api/dealerships/{id}/service-requests/{srId}/attachments` MUST accept authenticated (Bearer) multipart uploads from dealer staff. The same file type and size constraints defined in FR-INTAKE-04 apply.

### 5.5 Tenant and Location Management

**FR-TENANT-01 — Onboarding**
`POST api/tenants/config` MUST create the initial `TenantConfig` document for a new tenant. MUST be restricted to `platform:admin` or first-time bootstrap (tenant has no existing config).

**FR-TENANT-02 — Access gate**
`TenantAccessGateMiddleware` MUST run on every authenticated request. It MUST reject requests from tenants whose `AccessGate.Status` is not `Active` with `403`. The `TenantConfig` read MUST be gateway-cached (≈ 0 RU after first read per cache window).

**FR-TENANT-03 — Location slug management**
On `POST api/locations` or `PUT api/locations/{id}` (slug rename), `ILocationService` MUST atomically: delete the old `slugLookup` entry, write the new one, then update the `Location` document. A stale slug MUST never resolve a valid intake route.

**FR-TENANT-04 — QR code generation**
`GET api/locations/{id}/qr-code` MUST return a QR code image encoding `https://rvintake.com/{locationSlug}`. Acceptable response formats: `image/png` (default), `image/svg+xml`. QR code MUST encode the full HTTPS URL.

**FR-TENANT-05 — Service capabilities (tenant master list and per-location enablement)**
- `TenantConfig.AvailableCapabilities` is the master list of service capabilities offered by the tenant. Each entry MUST carry: `Code` (stable, immutable, slug-style — e.g., `diesel-service`, `electrical`, `hvac`), `Name` (display, max 100 chars), `Description?` (max 500 chars), `SortOrder` (int), and `IsActive` (bool, default `true`). New tenants MUST be seeded with a sensible default starter list (≈14 RV-service entries — diesel service, body & collision repair, RV refrigerator, slide-out repair, roof repair, electrical, plumbing, HVAC, generator, warranty work, mobile / on-site, winterization, safety inspection, tire & wheel — defined by `ConfigMapper.DefaultCapabilities()`).
- `PUT api/tenants/config` MUST allow `tenant-config:update` callers to replace `availableCapabilities`. The service layer MUST reject changes that mutate an existing `Code` (codes are immutable). Soft-deletion MUST be performed by setting `IsActive = false`; hard removal of a code that is referenced by any `Location.EnabledCapabilities` MUST be rejected with `409 Conflict`.
- `Location.EnabledCapabilities` is a list of capability codes opted into by that location. Each entry MUST exist in the tenant's `AvailableCapabilities` (validated server-side on `POST/PUT api/locations`). An empty list is allowed and means "no capability-based filtering applies to this location" — the intake capability assessment treats such a location as a match for any issue.
- `LocationDetailDto` and `LocationSummaryDto` MUST surface `enabledCapabilities`. `TenantConfigResponseDto` MUST surface `availableCapabilities`.

**FR-TENANT-06 — Tier location surcharge tracking**
There is NO architectural location limit per tier (Solo, Pro, Premium, Enterprise Scale all support unlimited locations architecturally). However, `POST api/locations` MUST update `TenantConfig.BillingConfig.LocationCountForBilling` atomically. The banded billing job (FR-BILL-04) uses `LocationCountForBilling` to determine the applicable per-location rate at the start of each billing period. Pro band breakpoints: 1-9 / 10-24 / 25-49 loc. Premium band breakpoints: 1-9 / 10-24 / 25-49 loc. Enterprise Scale uses contract-negotiated per-location rate. Solo is flat $39/loc across all sizes.

**FR-TENANT-07 — Self-serve tenant signup**
`POST api/signup` MUST accept `TenantSignupRequestDto { corporationName, ownerEmail, ownerFirstName, ownerLastName, locationName, locationAddress, locationPhone, requestedSlug }` and:
1. Validate slug uniqueness against `slugLookup` (return `409` on conflict; suggest alternative slugs in response)
2. Create Auth0 user with verification-pending email (using Auth0 Management API)
3. Create `Dealership` document with `Tier = Free`
4. Create initial `Location` with provided info
5. Create `TenantConfig` with Solo tier defaults, `AccessGate.Status = Active`, `NotificationConfig.Provider = RvsNative`, `BillingConfig.Tier = Solo`, `BillingConfig.TrialEndUtc` set 30 days out
6. Create `slugLookup` entry mapping slug → tenantId/locationId
7. Send welcome email via ACS with verification link and dashboard URL

The endpoint MUST be `[AllowAnonymous]` and rate-limited to 5 requests/IP/hour (signup is rare; abuse is concerning). On any step failure, all completed steps MUST be rolled back (best-effort; use compensating writes since Cosmos has no multi-container transactions).

**FR-TENANT-08 — Notification provider configuration**
`TenantConfig.NotificationConfig` MUST contain:
- `Provider: NotificationProvider` enum — `RvsNative` (default), `KenectWebhook`, `Disabled`
- `WebhookUrl: string?` — optional outbound webhook target
- `WebhookSecret: string?` — HMAC signing secret (stored in Key Vault, not in Cosmos)
- `SuppressOutboundCustomerMessages: bool` — when true, RVS sends no customer-facing email/SMS regardless of provider; webhook still fires

`PUT api/tenants/config/notifications` MUST allow `tenant-config:update` callers to update these settings. Webhook URL changes MUST trigger a verification call (`POST` with `event: "verification"` payload; expects `200 OK` within 10s) before being persisted.

**FR-TENANT-09 — Outbound integration webhook**
On the following events, the API MUST fire a webhook POST to `TenantConfig.NotificationConfig.WebhookUrl` if configured:
- `serviceRequest.created` — at the end of intake orchestration
- `serviceRequest.statusChanged` — on PUT to SR with status delta
- `serviceRequest.advisorNoteAdded` — when a customer-facing note is added
- `serviceRequest.completed` — when status transitions to `completed` or `delivered`

Payload schema (JSON):
```json
{
  "event": "serviceRequest.created",
  "tenantId": "org_xxx",
  "locationId": "loc_xxx",
  "serviceRequestId": "guid",
  "occurredAtUtc": "2026-04-30T...",
  "customer": { "firstName": "Alex", "phoneE164": "+1...", "email": "..." },
  "magicLinkUrl": "https://rvintake.com/status/...",
  "templatedMessageBody": "string",
  "metadata": { "issueCategory": "slide-system", "assetSummary": "..." }
}
```

Each request MUST include header `X-RVS-Signature: sha256=<hmac>` computed over the raw body using `WebhookSecret`. Delivery MUST be fire-and-forget with retry-on-failure (3 retries with exponential backoff: 30s, 5min, 30min). Failures after final retry are logged and surfaced in the audit log (Enterprise) but MUST NOT block the originating action. Webhook delivery latency MUST be < 30s P99 for first-attempt success.

**FR-LEDGER-01 — Asset ledger write discipline**
Every successful intake submission MUST result in an `AssetLedgerEntry` write within the same orchestration. The write MAY be async (Step 5 of orchestration may queue rather than write inline if Cosmos throttling is encountered), but the entry MUST be persisted within 60 seconds of SR creation under normal conditions. Persistent ledger write failure (3 consecutive retry attempts fail) MUST raise a P1 alert and surface in App Insights as `AssetLedgerWriteFailed` event with full SR context.

The asset ledger schema MUST follow `RVS_data_moat.md` §2.3. Required fields at SR creation: `assetId`, `tenantId`, `serviceRequestId`, `assetType`, `manufacturer`, `model`, `modelYear`, `serviceDateUtc`, `issueCategory`, `dealerLocationId`, `geoState`, `taxonomyVersion`, `createdAtUtc`. Section 10A fields (`componentType`, `failureMode`, `repairAction`, `partsUsed`, `laborHours`) MAY be null at creation and MUST be populated via a dedicated "complete entry" path when the technician completes the work.

**FR-LEDGER-02 — Asset ledger enrichment endpoint**
`PATCH api/asset-ledger/{ledgerEntryId}/section10a` MUST accept Section 10A enrichment from authenticated technicians or advisors. Strict taxonomy enforcement (per FR-TECH-03) applies. The endpoint MUST be idempotent (multiple submissions of the same payload result in a single state). On enrichment, the entry's `updatedAtUtc` is set; the entry's `taxonomyVersion` MUST match the version active at enrichment time (not creation time) — this allows ledger entries to span taxonomy versions.

**FR-LEDGER-03 — No general-purpose update API**
The asset ledger is logically append-only. There MUST NOT be a general `PUT api/asset-ledger/{id}` endpoint that allows arbitrary mutation. Corrections to ledger entries MUST be expressed as new entries with `correctsLedgerEntryId` populated. The platform admin role has direct Cosmos access for emergency corrections; this is logged in the audit log.

### 5.6 Premium-Tier Functional Requirements

These FRs activate when `TenantConfig.Tier = Premium`. They are documented in detail in `RVS_Premium_PRD.md`; this section catalogs the API surface they introduce.

> Enterprise Scale tier (50+ locations, sales-led custom contracts) inherits all Premium FRs and adds: multi-DMS support, custom analytics builder endpoints, custom data export endpoints, and OEM data partnership coordination. Enterprise Scale endpoints are catalogued in `RVS_Premium_PRD.md` §5; the Technical PRD treats Enterprise Scale as a configuration superset of Premium for implementation purposes.

**FR-ENT-01 — Multi-location activation (Pro and above)**
Professional and Premium tenants have no location count limit. `POST api/locations` is unrestricted. Bulk import via `POST api/locations/bulk-import` (CSV body) MUST validate all rows before inserting any. Solo tier tenants are not architecturally limited but the dashboard does not surface multi-location UI.

**FR-ENT-02 — Cross-location queue**
`POST api/dealerships/{id}/service-requests/search` already supports `locationId[]` filter (FR-DASH-01). Enterprise UI surfaces this as a multi-select. Regional managers (`dealer:regional-manager` role with `regionTag` claim) MUST have their search results automatically filtered to permitted locations — enforced server-side in `IServiceRequestService.SearchAsync` via `ClaimsService.GetRegionTag()`.

**FR-ENT-03 — Cross-location asset history**
`GET api/assets/{assetId}/history` MUST return all `AssetLedgerEntry` documents for the given asset where `tenantId = caller's tenantId`. MUST NOT return entries from other tenants. Sorted by `serviceDateUtc` descending. Supports `?from`, `?to` query params.

**FR-ENT-04 — Audit log writes**
All authenticated requests MUST emit an audit event to the `auditLog` container via `IAuditLogService`. Event taxonomy:
- `auth.*` — login, logout, mfa-challenge, failed-login
- `authorization.*` — permission-denied
- `data.read.*` — sr-view, asset-ledger-query, attachment-access, analytics-query, benchmarking-query
- `data.write.*` — sr-create, sr-status-change, section10a-update, user-create, user-role-change, settings-change
- `admin.*` — location-create, location-delete, integration-config-change

Entry shape: `{ id, tenantId, userId, eventType, resourceType, resourceId, action, timestamp, ipAddress, userAgent, correlationId, metadata }`. Partition key `/tenantId`. Retention: 7 years (configurable per tenant). Writes MUST be async and non-blocking; failure MUST log but not break the request.

**FR-ENT-05 — Audit log query**
`POST api/audit-log/search` MUST allow `dealer:corporate-admin` to query the audit log with filters on `eventType`, `userId`, `dateFrom`, `dateTo`, `resourceType`. Page size capped at 200. Export available via `POST api/audit-log/export` returning a SAS URL to a generated CSV/JSON archive (60-minute expiry).

**FR-ENT-06 — IP allowlisting**
`TenantConfig.AccessControl.IpAllowlist: string[]` (CIDR notation). When non-empty, authenticated requests from IPs outside the allowlist MUST return `403` with type `tenant-ip-restricted`. Anonymous intake endpoints (`api/intake/*`, `api/status/*`) MUST be excluded from allowlisting. Configuration via `PUT api/tenants/config/access-control`.

**FR-ENT-07 — SAML SSO (Auth0 Organizations)**
Enterprise tenants are provisioned as Auth0 Organizations. Per-organization SAML connection configuration is performed in Auth0 Dashboard or via Auth0 Management API. The RVS API does NOT implement SAML directly; ClaimsService is unchanged from Free. The `tenantId` claim is sourced from the Auth0 Organization ID (`org_xxx`) when the user authenticates via an Organization-scoped login.

**FR-ENT-08 — SCIM provisioning**
Enterprise tenants configure SCIM via Auth0's SCIM 2.0 endpoint exposed per-Organization. RVS does NOT implement SCIM directly. Group-to-role mapping is configured at the Auth0 Organization level.

**FR-ENT-09 — DMS bidirectional integration**
For Enterprise tenants with `TenantConfig.DmsIntegration` configured, the platform MUST sync SRs bidirectionally. Architecture:
- Implementation behind `IDmsIntegrationProvider` interface with `IdsAstraIntegrationProvider` and `LightspeedIntegrationProvider` implementations
- RVS → DMS push: outbound on SR create/status-change, queued via Azure Storage Queue, processed by `DmsSyncBackgroundService`
- DMS → RVS pull: inbound webhook (where DMS supports) or scheduled pull (where it doesn't); writes to SR with `dmsLastSyncUtc` updated
- Per-location configuration (some dealer groups have mixed DMS); `Location.DmsIntegration` overrides tenant-level

The DMS reconciliation dashboard (`GET api/dms/reconciliation`) MUST surface drift between RVS and DMS state with manual resolution UI.

**FR-ENT-10 — Industry benchmarking API (tier-gated query depth + variable k-anonymity)**
`POST api/benchmarking/query` MUST accept benchmarking queries against the `industryDataset` container. The endpoint MUST:
1. Validate caller has appropriate role and tier (Solo: `dealer:owner` for pre-built dashboards only; Pro: custom queries via predefined templates; Premium: full custom query API; Enterprise Scale: full firehose per contract)
2. Validate `TenantConfig.BenchmarkingAccess.Verified = true` (verification gate per FR-VG-01) — if not verified, return `403` with type `tenant-not-verified`
3. Apply variable k-anonymity threshold per `RVS_data_moat.md` §4.3 (k=5 generic, up to k=25 for OEM-relevant queries) — if query result would expose data from fewer than the threshold k tenants, return aggregate-only or suppress with "insufficient data"
4. Audit-log the query to `auditLog` per FR-RATE-02 (queryParameters, kAnonymityThresholdApplied, resultRowCount, etc.)
5. Rate-limit per tier (Solo: 20/mo dashboard refreshes; Pro: 200/mo queries; Premium: 2,000/mo queries; Enterprise Scale: negotiated)
6. Reject queries that violate tier-specific depth restrictions (Solo: no custom queries, no drill-down beyond category, no model-year, no geographic finer than region, no date range narrower than 90 days)

Supported query types (initial): `rectComparison`, `topFailureModes`, `laborHoursDistribution`, `seasonalPatterns`. Phase 2 ship adds Premium tier `componentFailureRateByModel`, `failureCorrelations`, `warrantyClaimsBenchmark`. Each returns aggregated statistics with variable k-anonymity guarantees enforced at the query engine layer.

### 5.7 Billing, Trial, and Subscription Management (new in v3)

**FR-BILL-01 — Stripe customer and subscription provisioning**
`POST api/signup` (per FR-019 in PRD) MUST create a Stripe Customer and store payment method during signup, and create a Subscription with `trial_end` set to 30 days from signup. `TenantConfig.BillingConfig` MUST persist:
- `StripeCustomerId`
- `StripeSubscriptionId`
- `Tier: Solo | Professional | Premium | EnterpriseScale`
- `BillingPeriodStart`, `BillingPeriodEnd`
- `TrialEndUtc`
- `LocationCountForBilling`
- `IncludedUsers` (Premium only; default 10)
- `ActiveUserCount` (Premium only; non-technician only)
- `CurrentMonthSrCount`

**FR-BILL-02 — Tier transition endpoints**
- `POST api/billing/upgrade-to-professional` — transitions Stripe subscription from Solo flat $39/loc to Pro banded pricing ($79/$69/$59 by 1-9/10-24/25-49 loc per FR-BILL-04); applies Solo→Pro upgrade with no transition discount (Solo→Pro is the standard upgrade path, not a discounted transition)
- `POST api/billing/upgrade-to-premium` — requires Pro tier active; transitions Stripe subscription to Premium banded pricing ($119/$109/$99 by 1-9/10-24/25-49 loc per FR-BILL-04); kicks off Auth0 Free → Auth0 Organizations migration job; applies Pro→Premium transition discount (50% off first 3 months per FR-BILL-08); generates implementation fee invoice per FR-BILL-09 banded by location count
- `POST api/billing/downgrade` — applies on next billing period boundary, never mid-period; downgrade Premium → Pro removes Auth0 Organizations identity and reverts to `app_metadata` scoping (one-way migration documented; advise customers to consider data implications)
- `POST api/billing/upgrade-to-enterprise-scale` — sales-led; not self-serve; admin-gated endpoint that records the negotiated contract terms ($150-$300/loc range; Critical support bundled), generates implementation fee invoice per FR-BILL-09, applies Premium→Enterprise Scale transition discount (50% off first 3 months per FR-BILL-08)
- `POST api/billing/support-tier-upgrade` — Pro and Premium tenants opt into Priority support ($500/mo); proration handled per FR-BILL-07; takes effect on next billing period boundary

**FR-BILL-03 — Trial end conversion**
A scheduled job runs daily checking for trials ending within 7 days, 1 day, and trials that have ended. Notifications:
- T-7 days: dashboard banner + email reminder
- T-1 day: dashboard banner + email reminder
- T-0: trial ends; first invoice generated by Stripe; payment captured
- T+1 to T+3: failed-payment grace period (dashboard alert, email retry); tenant stays accessible
- T+4: tenant moves to disabled access gate; HTTP 402 returned for all authenticated requests except billing settings

**FR-BILL-04 — Volume-banded per-location billing**
Monthly billing job computes per-location subscription charges using volume bands per `RVS_PRD.md` FR-P-006 and `RVS_Premium_PRD.md` FR-PR-012:

- **Solo:** flat $39/loc, all sizes. Stripe submits `LocationCount × $39`.
- **Professional:** banded by `LocationCount`:
  - 1–9 loc: $79/loc
  - 10–24 loc: $69/loc
  - 25–49 loc: $59/loc
- **Premium:** banded by `LocationCount`:
  - 1–9 loc: $119/loc
  - 10–24 loc: $109/loc
  - 25–49 loc: $99/loc
- **Enterprise Scale:** custom per-contract per-location rate stored in `TenantConfig.BillingConfig.NegotiatedPerLocationRate` (range $150–$300); Stripe submits `LocationCount × NegotiatedPerLocationRate`.

Bands are NOT graduated — a 12-location Pro customer pays 12 × $69, not (9 × $79) + (3 × $69). Band assignment uses `LocationCountForBilling` snapshot at billing period start. Mid-period location additions/removals adjust at next period boundary.

Tenant tier transitions (Solo→Pro, Pro→Premium, Premium→Enterprise Scale) propagate to the next monthly Stripe invoice; current period bills at prior tier.

**FR-BILL-05 — (removed in v3.3)**
Per-user surcharge metering for Premium tier was specified in v3.0 but removed in v3.3 with the elimination of per-user pricing. All users (technicians, advisors, managers, owners) are unlimited at every tier per `RVS_Context.md` §2.1 and `RVS_Premium_PRD.md` FR-PR-013.

**FR-BILL-06 — Tier access gate enforcement**
`TenantAccessGateMiddleware` MUST return `HTTP 402` when:
- Subscription is `past_due` beyond the 3-day grace period
- Trial ended without successful payment capture
- Tenant manually disabled by `platform:admin`

For Solo and Pro tier specifically, MUST return `HTTP 402` on intake submission when the tenant has reached its monthly SR cap (Solo: 300/loc/mo; Pro: 600/loc/mo). For Premium, return `HTTP 402` at 1,000/loc/mo cap. Customer-friendly message: *"This dealership is temporarily unable to accept new requests. Please contact the service center directly."* Enterprise Scale has no SR cap.

**FR-BILL-07 — Support tier add-on billing (Phase 2)**
Pro and Premium tenants may opt into Priority support tier via dashboard self-service. Selection writes to `TenantConfig.BillingConfig.SupportTier` (`Standard | Priority | Critical`). Monthly billing job adds:
- Priority: $500/mo (line item: "Priority Support")
- Critical: bundled with Enterprise Scale; no separate line item; not available as Pro/Premium add-on

Toggle of support tier mid-period prorates per Stripe metered usage. Tenant downgrade from Priority to Standard takes effect at end of billing period (no mid-period refund).

**FR-BILL-08 — Transition discount mechanics**
Pro→Premium and Premium→Enterprise Scale upgrades trigger a 50%-off-first-3-months promotional discount applied via Stripe coupon. Implementation:
- Discount applies to subscription line items only (per-location subscription); NOT to support add-ons, NOT to implementation fees
- Coupon auto-generated at upgrade time; expires after 3 monthly billing cycles
- Customer dashboard shows "Promotional pricing through [date]"
- Discount is internal sales lever; not in published pricing
- Cannot be combined with other promotional discounts; replaces any prior promotional discount

Implementation detail: Stripe coupon with `percent_off=50` and `duration=repeating, duration_in_months=3` applied at subscription update.

**FR-BILL-09 — Implementation fee billing (Phase 2)**
Premium and Enterprise Scale tenants pay one-time implementation fee at contract signing per `RVS_Premium_PRD.md` FR-PR-014 and FR-ES-008. Stripe invoice generated separately from subscription; due net 30. Fees:

| Tier | Locations | Fee |
|---|---|---|
| Premium | 1–9 | $5,000 |
| Premium | 10–24 | $7,500 |
| Premium | 25–49 | $10,000 |
| Enterprise Scale | 50–99 | $15,000 |
| Enterprise Scale | 100–249 | $25,000 |
| Enterprise Scale | 250+ | $40,000 |

Implementation add-ons (additional DMS, custom data migration, custom analytics) priced separately on the same invoice. Implementation fees are NOT subject to transition discounts (FR-BILL-08) or annual prepay discount (15% off applies to subscription only).

### 5.8 OEM Revenue-Share Calculation (Enterprise Scale, Phase 3+)

Implements `RVS_Premium_PRD.md` FR-ES-007 — documented OEM revenue-share clause for Enterprise Scale customers whose data represents ≥15% of an OEM-licensed aggregate. All requirements in this section apply ONLY when (a) at least one OEM data licensing contract is active, AND (b) at least one Enterprise Scale tenant has the revenue-share clause activated in their MSA. Until both conditions are met, this section's requirements are deferred but the data foundations (FR-OEM-01) MAY be implemented earlier to avoid retroactive backfill.

**FR-OEM-01 — OEM aggregate definition and contribution measurement**
The platform MUST support defining named OEM-licensed aggregates as queries over `assetLedger` and `serviceRequests`. Each aggregate definition MUST capture:
- `aggregateId` (e.g., `oem_grand_design_2024`)
- `oemContractId` (links to the OEM contract record)
- Asset filter (e.g., `manufacturer = 'Grand Design'`, optionally narrowed by model year, model, region)
- Time window (e.g., trailing 12 months)
- Effective date range of the licensing contract
- Annual contract value (for revenue-share calculation)

Contribution measurement methodology MUST be a **count of unique SRs** within the aggregate's filter and time window, attributable to a tenant via the SR's `tenantId`. The methodology is fixed at contract signing and stored in the OEM aggregate definition; changes mid-contract require both customer and OEM written consent.

**FR-OEM-02 — Quarterly contribution calculation job**
A scheduled job MUST run on the first business day after each calendar quarter end. For each active OEM aggregate:
1. Compute the SR count contributed by each ES tenant during the quarter (filter applied)
2. Compute total SRs in the aggregate during the quarter
3. Compute each tenant's `contributionPercentage = tenantSrCount / totalAggregateSrCount`
4. For each tenant whose `contributionPercentage >= 0.15`, calculate `quarterlyShare = (annualContractValue / 4) × tenantRevenueSharePercent` (where `tenantRevenueSharePercent` is a per-tenant value in the 0.05–0.10 range stored in the ES contract record)
5. Persist results in a new `oemRevenueShareEntries` Cosmos container, partitioned by `/tenantId`, with documents shape `{ tenantId, oemAggregateId, quarter, srCount, contributionPercentage, quarterlyShareAmount, status: pending|approved|paid|disputed }`

The job MUST log all calculations with full audit trail (input SRs by ID, methodology version, contract version) sufficient for customer dispute resolution.

**FR-OEM-03 — Revenue-share approval workflow**
Calculated revenue-share entries MUST be reviewed by `platform:admin` before payment. The platform MUST surface a `GET api/platform/oem-revenue-share/pending` endpoint listing all `pending` entries with full audit detail. Admin actions:
- `POST api/platform/oem-revenue-share/{id}/approve` — marks entry approved, queues payment
- `POST api/platform/oem-revenue-share/{id}/dispute` — marks entry disputed, requires written explanation, blocks payment until resolution
- `POST api/platform/oem-revenue-share/{id}/adjust` — admin override of calculated amount with audit trail (requires written justification)

Approved entries MUST trigger a Stripe invoice or ACH payment from RVS to the customer (mechanism TBD, recorded as accounts-payable to the customer; final mechanism decided in Phase 3 based on operational considerations and dispute-handling requirements).

**FR-OEM-04 — Customer-facing revenue-share dashboard**
ES tenants MUST have access to a dashboard surface showing:
- Active OEM aggregates their tenant contributes to
- Their contribution percentage by quarter (current quarter estimate, prior quarter actual)
- Pending and paid revenue-share amounts by quarter
- Anonymized aggregate-level metrics (total aggregate SR count, top-3 contributors by anonymized rank — never naming other tenants)
- Methodology documentation and contract reference

Endpoint: `GET api/dealerships/{tenantId}/oem-revenue-share/summary` (Bearer auth, requires ES tier and revenue-share clause active).

**FR-OEM-05 — Cancellation/forfeiture handling**
Per FR-ES-007, revenue-share clause is contingent on continuous active subscription. On ES subscription cancellation:
- Already-earned amounts in `approved` or `paid` status are paid out under contract terms (no clawback)
- Amounts in `pending` status as of cancellation date follow contract MSA exhibit (default: paid out at next quarterly run if attributable to pre-cancellation activity; not paid for any period after cancellation effective date)
- Future revenue-share accrual stops at cancellation effective date

**FR-OEM-06 — Anti-gaming measures**
The platform MUST flag for admin review any tenant whose SR submission rate increases >2.5× over their trailing 6-month baseline within 30 days of a major OEM contract event (signing, renewal, expansion). Flagged tenants are surfaced in the admin dashboard for manual review. Per FR-ES-007 contract language, RVS reserves the right to exclude SRs that appear to be artificially generated; this FR provides the operational mechanism to identify candidate cases.

### 5.9 Custom SLA Monitoring (Enterprise Scale, Phase 3+)

Implements `RVS_Premium_PRD.md` FR-ES-012 — custom SLA negotiation for Enterprise Scale customers including higher uptime targets, service credits, named incident response procedures, and custom maintenance windows.

**FR-SLA-01 — Per-tenant SLA configuration**
The platform MUST support per-ES-tenant SLA configuration stored in `TenantConfig.SlaConfig` with fields:
- `uptimeTargetPercent` (default 99.9; ES contracts may negotiate up to 99.99)
- `serviceCreditsEnabled` (bool)
- `serviceCreditTiers` (list of `{ thresholdBelowTargetPercent, creditPercentOfMonthlyFee }`; default for ES contracts: `[(0.1, 5), (0.5, 25), (1.0, 50)]`, capped at 50% — negotiable up to 100%)
- `maintenanceWindow` (cron-style schedule; default: Sunday 2-6am customer local time; ES may negotiate alternative windows)
- `p1ContactEscalationChain` (ordered list of named RVS engineering contacts for P1 incidents)
- `postIncidentReviewSlaBusinessDays` (default 5 for ES contracts)

`SlaConfig` is read-only via the customer-facing dashboard; modifications require `platform:admin` approval and contract amendment.

**FR-SLA-02 — Uptime measurement methodology**
The platform MUST compute monthly uptime per tenant using the existing `/health` endpoint availability test data (per RVS Technical PRD §6.4) plus tenant-specific availability checks. Uptime calculation:
```
uptime_percent = (total_minutes_in_month - downtime_minutes) / total_minutes_in_month × 100
```
Downtime is defined as: any 5-minute window during which `/health` returns 503 OR any tenant-specific synthetic transaction (intake submission, dashboard load, technician app sync) fails for >50% of attempts. Maintenance-window outages within the contracted maintenance window are EXCLUDED from downtime calculation.

**FR-SLA-03 — Service credit calculation and issuance**
A scheduled job MUST run on the 5th business day of each month to compute prior-month uptime per ES tenant. For each tenant whose `uptime_percent` is below `uptimeTargetPercent`:
1. Calculate `breachAmount = uptimeTargetPercent - uptime_percent`
2. Apply `serviceCreditTiers` step function to determine credit percentage
3. Compute `creditAmount = monthlySubscriptionFee × creditPercent`
4. Generate Stripe credit memo for that amount applied to next month's invoice
5. Email tenant's primary billing contact + dedicated success manager + dedicated solutions engineer with breach summary, credit amount, and remediation steps

Service credit issuance is automatic and does not require admin approval; admin override is available via `POST api/platform/sla/{tenantId}/credit-adjust` for cases where the breach was caused by customer-side issues.

**FR-SLA-04 — P1 incident response and escalation**
For ES tenants with `p1ContactEscalationChain` configured, the platform's incident management system MUST:
- Auto-page the configured escalation chain for any P1 incident affecting that tenant
- Surface incident details in a dedicated ES incident channel (separate from general support tier escalation)
- Track time-to-acknowledge, time-to-mitigate, and time-to-resolve per incident
- Flag any P1 incident exceeding the `Critical support` 1-hour response SLA for executive review

P1 incident definition: any production-impacting incident affecting service availability OR data integrity. Severity assignment is initial responder responsibility, reviewable by dedicated solutions engineer.

**FR-SLA-05 — Post-incident review obligation**
Within `postIncidentReviewSlaBusinessDays` of any P1 incident affecting an ES tenant, RVS MUST deliver:
- Written incident summary (timeline, root cause, mitigation, prevention)
- Joint review meeting with customer's named technical contact (customer's option)
- Mutual NDA-protected document (confidential customer-specific details, not published in public post-mortems)
- Formal corrective action plan with target completion dates

The dedicated solutions engineer (per `RVS_Premium_PRD.md` FR-ES-011) owns delivery of this output.

**FR-SLA-06 — Quarterly SLA conformance reporting**
The platform MUST produce a quarterly SLA conformance report per ES tenant including:
- Monthly uptime by month
- Service credits issued (count, total amount)
- P1 incident summary (count, types, resolution times)
- Maintenance window adherence
- Trend vs. prior quarters

Report delivered as a downloadable PDF and reviewed in the quarterly business review (per FR-PR-017 / FR-ES-005).

### 5.10 Self-Service User Provisioning (new in v3)

**FR-USER-01 — Auth0 Management API integration**
The platform MUST integrate with Auth0 Management API for self-service user lifecycle:
- `POST api/users` — create user with email, name, role, location assignments; calls Auth0 `POST /api/v2/users` and sends Auth0 invitation email
- `PUT api/users/{userId}` — update role, location assignments, name; updates Auth0 user `app_metadata` and `roles` accordingly
- `DELETE api/users/{userId}` — deactivate user; revokes Auth0 access; logs deactivation to audit log

The `dealer:owner`, `dealer:corporate-admin`, and `dealer:manager` (within their location only) roles MAY invoke these endpoints. Other roles MUST receive `403`.

**FR-USER-02 — Bulk user import (Pro+)**
`POST api/users/bulk-import` accepts a CSV body with columns: email, firstName, lastName, role, locationIds (semicolon-separated). Validates all rows before any insert. Sends individual Auth0 invitations per row. Failure on any row reports per-row error but does not roll back successful inserts.

**FR-USER-03 — User activity tracking (audit/observability only)**
The platform MAY track `User.LastLoginUtc` (updated on authenticated requests, throttled to once per hour per user to avoid Cosmos write storm) for audit and observability purposes. **No longer used for per-user billing as of v3.3** — all users are unlimited at every tier. The field remains in the data model for: dormant-user identification (success engineer outreach), audit log enrichment, and potential future analytics. Implementation is OPTIONAL in Phase 1; can be deferred to Phase 2 if Phase 1 schedule pressure demands.

**FR-USER-04 — (removed in v3.3)**
Technician role exemption from per-user billing was specified in v3.0 but removed in v3.3 with the elimination of per-user pricing. All roles (including `dealer:technician`) are unlimited at every tier; no role-specific billing logic exists.

### 5.11 Verification Gate (new in v3)

**FR-VG-01 — Dealer verification submission**
`POST api/verification/submit` accepts dealer claim form payload:
```json
{
  "dotNumber": "string (optional)",
  "businessEin": "string",
  "dealerLicenseNumber": "string",
  "dealerLicenseState": "string (US state code)",
  "businessAddress": {
    "street1": "string",
    "city": "string",
    "state": "string",
    "postalCode": "string"
  },
  "supportingDocumentUrl": "string (optional, blob SAS URL)"
}
```

Creates a `VerificationRequest` document with status `pending` in a new `verificationQueue` Cosmos container (partition key `/tenantId`). Triggers internal admin notification.

**FR-VG-02 — Admin verification review**
`GET api/admin/verification-queue` (platform:admin only) lists pending verifications. `POST api/admin/verification-queue/{id}/approve` and `POST api/admin/verification-queue/{id}/reject` finalize the decision. On approval, sets `TenantConfig.BenchmarkingAccess.Verified = true` and `BenchmarkingAccess.VerifiedAtUtc` to now. On rejection, surfaces reason to tenant via dashboard banner.

**FR-VG-03 — Benchmarking gate enforcement**
All benchmarking endpoints (per FR-ENT-10) MUST check `TenantConfig.BenchmarkingAccess.Verified` and return `403 tenant-not-verified` if false. The check is via dedicated middleware applied to the `/api/benchmarking/*` route group.

**FR-VG-04 — Verification expiry and re-verification**
Verification status remains valid indefinitely under normal operations. Re-verification MAY be triggered by:
- Tenant ownership transfer
- Material change in business address or licensing
- Anomaly detection flag (per FR-ANOM-01)

### 5.10 Benchmarking Rate Limiting and Audit (new in v3)

**FR-RATE-01 — Per-tier monthly query quota**
A new `benchmarkingQueryCount` field on `TenantConfig.BillingConfig` tracks queries this billing period. On every `POST api/benchmarking/query`:
1. Increment the counter
2. Check against tier limit (Solo: 20, Pro: 200, Premium: 2,000, Enterprise Scale: per-contract)
3. If over limit, return `429 too-many-requests` with `Retry-After` header set to next billing period start
4. Counter resets at billing period rollover

**FR-RATE-02 — Benchmarking query audit log**
Every benchmarking query MUST emit an audit log entry per `RVS_data_moat.md` §6.4:
```json
{
  "id": "guid",
  "tenantId": "...",
  "userId": "...",
  "eventType": "data.read.benchmarking-query",
  "queryParameters": { /* full param object */ },
  "kAnonymityThresholdApplied": 5,
  "resultRowCount": 42,
  "queryRejectionReason": null,
  "timestamp": "...",
  "ipAddress": "...",
  "userAgent": "...",
  "correlationId": "..."
}
```

These entries are queryable by `dealer:corporate-admin` for their tenant (via FR-ENT-05) and by `platform:admin` for cross-tenant anomaly investigation.

**FR-ANOM-01 — Anomaly detection on benchmarking query patterns**
A scheduled job runs hourly against the benchmarking audit log applying detection rules per `RVS_data_moat.md` §6.5:
- Volume spike (10× tenant's 30-day baseline)
- Parametric scanning (sequential queries varying one parameter)
- Profile mismatch (queries about manufacturers/regions outside tenant's operational profile)
- Re-identification probes (narrow combined filters in sequence)
- Account-cluster patterns (multiple tenants with similar query signatures)
- Zero-or-low SR submissions but high benchmarking volume

Flagged tenants surface in admin review queue (`GET api/admin/anomaly-queue`). Manual investigation determines action: no-action, ToS reminder, throttle, suspend benchmarking, terminate per ToS, escalate to legal.


---

## 6. Non-Functional Requirements

### 6.1 Performance

| Requirement | Target | Condition |
|---|---|---|
| Intake submission (POST, cold) | P95 < 3 s | Includes all 7 orchestration steps plus AI call |
| Intake submission (POST, warm) | P95 < 1.5 s | Gateway-cached slug + returning customer |
| SR detail read (GET) | P99 < 200 ms | Single point read (1 RU) |
| SR search (POST search) | P95 < 500 ms | Up to 100 results, single-partition query |
| Magic-link status page | P99 API response < 500 ms | WASM SPA client-side route; API point read by token hash prefix, single-partition Cosmos read |
| Analytics query | P95 < 2 s | MVP volume ≤ 200 jobs/month |
| AI diagnostic questions | P95 < 1.5 s | GPT-4o-mini, 5 s timeout, fallback on breach |
| Bulk outcome patch (25 SRs) | P95 < 2 s | Sequential Cosmos writes, single partition |
| Slug resolution (cached) | < 1 ms effective latency | Gateway cache hit |

### 6.2 Scalability

- Cosmos `serviceRequests` autoscale floor: 400 RU; ceiling: 4,000 RU. Must sustain burst intake without throttling.
- Architecture MUST support ≥ 10,000 service requests/month per tenant with no schema or index changes.
- Multi-location query patterns (corporate admin view) MUST remain single-partition regardless of location count.
- Blob Storage MUST support concurrent multi-tenant attachment uploads without cross-tenant path collision.

### 6.3 Availability and Reliability

- API availability SLA (MVP): ≥ 99.5% measured monthly
- Confirmation email delivery: fire-and-forget; email failure MUST NOT cause intake submission failure
- Azure OpenAI unavailability: MUST fall back to rule-based categorization; intake MUST succeed
- NHTSA vPIC unavailability: MUST proceed with customer-supplied asset info; no 500 error
- Offline sync (`MAUI.Tech`): MUST queue locally and replay without data loss up to 72 hours offline

### 6.4 Observability

**Structured logging (every request):**
- `tenantId`, `locationId` (anonymized — no customer PII in logs)
- Request correlation ID (injected by middleware)
- HTTP status code and latency
- Cosmos RU consumed per operation (from SDK `RequestCharge`)

**App Insights telemetry:**
- Custom events: `IntakeSubmitted`, `MagicLinkValidated`, `SlugNotFound`, `TenantGateBlocked`, `AICategorizationFailed`, `AICategorizationFallback`, `AICategorySuggested`, `AICategorySuggestionFailed`
- Dependency tracking: Cosmos DB calls, Azure OpenAI calls, ACS Email calls, ACS SMS calls, NHTSA calls
- Availability tests: `/health` endpoint pinged every 5 minutes from two Azure regions

**Health endpoint:**
`GET /health` MUST return `200 OK` when dependencies are reachable, `503` when any critical dependency (Cosmos, Blob) is unavailable. MUST NOT require authentication.

### 6.5 Error Handling

All API errors MUST return RFC 7807 `ProblemDetails`:

```json
{
  "type": "https://rvserviceflow.com/errors/not-found",
  "title": "Resource not found",
  "status": 404,
  "detail": "Location slug 'bad-slug' was not found.",
  "traceId": "00-a1b2c3..."
}
```

`ExceptionHandlingMiddleware` MUST catch all unhandled exceptions and convert them to `ProblemDetails`. Inner exception messages MUST NOT be exposed in production responses. Stack traces MUST be logged server-side only.

Standard error codes:

| Scenario | Status | Type slug |
|---|---|---|
| Unknown slug | 404 | `location-not-found` |
| Expired magic-link token | 410 | `token-expired` |
| Invalid magic-link token | 404 | `token-invalid` |
| Tenant access gate blocked | 403 | `tenant-access-denied` |
| Rate limit exceeded | 429 | `rate-limit-exceeded` |
| Validation failure | 400 | `validation-error` |
| Unauthorized (no/invalid JWT) | 401 | `unauthorized` |
| Forbidden (wrong role/tenant) | 403 | `forbidden` |
| Internal / unhandled | 500 | `internal-error` |

---

## 7. Data Model Requirements

### 7.1 Entity Constraints

**ServiceRequest**

| Field | Type | Constraints |
|---|---|---|
| `id` | string | GUID, globally unique |
| `tenantId` | string | Partition key; set from `app_metadata.tenantId`; required |
| `locationId` | string | Prefix `loc_`; must exist in `locations` container |
| `status` | enum | `New` → `InProgress` → `Completed` or `Cancelled`; enforced by `StatusTransitions.cs` |
| `customer.email` | string | Lowercase, trimmed; required |
| `asset.assetId` | string | Format `{AssetType}:{Identifier}` (e.g., `RV:1ABC234567`); required |
| `createdAtUtc` | DateTime | Set by server; immutable after write |
| `updatedAtUtc` | DateTime | Updated by `MarkAsUpdated` on every write; used for optimistic concurrency |
| `diagnosticResponses` | array | Optional; max 10 responses; each has `questionText`, `selectedOptions[]`, `freeTextResponse?` |
| `attachments` | array | Max 10; each has `blobUri`, `fileName`, `contentType`, `sizeBytes` |

**CustomerProfile**

| Field | Type | Constraints |
|---|---|---|
| `tenantId` | string | Partition key |
| `email` | string | Unique within partition (unique key policy) |
| `globalCustomerAcctId` | string | Required; references `GlobalCustomerAcct.id` |
| `assetsOwned` | array | Embedded; each has `assetId`, `status` (`Active`/`Inactive`), `firstSeenAtUtc`, `lastSeenAtUtc`, `requestCount` |

**GlobalCustomerAcct**

| Field | Type | Constraints |
|---|---|---|
| `id` | string | Partition key = `/email`; normalized lowercase |
| `magicLinkToken` | string | Format: `base64url(SHA256(email)[0..8]):random_bytes`; generated once, reused; regenerated only when absent or expired |
| `magicLinkExpiresAtUtc` | DateTime | Default 90 days; configurable per tenant |
| `linkedProfiles` | array | Each has `tenantId`, `customerProfileId`, `locationId`, `locationName` |

**AssetLedgerEntry**

| Field | Type | Constraints |
|---|---|---|
| `assetId` | string | Partition key; format `{AssetType}:{Identifier}` |
| `serviceRequestId` | string | Unique within partition; cross-references `serviceRequests` |
| `section10A` | object | Optional at write time; enriched via change feed Phase 5–6. Fields: `componentType`, `failureMode`, `repairAction`, `partsUsed[]`, `laborHours`, `serviceDateUtc` |

**Location — `IntakeFormConfigEmbedded`**

| Field | Default | Constraints |
|---|---|---|
| `maxFileSizeMb` | 25 | Range: 1–100 |
| `maxAttachments` | 10 | Range: 1–10 |
| `acceptedFileTypes` | `[".jpg",".jpeg",".png",".mp4",".m4a",".wav"]` | Must be a subset of supported MIME types |
| `aiContext` | null | Optional; appended to Azure OpenAI system prompt; max 500 characters |
| `allowAnonymousIntake` | true | If false, intake requires a specific tenant-issued token (Phase 2) |

**Location — service capabilities**

| Field | Type | Constraints |
|---|---|---|
| `enabledCapabilities` | array of string | Each value MUST be a `Code` from the owning tenant's `TenantConfig.AvailableCapabilities`; validated server-side on create/update; empty array allowed (means "no capability filtering") |

**TenantConfig — `AvailableCapabilities` (master list)**

| Field | Type | Constraints |
|---|---|---|
| `code` | string | Stable slug, immutable after create; unique within `availableCapabilities`; 1–60 chars; lowercase, kebab-case |
| `name` | string | Display name; required; max 100 chars |
| `description` | string? | Optional; max 500 chars |
| `sortOrder` | int | Default 0; used to order the master list in admin and intake UIs |
| `isActive` | bool | Default `true`; soft-delete by setting to `false`; inactive entries remain valid for existing `Location.EnabledCapabilities` references but are hidden from new selection |

### 7.2 Status Transition Rules

Enforced by `StatusTransitions.cs`. Invalid transitions MUST return `409 Conflict`.

```
New → InProgress       (Advisor/Manager)
New → Cancelled        (Advisor/Manager)
InProgress → Completed (Advisor/Manager/Technician via Section 10A completion)
InProgress → Cancelled (Advisor/Manager)
Completed  → (immutable — no further transitions in MVP)
Cancelled  → (immutable — no further transitions in MVP)
```

### 7.3 Cosmos Container Configuration

| Container | Partition Key | RU Mode | Unique Keys | TTL | Tier |
|---|---|---|---|---|---|
| `serviceRequests` | `/tenantId` | Autoscale 400–4,000 | — | None | All |
| `customerProfiles` | `/tenantId` | Autoscale 400–1,000 | `[/tenantId, /email]` | None | All |
| `globalCustomerAccts` | `/email` | Manual 400 | — | None | All |
| `assetLedger` | `/assetId` | Autoscale 400–1,000 | `[/assetId, /serviceRequestId]` | None | All |
| `dealerships` | `/tenantId` | Manual 400 | — | None | All |
| `locations` | `/tenantId` | Autoscale 400–1,000 | `[/tenantId, /slug]` | None | All |
| `tenantConfigs` | `/tenantId` | Manual 400 | — | None | All |
| `lookupSets` | `/category` | Manual 400 | — | None | All |
| `slugLookup` | `/slug` | Autoscale 400–1,000 | — | None | All |
| `auditLog` | `/tenantId` | Autoscale 400–4,000 | — | 7 years (configurable) | Enterprise |
| `industryDataset` | `/datasetVersion` | Autoscale 400–4,000 | — | None | Platform-internal |

**`auditLog` notes:**
- Provisioned at Enterprise tenant onboarding; not present for Free tenants
- Append-only by convention; no API surface for delete or update
- Indexed on `eventType`, `userId`, `timestamp` (custom indexing policy to limit RU on writes)
- Per-tenant TTL configured via `TenantConfig.AuditLogRetentionDays` (default 7 years)

**`industryDataset` notes:**
- Written exclusively by the anonymization pipeline (Azure Function with change feed lease on `assetLedger`)
- Read access via `IBenchmarkingService` only; no direct API surface
- Partitioned by `datasetVersion` so taxonomy version migrations can produce parallel datasets
- All tenant-identifying fields stripped at write time (see `RVS_data_moat.md` §4.4)
- K-anonymity enforced at query time, not at write time (allows raw aggregation; suppression at query response)

**SDK connection mode:** `ConnectionMode.Gateway` for all reads. Enables Cosmos server-side caching on stable point-read containers (`slugLookup`, `tenantConfigs`, `lookupSets`). No application-layer cache required.

### 7.4 Blob Storage Layout

```
Container: rvs-attachments
  {tenantId}/
    {locationId}/
      {serviceRequestId}/
        {attachmentId}_{filename}.{ext}
```

- Path is deterministic and collision-free across tenants and locations
- SAS token expiry: 1 hour for read; 15 minutes for upload
- Retention: configurable per `TenantConfig.AttachmentRetentionDays` (default: unlimited in MVP)

---

## 8. API Specification

### 8.1 Authentication

All dealer-facing endpoints MUST require `Authorization: Bearer {jwt}` from Auth0.

JWT validation requirements:
- Issuer: `https://{auth0-domain}/`
- Audience: `https://api.rvserviceflow.com` (configurable in `appsettings.json`)
- Algorithm: RS256
- Claims required: `sub`, `https://rvserviceflow.com/tenantId` (from `app_metadata`), `roles[]`, `locationIds[]`

Customer-facing endpoints (`/intake/*`, `/status/*`) MUST be `[AllowAnonymous]`.

### 8.2 Full Route Inventory

**Tier legend:** F = Free, E = Enterprise, P = Platform admin, A = All

| Method | Route | Auth | Policy | Return | Notes | Tier |
|---|---|---|---|---|---|---|
| `GET` | `api/intake/{locationSlug}/config` | Anonymous | — | `IntakeConfigResponseDto` | Optional `?token=` for prefill | A |
| `POST` | `api/intake/{locationSlug}/diagnostic-questions` | Anonymous | — | `DiagnosticQuestionsResponseDto` | AI or fallback questions | A |
| `POST` | `api/intake/{locationSlug}/ai/suggest-category` | Anonymous | — | `AiOperationResponseDto<IssueCategorySuggestionResultDto>` | Assistive category prefill from description | A |
| `POST` | `api/intake/{locationSlug}/ai/transcribe-issue` | Anonymous | — | `AiOperationResponseDto<IssueTranscriptionResultDto>` | Speech-to-text and cleaned draft | A |
| `POST` | `api/intake/{locationSlug}/ai/refine-issue-text` | Anonymous | — | `AiOperationResponseDto<IssueTextRefinementResultDto>` | Cleanup endpoint for transcript/text | A |
| `POST` | `api/intake/{locationSlug}/ai/extract-vin` | Anonymous | — | `AiOperationResponseDto<VinExtractionResultDto>` | VIN extraction from captured image | A |
| `POST` | `api/intake/{locationSlug}/assess-capabilities` | Anonymous | — | `CapabilityAssessmentResponseDto` | Step 5 → Step 6 capability check (always 200) | A |
| `POST` | `api/intake/{locationSlug}/service-requests` | Anonymous | — | `201 ServiceRequestSummaryDto` / `402` | Full 7-step orchestration; Solo and Pro tiers return 402 if quota exceeded | A |
| `POST` | `api/intake/{locationSlug}/service-requests/{id}/attachments` | Anonymous | — | `201 AttachmentDto` | Customer photo upload | A |
| `GET` | `api/status/{token}` | Anonymous | — | `CustomerStatusResponseDto` | Cross-dealer SR summary | A |
| `POST` | `api/signup` | Anonymous | rate-limited 5/IP/hr | `201 TenantSignupResponseDto` | Self-serve Solo tier signup with Stripe trial (FR-TENANT-07) | A |
| `GET` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | `CanReadServiceRequests` | `ServiceRequestDetailDto` | | A |
| `POST` | `api/dealerships/{id}/service-requests/search` | Bearer | `CanSearchServiceRequests` | `PagedResult<ServiceRequestSummaryDto>` | Cross-location filter for Enterprise | A |
| `PUT` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | `CanUpdateServiceRequests` | `200 ServiceRequestDetailDto` / `400` | Status + Section 10A (strict taxonomy) + notes | A |
| `PATCH` | `api/dealerships/{id}/service-requests/batch-outcome` | Bearer | `CanUpdateServiceRequests` | `200 BatchOutcomeResponseDto` | Max 25 SRs | A |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}` | Bearer | `CanDeleteServiceRequests` | `204` | | A |
| `POST` | `api/dealerships/{id}/service-requests/{srId}/attachments` | Bearer | `CanUploadAttachments` | `201 AttachmentDto` | Authenticated upload | A |
| `GET` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | `CanReadAttachments` | `AttachmentSasDto` | SAS URL, 1-hour expiry | A |
| `DELETE` | `api/dealerships/{id}/service-requests/{srId}/attachments/{attId}` | Bearer | `CanDeleteAttachments` | `204` | | A |
| `GET` | `api/dealerships` | Bearer | `CanReadDealerships` | `List<DealershipSummaryDto>` | Tenant-scoped list | A |
| `GET` | `api/dealerships/{id}` | Bearer | `CanReadDealerships` | `DealershipDetailDto` | | A |
| `PUT` | `api/dealerships/{id}` | Bearer | `CanUpdateDealerships` | `200 DealershipDetailDto` | | A |
| `GET` | `api/locations` | Bearer | `CanReadLocations` | `List<LocationSummaryDto>` | Filtered by `locationIds` claim and `regionTag` | A |
| `GET` | `api/locations/{id}` | Bearer | `CanReadLocations` | `LocationDetailDto` | | A |
| `POST` | `api/locations` | Bearer | `CanCreateLocations` | `201 LocationDetailDto` / `402` | Free returns 402 if 1 location exists | A |
| `POST` | `api/locations/bulk-import` | Bearer | `CanCreateLocations` | `201 BulkLocationImportResultDto` | CSV body; validates all rows before insert | E |
| `PUT` | `api/locations/{id}` | Bearer | `CanUpdateLocations` | `200 LocationDetailDto` | Renames slug atomically | A |
| `GET` | `api/locations/{id}/qr-code` | Bearer | `CanReadLocations` | `image/png` | Encodes intake URL | A |
| `GET` | `api/dealerships/{id}/analytics/service-requests/summary` | Bearer | `CanReadAnalytics` | `ServiceRequestAnalyticsResponseDto` | `?from`, `?to`, `?locationId` | A |
| `GET` | `api/dealerships/{id}/analytics/cross-location` | Bearer | `CanReadAnalytics` | `CrossLocationAnalyticsResponseDto` | Multi-location dashboard data | E |
| `GET` | `api/dealerships/{id}/analytics/warranty-leakage` | Bearer | `CanReadAnalytics` | `WarrantyLeakageAnalysisDto` | Candidate flags for review | E |
| `GET` | `api/dealerships/{id}/analytics/sla` | Bearer | `CanReadAnalytics` | `SlaMonitoringDto` | Threshold breach status by location | E |
| `GET` | `api/assets/{assetId}/history` | Bearer | `CanReadAssetHistory` | `AssetHistoryResponseDto` | Cross-location asset history within tenant | E |
| `POST` | `api/benchmarking/query` | Bearer | `CanQueryBenchmarking` | `BenchmarkingQueryResponseDto` | Industry benchmarking against `industryDataset` (k-anonymity enforced) | E |
| `POST` | `api/audit-log/search` | Bearer | `CanReadAuditLog` | `PagedResult<AuditLogEntryDto>` | Page size capped at 200 | E |
| `POST` | `api/audit-log/export` | Bearer | `CanExportAuditLog` | `AuditLogExportSasDto` | SAS URL to CSV/JSON archive (60-min expiry) | E |
| `POST` | `api/dms/sync/{srId}` | Bearer | `CanManageDmsIntegration` | `200 DmsSyncResultDto` | Manual force-sync of single SR | E |
| `GET` | `api/dms/reconciliation` | Bearer | `CanManageDmsIntegration` | `DmsReconciliationDto` | Drift report between RVS and DMS | E |
| `POST` | `api/tenants/config` | Bearer | `CanManageTenantConfig` | `201 TenantConfigDto` | Bootstrap only | P |
| `GET` | `api/tenants/config` | Bearer | `CanManageTenantConfig` | `TenantConfigDto` | | A |
| `PUT` | `api/tenants/config` | Bearer | `CanManageTenantConfig` | `200 TenantConfigDto` | | A |
| `PUT` | `api/tenants/config/notifications` | Bearer | `CanManageTenantConfig` | `200 NotificationConfigDto` | Provider, webhook URL, secret | A |
| `PUT` | `api/tenants/config/access-control` | Bearer | `CanManageTenantConfig` | `200 AccessControlConfigDto` | IP allowlist | E |
| `GET` | `api/tenants/access-gate` | Bearer | `CanManageTenantConfig` | `AccessGateStatusDto` | | A |
| `GET` | `api/lookups/{lookupSetId}` | Bearer | `CanReadLookups` | `LookupSetDto` | | A |
| `GET` | `api/lookups/section10a/active-version` | Bearer | `CanReadLookups` | `Section10ATaxonomyDto` | Active taxonomy version + codes for technician app | A |
| `PATCH` | `api/asset-ledger/{ledgerEntryId}/section10a` | Bearer | `CanUpdateServiceRequests` | `200 AssetLedgerEntryDto` | Section 10A enrichment with strict taxonomy | A |
| `POST` | `api/admin/tenants/{id}/upgrade-to-enterprise-scale` | Bearer | `CanManagePlatform` | `200 TenantConfigDto` | Sales-led: configures Enterprise Scale contract terms (50+ loc) | P |
| `GET` | `api/admin/tenants/usage` | Bearer | `CanManagePlatform` | `TenantUsageReportDto` | Per-tenant usage metrics for invoicing | P |
| `GET` | `/health` | None | — | `200 / 503` | Dependency health check | A |

**Removed from v1.0 (do not re-introduce):**
- `POST /api/billing/checkout-session` — no Stripe billing in MVP
- `POST /api/billing/webhook` — no Stripe webhook
- `GET /api/billing/status` — returns current tier, billing period, location count, user count, trial status
- `POST /api/dms/sftp-export` (manual trigger) — Solo and Pro tiers use CSV download UI; Premium and Enterprise Scale use scheduled SFTP via `DmsSyncBackgroundService`

### 8.3 Key Request/Response Shapes

#### `ServiceRequestCreateRequestDto`

```csharp
public record ServiceRequestCreateRequestDto(
    CustomerInfoDto Customer,          // firstName, lastName, email (required), phone
    AssetInfoDto Asset,                // assetId (RV:{VIN}), manufacturer?, model?, year?
    string IssueCategory,              // from LookupSet
    string IssueDescription,           // customer free-text; required; max 2000 chars
    UrgencyLevel Urgency,              // Routine | Urgent | Emergency
    RvUsageType RvUsage,               // PartTime | FullTime
    List<DiagnosticResponseDto>? DiagnosticResponses  // from AI wizard; optional
);
```

#### `ServiceRequestSummaryDto`

```csharp
public record ServiceRequestSummaryDto(
    string Id,
    string LocationId, string LocationName,
    string Status,
    string CustomerFullName,
    string AssetId, string AssetDisplay,   // "2023 Grand Design Momentum 395G"
    string IssueCategory,
    string TechnicianSummary,              // truncated to 150 chars
    int AttachmentCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? AssignedTechnicianId,
    string? Priority
);
```

#### `DiagnosticQuestionsResponseDto`

```csharp
public record DiagnosticQuestionsResponseDto(
    List<DiagnosticQuestionDto> Questions,
    string? SmartSuggestion              // e.g., "Upload a photo of the hydraulic area"
);

public record DiagnosticQuestionDto(
    string QuestionText,
    List<string> Options,
    bool AllowFreeText,
    string? HelpText
);

public record AiOperationResponseDto<T>(
  bool Success,
  T? Result,
  double Confidence,
  List<string> Warnings,
  string Provider,
  string CorrelationId
);

public record IssueCategorySuggestionRequestDto(
  string IssueDescription
);

public record IssueCategorySuggestionResultDto(
  string? IssueCategory
);
```

#### `CapabilityAssessmentRequestDto` / `CapabilityAssessmentResponseDto`

```csharp
public sealed record CapabilityAssessmentRequestDto
{
    // Customer-supplied free-text issue description from Step 5. 1..2000 chars, required.
    public string IssueDescription { get; init; } = string.Empty;

    // Optional pre-resolved category. When supplied, the API skips the AI categorization
    // step and uses this value directly.
    public string? IssueCategory { get; init; }
}

public sealed record CapabilityAssessmentResponseDto
{
    // True when every required capability is enabled at the location, OR when no
    // specific capabilities are required for the resolved category.
    public bool Matched { get; init; }

    // Resolved issue category used to derive the required capability list. Null when
    // categorization could not produce a result (in which case Matched MUST be true).
    public string? IssueCategory { get; init; }

    // Capability codes considered necessary to service the issue.
    public List<string> RequiredCapabilities { get; init; } = [];

    // Required capability codes NOT enabled at the selected location. Empty when Matched.
    public List<string> MissingCapabilities { get; init; } = [];

    // Phone number of the selected location, surfaced so the Intake UI can render it
    // in the Step 6 alert when capabilities are not satisfied. Null when unavailable.
    public string? LocationPhone { get; init; }
}
```

#### `ServiceRequestAnalyticsResponseDto`

```csharp
public record ServiceRequestAnalyticsResponseDto(
    int TotalRequests,
    Dictionary<string, int> RequestsByStatus,
    Dictionary<string, int> RequestsByCategory,
    Dictionary<string, int> RequestsByLocation,
    List<AnalyticsRankItem> TopFailureModes,
    List<AnalyticsRankItem> TopRepairActions,
    decimal? AverageRepairTimeHours,
    List<AnalyticsRankItem> TopPartsUsed,
    decimal? AverageDaysToComplete
);

public record AnalyticsRankItem(string Name, int Count);
```

### 8.4 Search Request

`POST api/dealerships/{id}/service-requests/search` body:

```csharp
public record ServiceRequestSearchRequestDto(
    string? Keyword,
    string? Status,
    string? IssueCategory,
    string? LocationId,
    string? AssignedTechnicianId,
    string? AssignedBayId,
    string? AssetId,
    DateTime? DateFrom,
    DateTime? DateTo,
    string? Priority,
    int Page = 1,
    int PageSize = 25   // max 100
);
```

Search input validation: reject any `Keyword` containing `<`, `>`, `;`, `'`, `"`, `\`, `\0` — return `400` with `validation-error`.

---

## 9. Security Requirements

### 9.1 Authentication and Authorization

**SEC-AUTH-01** — All dealer endpoints MUST validate the JWT signature using Auth0's JWKS endpoint. Token expiry MUST be enforced.

**SEC-AUTH-02** — `ClaimsService.GetTenantIdOrThrow()` MUST be called in every authenticated service method before any Cosmos query. A request whose `tenantId` does not match the resource's `tenantId` MUST return `403` (not `404`).

**SEC-AUTH-03** — Location-scoped roles (`dealer:advisor`, `dealer:technician`, `dealer:manager`) MUST have their `locationIds` claim verified before returning or modifying any resource. `ClaimsService.HasAccessToLocation(locationId)` MUST be called for location-filtered operations.

**SEC-AUTH-04** — `platform:admin` role bypasses tenant isolation and CAN access any tenant. Usage MUST be logged as a custom App Insights event `PlatformAdminAccess` with `tenantId` and `userId`.

**SEC-AUTH-05** — Auth0 access tokens MUST NOT be stored in browser `localStorage`. Use memory-only or `sessionStorage` with short token lifetimes (≤ 1 hour).

### 9.2 Input Validation

**SEC-INPUT-01** — All string inputs MUST be validated for maximum length. Strings stored in Cosmos MUST be capped: issue description 2,000 chars; advisor notes 5,000 chars; names 100 chars; slugs 64 chars (alphanumeric + hyphens only).

**SEC-INPUT-02** — Slug format MUST be enforced: `/^[a-z0-9-]+$/` (lowercase alphanumeric and hyphens only). Reject on creation/update with `400`.

**SEC-INPUT-03** — Search keyword input MUST be sanitized as described in Section 8.4. Cosmos parameterized queries MUST be used for all variable inputs (no string concatenation).

**SEC-INPUT-04** — File upload MIME type MUST be validated server-side (not just client-side). Read the first 512 bytes of the file to verify the content matches the declared MIME type (magic bytes check).

**SEC-INPUT-05** — VIN input MUST be stripped of non-alphanumeric characters and validated for length (17 chars) and check digit before calling NHTSA. An invalid VIN MUST return `400` on the intake form before orchestration runs.

### 9.3 Secrets and Credential Management

**SEC-SECRETS-01** — Azure OpenAI endpoint key and SFTP private keys MUST be stored in Azure Key Vault. They MUST NOT appear in `appsettings.json`, environment variables, or version control. Azure Communication Services (email + SMS) authenticates via managed identity — no API key required.

**SEC-SECRETS-02** — All Azure services (Cosmos DB, Blob Storage, Key Vault, OpenAI) MUST authenticate via `DefaultAzureCredential` (Managed Identity in production; local dev uses Azure CLI credential). No connection string passwords in config.

**SEC-SECRETS-03** — Auth0 `Client Secret` (if used in server-to-server flows) MUST be stored in Key Vault, not `appsettings.json`.

### 9.4 Magic-Link Security

| Threat | Mitigation |
|---|---|
| Token enumeration | 256-bit cryptographic random suffix; infeasible to guess |
| Token theft via URL sharing | 90-day expiry; stable token (bookmarkable status page) |
| Cross-customer data leakage | Status page only returns data for the token's owner; partition key derived from email-hash, not walked |
| Replay after expiry | `magicLinkExpiresAtUtc` checked server-side; return `410 Gone` |
| Rate-based scanning | 10 req/min per IP on `api/status/{token}` |

### 9.5 Data Privacy

**SEC-PRIV-01** — Customer email addresses MUST be normalized (lowercased, trimmed) before storage and MUST NOT be logged in application telemetry.

**SEC-PRIV-02** — Blob Storage SAS URIs MUST expire within 1 hour. SAS URIs MUST NOT be stored permanently in the `ServiceRequest` document — generate on demand only.

**SEC-PRIV-03** — `GlobalCustomerAcct` container (partitioned by `/email`) MUST only be accessible to the service layer, never directly from a controller without going through `IGlobalCustomerAcctService`. The `platform:admin` role is the only role with direct cross-tenant query access.

**SEC-PRIV-04** — No PII (email, phone, name) MUST appear in structured logs, App Insights custom dimensions, or trace spans. Use `customerId` and `tenantId` only.

### 9.6 OWASP Top 10 Coverage

| OWASP Category | Mitigation in RVS |
|---|---|
| A01 Broken Access Control | ClaimsService tenant isolation on every authenticated call; location scope checks |
| A02 Cryptographic Failures | TLS enforced (`UseHttpsRedirection`); AES-256 at rest (Azure); SAS token 256-bit randomness |
| A03 Injection | Parameterized Cosmos queries; search input sanitization; slug regex enforcement |
| A04 Insecure Design | Anonymous intake by design; magic-link with one-way email hash; never storing SAS URIs |
| A05 Security Misconfiguration | `DefaultAzureCredential` / Managed Identity; no connection string secrets in config |
| A06 Vulnerable Components | Automated dependency scanning in CI (Dependabot); pin .NET 10 LTS patch versions |
| A07 Auth Failures | Auth0 JWT RS256; short token lifetimes; no `localStorage` for access tokens |
| A08 Integrity Failures | GitHub Actions pipeline with manual approval gate for production deployments |
| A09 Logging Failures | Structured logging on every request; App Insights; no PII in logs |
| A10 SSRF | VIN decode calls only permitted to NHTSA vPIC (allowlisted base URL); no user-supplied URLs |

---

## 10. Integration Requirements

### 10.1 Auth0

| Requirement | Spec |
|---|---|
| JWT issuer | `https://{tenant}.auth0.com/` |
| JWT audience | `https://api.rvserviceflow.com` |
| Algorithm | RS256 |
| Custom claims namespace | `https://rvserviceflow.com/` |
| Solo / Pro tier claim injection | Login Action injects `tenantId`, `orgName`, `locationIds`, `regionTag` from `app_metadata` |
| Solo / Pro tier tenant scoping | `app_metadata.tenantId` per user; no Auth0 Organizations (Auth0 Free plan) |
| **Premium tier identity** | Auth0 Organizations (Essentials B2B plan or higher); `tenantId` claim sourced from Organization ID (`org_xxx`) when login is Organization-scoped |
| **Premium tier SSO** | Per-Organization SAML connections (Okta, Entra ID, Google Workspace, OneLogin, Ping); RVS does not implement SAML directly |
| **Premium tier SCIM** | SCIM 2.0 via Auth0 per-Organization endpoint; group-to-role mapping at Organization level |
| Token lifetime | Access token: 1 hour; Refresh token: 15 days (rolling) |

**Migration note:** Solo or Pro tier tenants upgrading to Premium transition from `app_metadata` scoping to Auth0 Organization scoping. Triggered by `POST api/billing/upgrade-to-premium` (FR-BILL-02). The migration job: (1) creates an Auth0 Organization for the tenant, (2) re-creates each user inside the Organization (or links existing user via Auth0 connection), (3) updates `TenantConfig.IdentityModel = Organizations`, (4) updates `app_metadata.tenantId` to match the Organization ID for backward-compat during cutover. Existing JWTs remain valid until expiry; new logins use Organization-scoped flow. The ClaimsService implementation MUST handle both sources transparently during the cutover window. Post-migration, SAML and SCIM are configurable per Auth0 Organization.

### 10.2 Azure Cosmos DB

| Requirement | Spec |
|---|---|
| API | SQL (Core) API |
| Connection mode | Gateway (enables server-side caching) |
| Consistency level | Session (default); Eventual for read-only analytics queries |
| Multi-region | Single-write region MVP; multi-write deferred to Phase 2+ |
| RU budget per intake | ≤ 12 RU (cold), ≤ 10 RU (warm) |
| Partition key invariant | `tenantId` for all dealer/customer containers; never cross-partition query in service layer |

### 10.3 Azure Blob Storage

| Requirement | Spec |
|---|---|
| Container | `rvs-attachments` |
| Authentication | DefaultAzureCredential (Managed Identity) |
| SAS expiry — customer upload | 15 minutes |
| SAS expiry — dealer read | 1 hour |
| Path format | `{tenantId}/{locationId}/{serviceRequestId}/{attachmentId}_{filename}.{ext}` |
| Redundancy | LRS (MVP); GRS for production |

### 10.4 Azure OpenAI

| Requirement | Spec |
|---|---|
| Deployment | `gpt-4o-mini` (recommended); `gpt-4o` optional |
| Timeout | 5 seconds |
| Max output tokens | 500 |
| Fallback | Rule-based categorization + hardcoded questions per category |
| Authentication | DefaultAzureCredential |
| Cost target | ≤ $0.0002/intake at GPT-4o-mini rates |
| System prompt customization | `IntakeFormConfigEmbedded.aiContext` appended to base system prompt |

### 10.5 NHTSA vPIC API

| Requirement | Spec |
|---|---|
| Endpoint | `https://vpic.nhtsa.dot.gov/api/vehicles/decodevin/{VIN}?format=json` |
| Auth | None (public API) |
| Timeout | 3 seconds |
| Failure behavior | Proceed with customer-supplied asset info; log `VinDecodeTimeout` event |
| Rate limiting | Not documented by NHTSA; avoid concurrent bursts; no caching required in MVP |

### 10.6 Azure Communication Services (Email + SMS Notifications)

ACS is the default provider when `TenantConfig.NotificationConfig.Provider = RvsNative`. Per-tenant override (FR-TENANT-08) routes through webhook instead. Both modes can coexist with the outbound integration webhook (FR-TENANT-09); webhook fires regardless of provider.

| Requirement | Spec |
|---|---|
| Injection | Via `INotificationService` (email) and `ISmsNotificationService` (SMS) |
| Provider routing | `NotificationProvider` enum on `TenantConfig`: `RvsNative` (ACS), `KenectWebhook` (no ACS, webhook only), `Disabled` (no customer messaging from RVS) |
| Authentication | Azure Managed Identity via `DefaultAzureCredential` — no API keys |
| Email templates | Intake confirmation, status update (in-progress, completed) — code-managed HTML |
| SMS templates | Intake confirmation, magic-link delivery, status update |
| Delivery | Fire-and-forget; failure MUST NOT fail the intake transaction |
| From address (email) | `noreply@notifications.rvserviceflow.com` |
| From number (SMS) | Shared toll-free number (configured in `AzureCommunicationServices:Sms:FromPhoneNumber`) |
| Customer preference | `email` (default) or `sms` — either/or choice during intake |
| Suppression flag | `TenantConfig.NotificationConfig.SuppressOutboundCustomerMessages` — when true, no ACS sends regardless of provider; webhook still fires |

**Removed from MVP scope (per RVS_Competitive_Strategy.md §4):**
- Two-way SMS / inbound conversation handling
- Broadcast / bulk messaging
- Marketing or re-engagement automation
- Review request automation
- In-dashboard SMS composition

These features are Kenect / ServiceNomad territory. RVS coexists via the outbound webhook; integration partners handle two-way conversation.

### 10.6A Outbound Integration Webhook

Implementation requirement for FR-TENANT-09.

| Requirement | Spec |
|---|---|
| Trigger events | `serviceRequest.created`, `serviceRequest.statusChanged`, `serviceRequest.advisorNoteAdded`, `serviceRequest.completed` |
| Method | `POST` |
| Target | `TenantConfig.NotificationConfig.WebhookUrl` |
| Headers | `Content-Type: application/json`, `X-RVS-Signature: sha256=<hmac>`, `X-RVS-Event: <event-name>`, `X-RVS-Delivery: <guid>` |
| HMAC | SHA-256 over raw request body using `WebhookSecret` (Key Vault-stored) |
| Timeout | 10 seconds per attempt |
| Retry policy | 3 retries with exponential backoff: 30s, 5min, 30min |
| Verification | Webhook URL changes trigger a verification call (`event: "verification"`); persists only on `200 OK` within 10s |
| Audit | Failed deliveries after final retry surface in `auditLog` (Enterprise) and App Insights as `WebhookDeliveryFailed` |
| Latency target | First-attempt success < 30s P99 |

### 10.7 DMS Integration (Reframed for Free vs Enterprise)

The v1.0 Technical PRD specified SFTP-only DMS export. v2.0 splits this:

**Solo and Professional tiers — Manual CSV download:**
| Requirement | Spec |
|---|---|
| Trigger | User-initiated via `Blazor.Manager` UI |
| Endpoint | `GET api/dealerships/{id}/service-requests/export?format=csv&from=...&to=...` |
| Format | CSV; column schema mirrors `ServiceRequestCreateRequestDto` + status + advisor notes + Section 10A |
| Delivery | Browser download (file streamed in response) |

**Premium and Enterprise Scale tiers — Bidirectional integration via `IDmsIntegrationProvider`:**
| Requirement | Spec |
|---|---|
| Architecture | Provider abstraction: `IDmsIntegrationProvider` with `IdsAstraIntegrationProvider`, `LightspeedIntegrationProvider` implementations |
| RVS → DMS push | Outbound on SR `created` and `statusChanged` events; queued via Azure Storage Queue; processed by `DmsSyncBackgroundService` |
| DMS → RVS pull | Inbound webhook (where DMS supports) or scheduled pull (where it doesn't); writes to SR with `dmsLastSyncUtc` updated |
| Per-location config | `Location.DmsIntegration` overrides tenant-level config — supports mixed-DMS dealer groups (FR-ENT-09) |
| Reconciliation | Daily reconciliation job; drift report at `GET api/dms/reconciliation` |
| Auth | DMS partner-program credentials in Key Vault |
| Failure handling | Failed sync logged; retry per provider config; surfaced in reconciliation dashboard |

**Premium and Enterprise Scale — SFTP fallback for unsupported DMS:**
| Requirement | Spec |
|---|---|
| Trigger | Scheduled (configurable cron in `TenantConfig.DmsIntegration.SftpSchedule`) |
| Format | Same CSV as Solo / Pro tier export |
| Auth | Key-based and password-based SFTP; credentials in Key Vault |
| Config | Per-tenant SFTP host, port, remote path stored in `TenantConfig.DmsIntegration.Sftp` |
| Use case | Dealer groups whose DMS is not yet supported by a partner integration |

**Partnership status note:** As of v2.0, RVS is pursuing IDS Astra and Lightspeed Technology Partner programs concurrently. Whichever admits us first becomes the first bidirectional integration. The provider abstraction is designed so adding a second DMS integration is incremental, not architectural.

### 10.8 Anonymization Pipeline (Phase 2)

Architectural commitment, not future enhancement. Detailed design in `RVS_data_moat.md` §4.

| Requirement | Spec |
|---|---|
| Implementation | Azure Function (consumption or premium plan) with Cosmos DB Change Feed Trigger on `assetLedger` container |
| Lease container | Dedicated `assetLedgerLeases` Cosmos container, partition key `/id`, manual 400 RU |
| Trigger mode | Continuous (low-latency) or scheduled (cost-optimized); MVP starts with scheduled nightly batch |
| Anonymization steps | (1) Strip strict-identifying fields; (2) Aggregate geography to state level; (3) Aggregate temporal to date-only; (4) Apply differential privacy noise (Phase 3+) |
| Output | Write to `industryDataset` container, partition key `/datasetVersion` |
| Versioning | Each batch tagged with `datasetVersion` matching the active Section 10A taxonomy version |
| Audit trail | Lineage from `industryDataset` entry back to source `assetLedger` entry preserved internally; never exposed in commercial outputs |
| Failure handling | Pipeline failure raises P1 alert; retry from last successful lease; no data loss |
| Cost target | < $50/month at 50K events/month |

### 10.9 Section 10A Taxonomy Enforcement

Implementation requirement supporting FR-TECH-03 and FR-LEDGER-01.

| Requirement | Spec |
|---|---|
| Storage | `lookupSets` container, `category` = `section10a-component`, `section10a-failure-mode`, `section10a-repair-action`, `section10a-issue-category`, `section10a-part-number` |
| Active version | `TenantConfig.ActiveSection10ATaxonomyVersion` references the active `lookupSet` document for that tenant; defaults to platform-active version on tenant creation |
| Validation | `ITaxonomyValidator` service called from `ServiceRequestService.UpdateSection10AAsync` and `AssetLedgerService.EnrichAsync` |
| Error response | Invalid code → `400 ProblemDetails { type: "rvs:taxonomy-violation", invalidCodes: [...], suggestedAlternatives: [...] }` |
| Versioning | Adding a code = minor version bump; deprecating = minor version bump (deprecated codes remain valid for historical entries); semantic restructuring = major version bump |
| Per-entry tracking | `AssetLedgerEntry.taxonomyVersion` records the version under which the entry was captured; never silently re-mapped |
| Cross-version migration | Explicit operation; mapping table stored in `lookupSets` with category `section10a-version-mapping` |
| Tenant customization | NOT permitted in MVP; taxonomy is platform-managed (per `RVS_data_moat.md` §3.2). Per-tenant extensions are a future Enterprise consideration but break the dataset thesis if mishandled. |

---

## 11. Middleware Pipeline

Order is mandatory. Deviation MUST require architecture review sign-off.

| Order | Component | Registration | Applies To |
|---|---|---|---|
| 1 | Dev endpoints | `UseSwaggerUI()` | Development only |
| 2 | HTTPS redirect | `UseHttpsRedirection()` | Production only |
| 3 | CORS | `UseCors("AllowBlazorClient")` | All origins (Blazor.Intake WASM + Blazor.Manager WASM) |
| 4 | Rate limiting | `UseRateLimiter()` | Public intake + status endpoints |
| 5 | Exception handling | `ExceptionHandlingMiddleware` (singleton) | All exceptions → ProblemDetails |
| 6 | Authentication | `UseAuthentication()` | JWT validation |
| 7 | Authorization | `UseAuthorization()` | Policy enforcement |
| 8 | Tenant access gate | `TenantAccessGateMiddleware` (scoped) | Authenticated requests only |
| 9 | Controllers | `MapControllers()` | Terminal |

`TenantAccessGateMiddleware` MUST skip anonymous endpoints (check `IAllowAnonymous` metadata).

---

## 12. Testing Requirements

### 12.1 Unit Tests (`RVS.Tests.Unit`)

Required coverage areas:
- `StatusTransitions.cs` — All valid and invalid transition combinations
- `CustomerProfileService.ResolveOrCreateProfileAsync` — Three asset ownership branches (same owner, transfer, new asset) + reactivation
- `GlobalCustomerAcctService` — Magic-link token generation, format validation, expiry check
- `ServiceRequestService` — Orchestration step sequencing; AI fallback behavior; notification fire-and-forget
- `ClaimsService` — All claim accessor methods with valid/missing/malformed inputs
- `AzureOpenAiCategorizationService` — Timeout triggers fallback; structured JSON response parsed correctly
- Search sanitization — Blocked characters return `400`; clean input passes through

### 12.2 Integration Tests (`RVS.Tests.Integration`)

Required test scenarios:
- Full intake submission: new customer + new VIN → verify 5 Cosmos documents written
- Returning customer intake: reuse existing `GlobalCustomerAcct` + `CustomerProfile`
- VIN transfer: Customer B submits VIN previously active under Customer A → verify deactivation + reactivation
- Magic-link status page: valid token → cross-dealer SRs returned; expired token → 410
- Slug not found → 404; disabled tenant → 403
- Batch outcome: 25 SRs updated atomically; 26th SR rejected with 400
- Authenticated attachment upload: file stored at correct Blob path with correct tenant/location/SR prefix
- Rate limiting: 11th request within sliding window → 429

Integration tests MUST run against the Cosmos Emulator (Windows) or a dedicated test database (CI). They MUST NOT use production or shared dev Cosmos accounts.

### 12.3 End-to-End Tests

Minimum E2E scenarios (automated, against staging):
- RV owner submits intake → receives confirmation email with magic-link
- Advisor logs in, finds SR in queue, updates status to InProgress
- Technician opens job on `MAUI.Tech`, records Section 10A fields, job shows Completed in `Blazor.Manager`

### 12.4 Performance Baselines

Before MVP release, run load tests at:
- 50 concurrent intake submissions/minute for 5 minutes
- Verify: P95 < 3 s, no 5xx errors, Cosmos throttling (429) < 0.1%

---

## 13. Deployment Requirements

### 13.1 Infrastructure

| Component | Resource | Notes |
|---|---|---|
| API | Azure App Service (B2/B3) or Container Apps (MVP) | Enable Always On |
| `Blazor.Intake` WASM | Azure Static Web Apps | CDN-enabled; custom domain apex `rvintake.com`; PWA service worker caches WASM runtime for instant repeat visits |
| `Blazor.Manager` WASM | Azure Static Web Apps | CDN-enabled; same hosting pattern as Blazor.Intake |
| Cosmos DB | Single account, single region (MVP) | 9 containers per spec |
| Blob Storage | Single account | `rvs-attachments` container with per-tenant virtual paths |
| Key Vault | 1 vault | All secrets; API Managed Identity granted `get` + `list` |
| App Insights | 1 workspace | Linked to all API and frontend deployments |

### 13.2 CI/CD

| Stage | Toolchain | Requirements |
|---|---|---|
| Build | GitHub Actions | `dotnet build` must pass; no warnings treated as errors |
| Test | GitHub Actions | Unit + integration tests must pass; coverage ≥ 80% on domain/service layers |
| Publish | GitHub Actions | Publish API + frontend apps as artifacts |
| Deploy Staging | GitHub Actions | Automatic on `main` merge |
| Deploy Production | GitHub Actions | Manual approval gate required |
| Secrets | GitHub OIDC → Azure | No long-lived credentials in GitHub secrets; use Workload Identity Federation |

### 13.3 Configuration

`appsettings.json` MUST contain only non-secret configuration:

```json
{
  "Auth0": { "Domain": "...", "Audience": "..." },
  "AzureCosmosDb": { "Endpoint": "https://..." },
  "AzureBlobStorage": { "ServiceUri": "https://..." },
  "AzureOpenAi": { "Endpoint": "https://...", "DeploymentName": "gpt-4o-mini", "MaxTokens": 500, "TimeoutSeconds": 5 },
  "Nhtsa": { "BaseUrl": "https://vpic.nhtsa.dot.gov/api/" }
}
```

Secret values (API keys, connection strings) MUST be injected from Key Vault at startup via `AddAzureKeyVault` in `Program.cs`.

---

## 14. Cosmos RU Budget Summary

| Operation | Estimated RU | Note |
|---|---|---|
| Intake — new customer, cold | ~11.8 RU | 7 Cosmos operations |
| Intake — returning customer, warm | ~10.8 RU | Gateway-cached slug |
| Magic-link status read | ~1 + N RU | N = linked SR count |
| SR detail view | ~1 RU | Point read with embedded customer snapshot |
| SR search (25 results) | ~3 RU | Single-partition indexed query |
| Slug resolution (cached) | ~0 RU | Gateway cache hit |
| TenantConfig read (cached) | ~0 RU | Gateway cache hit |
| Analytics query (MVP volume) | ~5–10 RU | Single-partition aggregate |
| Batch outcome (25 SRs) | ~25 RU | 1 RU write × 25 |
| Asset history query (10A) | ~1 RU | Single-partition point read |

**Monthly cost estimate (100 tenants, 200 intakes/month each = 20,000 intakes/month):**
- Intake RU: 20,000 × 12 RU = 240,000 RU/month → < $1/month (Autoscale)
- Storage: 20,000 × avg 3 attachments × 10 MB ≈ 600 GB → ~$12/month

---

## 15. Known Gaps and Deferred Items

| ID | Description | Target Phase | Priority |
|---|---|---|---|
| GAP-01 | SFTP private keys stored in `TenantConfig` (Cosmos) — MUST move to Key Vault | Before Enterprise launch | Critical |
| GAP-02 | `AssetLedgerEntry` Section 10A enrichment via change feed not built; v2.0 specifies dedicated `PATCH api/asset-ledger/{id}/section10a` endpoint instead | Phase 1 (FR-LEDGER-02) | Required |
| GAP-03 | Customer Auth0 account (persistent login, preference saving) not supported | Deferred indefinitely | Low (anonymous + magic-link sufficient) |
| GAP-04 | Follow-up request endpoint not implemented; advisors use phone | Phase 2+ | Low |
| GAP-05 | Analytics counters run against Cosmos directly; no Azure Tables pre-aggregation | Phase 2 (Enterprise scale) | Performance risk at scale |
| GAP-06 | Batch SR update endpoint (`POST batch-update`) for high-scale offline sync | Future | Low (sequential PUT sufficient for MVP) |
| GAP-07 | Labor time prediction API | Deferred | Future |
| GAP-08 | MVP uses long polling for Service Board updates; vNEXT introduces a dedicated SignalR hub | Phase 3+ | Medium |
| **GAP-09** | **Anonymization pipeline (FR-ENT-10 dependency) — production-grade k-anonymity enforcement, differential privacy** | **Phase 2** | **Critical for Enterprise launch** |
| **GAP-10** | **`auditLog` container provisioning automation when tenant upgrades to Enterprise** | **Phase 2** | **Required for Enterprise launch** |
| **GAP-11** | **Auth0 Organization migration tooling for Free → Enterprise tenant upgrades** | **Phase 2** | **Required for Enterprise launch** |
| **GAP-12** | **Section 10A taxonomy v1 final controlled vocabulary list** | **Phase 1, Sprint 4** | **Critical (blocks all 10A enforcement)** |
| **GAP-13** | **`IDmsIntegrationProvider` abstraction with first concrete implementation (IDS or Lightspeed)** | **Phase 2** | **Required for first Enterprise customer** |
| **GAP-14** | **ToS / DPA / MSA template language for cross-dealer aggregation and OEM licensing** | **Phase 0** | **Critical (blocks first design partner)** |

---

## 16. Out of Scope (Phase 1 Solo + Pro and Phase 2+ Premium + Enterprise Scale)

Per `RVS_Competitive_Strategy.md` §7 (the Yes/No filter), the following are explicitly out of scope and MUST NOT be implemented without a strategic review:

**Always out of scope (Free and Enterprise both):**
- Dealer Management System (DMS) replacement features (accounting, warranty claim filing, parts inventory)
- Two-way SMS conversation, broadcast messaging, marketing automation, in-dashboard message composition
- Voice AI / inbound call handling
- Customer-facing iOS or Android native app
- Service appointment scheduling, bay reservation, technician routing algorithm
- Invoicing, payments, credit card processing, ESC approval workflows
- Marine, heavy equipment, or agricultural vertical-specific features (deferred until OEM thesis validates)
- (No longer applicable — Stripe billing is now in scope from Phase 1; see FR-BILL-01 through FR-BILL-06)
- Operator-couple / mobile tech specialized workflow features
- Per-tenant Section 10A taxonomy customization (breaks the dataset; if required by Enterprise customer, becomes a contract negotiation point, not a product feature)

**Phase 1 (Solo + Pro) out of scope (planned for Phase 2 Premium tier):**
- Multi-location features in UI (architecture supports them)
- Cross-location analytics, cross-location asset history
- Drag-and-drop Service Board (single-location queue table only)
- SAML SSO, SCIM provisioning, IP allowlisting
- `auditLog` container and audit log query/export
- Bidirectional DMS integration (CSV download only in Free)
- Industry benchmarking access
- Predictive maintenance suggestions
- Warranty leakage analytics

**Phase 3+ out of scope (planned for OEM track):**
- OEM data licensing API surfaces
- Differential privacy beyond k-anonymity
- Per-OEM custom analytics products (those become consulting engagements, not product features)

---

## 17. Open Questions

| # | Question | Owner | Due | Status |
|---|---|---|---|---|
| OQ-01 | ~~What is the Auth0 plan tier at commercialization?~~ | Business | Resolved | Free for Free tenants (`app_metadata`); Organizations (Essentials B2B+) for Enterprise |
| OQ-02 | Should the magic-link token be stored hashed or plaintext in Cosmos? | Engineering | Before Phase 1 launch | Open |
| OQ-03 | When is Azure SignalR Service required vs. single-instance App Service sticky sessions sufficient? | Engineering | Phase 3+ | Deferred (long polling sufficient through Phase 3) |
| OQ-04 | NHTSA vPIC rate limit behavior under burst intake — implement client-side throttle if needed | Engineering | Phase 1 load test | Open |
| **OQ-05** | **Section 10A taxonomy v1 final controlled vocabulary list — domain expert workshop output** | **Domain SME + Engineering** | **Phase 1, Sprint 4** | **Open (gating)** |
| **OQ-06** | **ToS / DPA language for cross-dealer aggregation and OEM licensing — counsel review** | **Legal counsel + Founder** | **Phase 0 (before first design partner)** | **Open (gating)** |
| **OQ-07** | **Anonymization k-anonymity threshold for `industryDataset` queries — initial value k=5; pressure-test with first benchmarking queries** | **Engineering + Legal** | **Phase 2 design** | **Open** |
| **OQ-08** | **Should any tier dealers be able to opt out of cross-dealer anonymized aggregation?** | **GTM + Legal** | **Phase 0** | **Decided: NO; documented in `RVS_data_moat.md` §5.3** |
| **OQ-09** | **First DMS partner — IDS or Lightspeed?** | **GTM + Engineering** | **Phase 2 kickoff** | **Open (pursue both partner programs concurrently; whichever admits first wins)** |
| **OQ-10** | **SOC 2 audit timing — Type I before first Enterprise customer, or Type II after first 3 customers?** | **Compliance + GTM** | **Before first Enterprise pitch** | **Open** |
| **OQ-11** | **Solo tier monthly SR cap value — currently 300/loc; pressure-test against design partner usage** | **GTM** | **After 3 design partners active** | **Open** |
| **OQ-12** | **Webhook payload schema final — pressure-test against Kenect's expected inbound shape before locking** | **Engineering + GTM** | **Phase 1 Sprint 13** | **Open** |
| **OQ-13** | **Enterprise pricing — fixed Standard/Plus/Premium tiers or fully custom per contract?** | **GTM** | **Before first Enterprise sales conversation** | **Recommendation: fully custom for first 5; introduce tiers after observed deal patterns** |

---

## 18. Document Status

This Technical PRD v3.0 reflects the four-tier pricing model (Solo / Professional / Premium / Enterprise Scale) plus the OEM Data Licensing track. Companion documents:

- Strategic context: `RVS_Context.md` v3.0, `RVS_Competitive_Strategy.md` v3.0
- Product requirements: `RVS_PRD.md` v3.0 (Solo + Pro), `RVS_Premium_PRD.md` v1.0 (Premium + Enterprise Scale)
- Data and OEM strategy: `RVS_data_moat.md` v3.0, `RVS_OEM_GoToMarket.md` v1.0
- Execution: `RVS_Implementation_Plan_v2.md` v3.0
- DMS coexistence: `RVS_vs_DMS_value_prop.md` v3.0

If this document conflicts with any other v3.0 document on a specific requirement, the conflict should be raised and resolved before implementation begins; the resolution updates whichever document is wrong.

---

*Last updated: April 30, 2026. v3.0 supersedes v2.0 (April 30, 2026, earlier same-day) and v1.0 (March 20, 2026). For questions, contact the RVS platform team.*
