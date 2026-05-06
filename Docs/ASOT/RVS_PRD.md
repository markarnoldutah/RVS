# PRD: RV Service Flow (RVS) — Solo + Professional Tiers

## 1. Product Overview

**Document version:** 3.4
**Date:** April 30, 2026
**Status:** Draft (post-pivot, v3.4 — version sync only, no substantive change to Solo/Pro requirements)
**Supersedes:** v3.3 (earlier same-day), v3.0 (earlier same-day), v2.0 (MVP-Free), v1.2

### 1.1 Scope

This PRD describes **the Solo and Professional tiers of RVS** — the Phase 1 deliverables that ship together as the initial commercial product. Premium and Enterprise Scale tier requirements are in [`RVS_Premium_PRD.md`](RVS_Premium_PRD.md). Strategic context for the four-tier model is in [`RVS_Context.md`](RVS_Context.md) and [`RVS_Competitive_Strategy.md`](RVS_Competitive_Strategy.md).

### 1.2 What Changed From v3.3 (Same-Day Refinement)

v3.4 makes no substantive change to the Solo or Professional tier requirements documented here. The v3.4 changes are entirely Enterprise Scale-tier additions documented in `RVS_Premium_PRD.md` v1.2: dedicated solutions engineer FTE commitment (FR-ES-011), custom SLA negotiation (FR-ES-012), and documented OEM revenue-share clause (FR-ES-007 rewritten). The version bump on this document is for set consistency only.

### 1.3 What Changed From v3.0 (Earlier Same-Day) → v3.3

- **Pure per-location pricing with volume bands** replaces the v3.0 flat-fee + per-location-surcharge model. Solo flat $39/loc; Pro $79/$69/$59 per 1-9/10-24/25-49 loc; Premium $119/$109/$99 per same bands.
- **Per-user pricing eliminated entirely.** All tiers, all roles, unlimited users (including non-technicians at Premium and Enterprise Scale, which were previously per-user metered).
- **Pro SR cap reduced** from 1,000/loc/mo to 600/loc/mo to create a clear upgrade signal to Premium.
- **Trial structure clarified:** prospects can choose Solo or Pro trial at signup with separate 30-day trials; Premium and Enterprise Scale are sales-led pilots, not self-serve trials.
- **Volume-banded billing logic** added to Phase 1 Stripe billing infrastructure (Sprint 13-14, ~minor addition; per-user metering removed from Phase 2 scope, ~1 sprint saved).

### 1.4 What Changed From v2.0 (Earlier Today)

The v2.0 PRD targeted a Free + Enterprise model with Free as a dataset-feeding distribution channel. v3.0 (and now v3.3) restructures into four tiers with paid revenue from day one:

- **Free tier eliminated.** Replaced with 30-day Solo or Pro trial, credit card required at signup.
- **Stripe billing infrastructure** required from Phase 1 (not deferred).
- **Self-service user provisioning** required from Phase 1 (Auth0 Management API integration).
- **Anti-corpus-theft protections** added throughout (verification gates, audit logs, rate limits).
- **Industry benchmarking deferred to Phase 2** when anonymization pipeline is operational.
- **Phase 1 engineering scope** is ~25 sprints to accommodate Pro features and billing.

The v1.2 PRD's deprecated features list (no scheduling, no payments, no two-way SMS, no broadcast, etc.) remains fully in force.

### 1.5 The Product in One Paragraph

RVS Solo is a single-tenant, multi-location-capable structured intake and technician workflow product for RV dealerships and independent shops, priced at $39/location/month with no per-user pricing. The customer submits a service request through a web form on their phone. AI categorizes the issue. The advisor sees a triaged queue. The technician sees a pre-diagnosed job, captures structured Section 10A outcome data via mobile app, and the platform writes a structured event to the asset ledger. RVS Professional ($79/loc/mo for 1-9 loc, decreasing to $69 at 10-24 loc and $59 at 25-49 loc) adds multi-location coordination, cross-location asset history, regional manager dashboards, warranty leakage analytics, and advanced benchmarking — the layer that makes RVS commercially viable for the 5–15 location dealer group.

---

## 2. Goals

### 2.1 Strategic Goals

- Land **15–25 paying Solo customers** in the first 4 months after Phase 1 ship
- Land **3–5 paying Professional customers** within 6 months after Phase 1 ship
- Reach **month 12 ARR of $50K–$150K** from combined Solo + Professional
- Accumulate **≥10,000 structured Section 10A service events** in the asset ledger by month 12
- Achieve **≥95% Section 10A taxonomy adherence** across all submitted events
- Cover **at least 3 major OEMs** with meaningful event counts (≥500 per OEM by month 12)
- Validate the dataset thesis well enough to support Premium tier sales pitches and an initial OEM exploratory conversation

### 2.2 User Goals (by persona)

- **RV owner:** Submit a structured service request from phone in under 3 minutes, with photos and voice description, no account required. Check status anytime via magic-link.
- **Service advisor:** Replace phone intake with a triaged, AI-categorized queue with technician-ready summaries.
- **Technician:** Open jobs by VIN/QR scan, see customer photos and structured wizard answers, capture repair outcome via Section 10A controlled vocabularies in 3–5 seconds per job.
- **Dealership owner / single-location operator (Solo customer):** Use a $39/location service intake tool that makes the operation more efficient and provides industry benchmarking insights.
- **Multi-location dealer manager (Professional customer):** Coordinate service operations across 5–15 locations with cross-location queue, regional manager hierarchy, and warranty leakage analytics that can pay for the platform multiple times over.

### 2.3 Non-Goals

The following are explicitly out of scope for Solo and Professional tiers:

- IT-grade compliance features (SAML, SCIM, audit log, IP allowlisting) — Premium tier
- Bidirectional DMS integration — Premium tier (Solo/Pro use CSV download)
- Per-user pricing or seat-based licensing — Premium tier
- Custom analytics builder, custom report exports — Enterprise Scale
- Service appointment scheduling, bay assignment, technician routing
- Parts ordering, inventory integration, warranty claim filing
- Persistent customer Auth0 accounts (anonymous intake only)
- Two-way SMS conversations, broadcast messaging, in-dashboard message composition
- Voice AI, inbound call handling
- Invoicing, payments, ESC approval workflows
- Marine, heavy equipment, agricultural verticals

---

## 3. User Personas

### 3.1 Primary Personas

- **Alex** — RV owner, anonymous intake user
- **Maria** — Service advisor at a Solo or Professional tier dealer
- **Jordan** — Technician at any tier
- **Sam** — Owner / GM of a single-location dealer (Solo customer)
- **Chris** — Multi-location dealer GM (Professional customer)
- **Pat** — RVS platform admin (internal operations)

### 3.2 Persona Roles by Tier

**Solo tier:**
- `dealer:owner` — full access (typical for single-location operator)
- `dealer:advisor` — SR creation, search, status updates
- `dealer:technician` — Section 10A capture only (unlimited)
- `dealer:readonly` — read-only

**Professional tier (Solo roles + multi-location additions):**
- `dealer:regional-manager` — `regionTag`-scoped subset of locations
- `dealer:manager` — single-location full access
- Cross-location queue, asset history, and analytics accessible to owners and regional managers
- Bulk user provisioning via CSV import

**Premium and Enterprise Scale roles** (documented in `RVS_Premium_PRD.md`).

Customers remain anonymous in all tiers.

---

## 4. Functional Requirements (Solo Tier)

### 4.1 Core Intake and Workflow

**FR-001: Single-tenant data model with multi-location support**
The architecture supports multi-tenancy and multi-location from day one. Solo tenants have no architectural location limit but use single-location features in the dashboard UI. Adding location 5+ on Solo is allowed and works architecturally; the customer is prompted that Professional tier features (cross-location queue, regional manager hierarchy, multi-location analytics) become available with a Professional upgrade.

**FR-002: Anonymous customer intake**
Identical to v2.0 FR-002. The intake form at `https://rvintake.com/{locationSlug}` requires no customer login. On submission, the API runs the seven-step orchestration (identity resolution, profile upsert, asset ownership check, asset ledger write, AI categorization, magic-link generation, fire-and-forget notification).

**FR-003: VIN scan and lookup**
Camera-based VIN extraction via Azure OpenAI vision endpoint, plus NHTSA vPIC decode. Manual entry remains available as fallback.

**FR-004: Description-first AI category suggestion + structured wizard**
Customer enters or speaks issue description. AI suggests a top-level category. Customer can override. Contextual follow-up questions render based on selected category. Wizard answers populate `ServiceEventEmbedded` structured fields.

**FR-005: Speech-to-text + AI cleanup for issue description**
Browser-native Web Speech API (or device microphone on mobile). AI cleans transcript. Customer reviews edited text before submission. After review, description is sent to category suggestion endpoint.

**FR-006: Photo and video upload**
Up to 10 attachments per SR. Direct-to-blob via SAS upload URL. Configured size and type constraints. Tenant-scoped blob path.

**FR-007: Magic-link customer status page**
Anonymous, rate-limited, magic-link-token-validated. Shows the customer's active service requests at this corporation across all locations within the tenant.

**FR-008: Service Manager dashboard — single-location queue (Solo)**
Single-location queue table view: customer name, VIN, issue category, AI-generated technician summary (truncated), status badge, submission date, attachment count. Search and filter (status, category, date range, asset). Detail view, status updates, advisor notes, attachment viewing. **No drag-and-drop Service Board in Solo** — Professional tier feature.

**FR-009: Technician mobile app — `RVS.MAUI.Tech`**
Offline-first, VIN/QR scan, photo capture, voice notes, Section 10A capture. Local SQLite queue, sequential PUT replay on reconnect. Optimistic concurrency on `updatedAtUtc`. **Technicians are unlimited at all tiers** — no per-user pricing affects this app.

**FR-010: Section 10A structured outcome capture (strict taxonomy)**
Section 10A fields (`componentType`, `failureMode`, `repairAction`) must conform to controlled vocabularies sourced from `lookupSets`. The technician app and dashboard render dropdowns. Submissions with invalid or missing taxonomy values are rejected at the API layer with HTTP 400. Taxonomy is versioned. Each `AssetLedgerEntry` records the taxonomy version under which it was captured.

**FR-011: Asset ledger writes are mandatory and non-blocking**
Every service request submission writes an `AssetLedgerEntry` to the ledger container. Async write within 60 seconds of SR creation. Persistent failure raises P1 alert.

**FR-012: AI-generated technician summary**
On SR creation, AI generates a structured technician-ready summary from the issue description, wizard answers, and asset metadata. Summary is stored on the SR document and displayed prominently in advisor and technician views.

**FR-013: Basic industry benchmarking dashboard (read-only)**
Solo tier customers have read access to pre-built benchmarking dashboard views. Includes:
- "How does my RECT compare to industry P50?"
- "Top 5 industry-wide failure categories vs my distribution"
- "How does my month-over-month service volume compare to industry trends?"

Restrictions on Solo benchmarking access:
- Pre-built dashboard views only (no custom queries)
- Single-dimension comparisons
- No drill-down beyond category level
- No date range narrower than 90 days
- No geographic filtering finer than US-region level
- No model-year-specific data
- 20 dashboard refreshes per month
- **Verification gate** (FR-024) must be cleared before benchmarking unlocks

Benchmarking ships in **Phase 2** alongside the anonymization pipeline; not available in Phase 1 launch. Solo customers in Phase 1 see "available [date]" placeholder.

### 4.2 Notification and Integration

**FR-014: Notification provider abstraction**
`TenantConfig.NotificationConfig.Provider` enum: `RvsNative` | `KenectWebhook` | `Disabled`. Default `RvsNative`. Available at all tiers.

**FR-015: Outbound integration webhook**
On SR submission, status change, advisor note (customer-facing), and SR completion, the platform fires an outbound webhook to `TenantConfig.NotificationConfig.WebhookUrl` (when configured). Available at all tiers.

**FR-016: Transactional notifications via ACS**
Email + SMS on intake submission (with magic-link). SMS only for status changes. No two-way SMS, no broadcast, no marketing.

**FR-017: CSV download for DMS export (manual)**
Solo and Professional tier customers can download a CSV of their SRs filtered by date range and other parameters via dashboard UI. No SFTP scheduled push, no bidirectional API integration (Premium tier features).

### 4.3 Identity, Tenants, Onboarding, and Billing

**FR-018: Auth0 authentication (Free plan; `app_metadata` tenant scoping)**
Solo and Professional tier tenants use Auth0 Free with `app_metadata` tenant scoping. Login Action injects `tenantId`, `locationIds`, and roles. Migration to Auth0 Organizations occurs at Premium tier upgrade.

**FR-019: Self-serve tenant signup with credit card**
`POST api/signup` accepts `TenantSignupRequestDto { corporationName, ownerEmail, ownerFirstName, ownerLastName, locationName, locationAddress, locationPhone, requestedSlug, paymentMethodToken }` and:
1. Validates slug uniqueness against `slugLookup`
2. Creates Stripe Customer and stores payment method
3. Initiates 30-day trial
4. Creates Auth0 user with verification-pending email
5. Creates `Dealership` document with `Tier = Solo`
6. Creates initial `Location` with provided info
7. Creates `TenantConfig` with Solo defaults, `AccessGate.Status = Active`
8. Sends welcome email with verification link and dashboard URL

The endpoint is `[AllowAnonymous]` and rate-limited to 5 requests/IP/hour.

**FR-020: 30-day self-serve trial with auto-conversion (Solo or Professional)**
At signup, the prospect can choose Solo trial or Professional trial. The signup flow asks for location count and recommends the appropriate tier (1-4 loc → Solo recommended; 5+ loc → Pro recommended), but does not force the choice.

Common to both:
- Trial begins on signup
- Credit card required at signup; payment method captured but not charged during trial
- 7 days before trial end: dashboard banner reminding of upcoming charge; email notification
- Customer can cancel anytime during trial; no charge if cancelled before trial end
- Customer can downgrade Pro trial → Solo at trial end (downgrade processes at conversion, not mid-trial)
- Failed payment at trial end: 3-day grace period with dashboard alerts; tenant moves to disabled access gate after grace

Trial-end conversion:
- Solo trial → Solo subscription at $39/location/month (based on locations active at trial end)
- Pro trial → Pro subscription at the band-applicable rate per FR-P-006 (1-9 loc: $79/loc; 10-24 loc: $69/loc; 25-49 loc: $59/loc)

Premium and Enterprise Scale do NOT have self-serve trials. Premium and Enterprise Scale evaluations are sales-led pilots negotiated as part of contract conversations (typically 30-60 days for Premium, 60-90 for Enterprise Scale; pilot terms specified per contract).

**FR-021: Solo tier billing enforcement**
`TenantConfig.BillingConfig` tracks: Stripe customer ID, subscription ID, plan tier (Solo/Professional/Premium/EnterpriseScale), billing period dates, location count for billing, current month SR count.

`TenantAccessGateMiddleware` returns HTTP 402 when:
- Subscription is past_due beyond grace period
- Trial expired without payment method capture (cannot occur given signup flow)
- Tenant manually disabled by platform admin

Solo tier monthly SR cap: 300/location/month. At cap, intake submissions return HTTP 402 with customer-friendly message: *"This dealership is temporarily unable to accept new requests. Please contact the service center directly."*

**FR-022: Self-serve user provisioning (Auth0 Management API)**
Owners and managers can add staff users via dashboard:
- Enter email, name, role, location assignment
- System creates Auth0 user via Management API
- System sends invitation email with verification + password setup link
- User completes setup and gains access per assigned role

User deactivation through dashboard removes Auth0 access and updates audit trail. Technicians (`dealer:technician` role) are unlimited at all tiers and do not affect Premium tier per-user billing.

**FR-023: Capability assessment at intake**
Carried forward from v2.0 FR-021. The intake wizard calls `POST /api/intake/{locationSlug}/assess-capabilities` between Step 5 and Step 6, and renders a non-blocking advisory banner if the location's `EnabledCapabilities` does not match the inferred required capabilities for the issue.

**FR-024: Dealer verification gate for benchmarking access**
Before unlocking industry benchmarking access, every tenant must clear a verification step:
- During signup or first benchmarking access attempt, customer submits dealer claim form: DOT number, business EIN, dealer license number (state + license #), business address
- Submitted info routed to verification queue (manual review)
- Verification typically completes 24–72 hours
- Cross-checks against state dealer license registries (where available)
- Verified tenants get `TenantConfig.BenchmarkingAccess.Verified = true` and benchmarking unlocks
- Verification failure surfaces a "verification incomplete — please contact support" message; tenant can still use all other features

This protects the dataset against scraping by competitors, aggregators, or OEM end-runs. Detailed in `RVS_data_moat.md` §6.

**FR-025: Tenant access gate**
Standard `TenantAccessGateMiddleware`. Disabled tenants receive HTTP 403. Tenants past trial without payment receive HTTP 402.

### 4.4 Data Moat Foundations

**FR-026: Section 10A taxonomy versioning**
Every `AssetLedgerEntry` records the taxonomy version under which it was captured. Taxonomy stored in `lookupSets` with version metadata. Cross-version analysis uses explicit migration tooling.

**FR-027: Anonymization-ready data model**
Asset ledger schema separates strictly-identifying fields (tenantId, customer email, dealer notes) from anonymizable fields (asset metadata, failure mode, repair action, parts, geographic region). Anonymization pipeline ships in Phase 2.

**FR-028: Asset ID format enforcement**
Asset IDs follow `{AssetType}:{Identifier}` (e.g., `RV:1ABC234567`). Format enforced at write time.

**FR-029: Data retention and deletion semantics**
Tenant data in the asset ledger is retained indefinitely. The dealer's dashboard view is a rolling 90-day window plus current month. Tenant deletion does not remove asset ledger entries — dealer-identifying fields are anonymized but the structured service event remains in the dataset. Disclosed in ToS.

---

## 5. Functional Requirements (Professional Tier)

Professional tier inherits all Solo FRs (FR-001 through FR-029) and adds the following. Pricing is **volume-banded per-location** ($79/$69/$59 per location/mo by 1-9/10-24/25-49 location bands per FR-P-006). No per-user pricing.

### 5.1 Multi-Location Coordination

**FR-P-001: Multi-location queue and search**
`POST api/dealerships/{id}/service-requests/search` supports `locationId[]` filter as multi-select. Pro UI surfaces this prominently. Regional managers (`dealer:regional-manager` role with `regionTag` claim) have search results auto-filtered to permitted locations.

**FR-P-002: Drag-and-drop Service Board**
Kanban-style status columns (`New` → `InProgress` → `Completed` → `Cancelled`). Drag-and-drop transitions call the existing PUT API. Long polling (default 5 min, configurable) refreshes the board. Available at Professional tier and above; Solo tier uses simple table queue (FR-008).

**FR-P-003: Cross-location asset history**
For any asset (VIN), Professional+ users can view the full service history across every location in the corporation. Aggregates `AssetLedgerEntry` documents partitioned by `assetId` but filtered to only show entries belonging to the requesting tenant. Sorted by `serviceDateUtc` descending.

**FR-P-004: Regional manager hierarchy**
The `dealer:regional-manager` role activates with a `regionTag` claim. Regional managers see only locations matching their `regionTag`. Corporate admin assigns `regionTag` values to locations and users. Enforced server-side in `IServiceRequestService.SearchAsync` via `ClaimsService.GetRegionTag()`.

**FR-P-005: Multi-location onboarding tools**
- Bulk location import via CSV (validates all rows before insert)
- Location templates (intake form config, capabilities, branding, default settings) — new locations clone from a template
- Bulk user import via CSV with location and role assignments

**FR-P-006: Volume-banded per-location pricing**
Professional tier pricing is per-location with three volume bands tracked in `TenantConfig.BillingConfig`:
- 1–9 locations: $79/loc/mo
- 10–24 locations: $69/loc/mo
- 25–49 locations: $59/loc/mo

The applicable rate is determined by `TenantConfig.BillingConfig.LocationCountForBilling` at billing period start. ALL locations are billed at the band-applicable rate (not graduated — a 12-location customer pays 12 × $69, not 9 × $79 + 3 × $69). Band transitions happen on monthly billing boundaries; mid-month location additions or removals adjust at next period start. Billing job submits per-location quantity to Stripe at the band-determined unit price.

Pro tenants exceeding 49 locations are nudged toward Enterprise Scale; system permits operation at 50+ locations on Pro pricing for up to 30 days while sales engagement converts the contract, then tier transition is required.

### 5.2 Cross-Location Analytics

**FR-P-007: Cross-location operational analytics dashboard**
Dashboard answers, at minimum:
- RECT by location, by region, by month
- Service request volume by location, by category, by month
- Top failure modes by location and by region
- Technician productivity (SRs completed per tech per week, by location)
- Warranty vs. customer-pay job mix, by location
- Outliers and anomalies (locations exceeding RECT thresholds)

Drill-down from corporate → region → location → individual SR. Date range filtering, period comparison, CSV export.

**FR-P-008: Warranty leakage analytics**
Specialized analytics view identifies potentially-mislabeled service requests:
- SRs marked customer-pay where the asset is within likely warranty window
- SRs where failure mode + asset age suggest warranty-eligibility but billed customer-pay
- Technician-by-technician variance in warranty-vs-customer-pay mix

This is an "alert candidates for review" tool, not automatic re-classification. The dealer's accounting team makes final determinations. **This is the load-bearing ROI feature for Professional tier** — the warranty recovery this enables typically pays for the Professional subscription multiple times over.

**FR-P-009: Advanced industry benchmarking (custom queries)**
Professional tier customers can run custom benchmarking queries beyond the Solo dashboard limits:
- Custom queries via predefined templates
- Model-year-specific data
- State-level geography
- 30-day date ranges
- 200 queries/month

Same verification gate (FR-024) applies. Tier-gated query depth detailed in `RVS_data_moat.md` §6.

**FR-P-010: Service throughput SLA dashboards**
Configurable per-tenant SLAs (RECT thresholds, intake-to-first-action time, status-staleness durations). Dashboard surfaces breaches and trends. Alert routing to managers (basic — full alerting is Premium).

### 5.3 Professional Tier Capacity

**FR-P-011: Per-location SR cap**
Professional tier SR cap is 600/loc/mo (raised from Solo's 300/loc/mo). At cap, intake submissions return HTTP 402 with the same customer-friendly message as Solo. The 600 cap creates a clear upgrade signal to Premium for high-volume customers.

---

## 6. User Experience

### 6.1 Intake Flow (Unchanged)

The 6-step wizard is unchanged: Contact → VIN → Description+Category → Photos → Urgency → Review/Submit. Capability assessment runs between steps 5 and 6.

### 6.2 Dashboard Flow (Tier-Differentiated)

**Solo:**
- Single-location queue table view
- Search and filter (status, category, date range, asset)
- Detail view with attachments, advisor notes, status updates
- Section 10A view (strict taxonomy dropdowns)
- Settings: tenant + location config, intake form config, user management, notification provider, webhook URL, billing
- Basic benchmarking dashboard (Phase 2)

**Professional adds:**
- Cross-location queue with multi-select location filter
- Drag-and-drop Service Board
- Cross-location asset history view
- Regional manager dashboard with `regionTag` scoping
- Cross-location analytics with drill-down
- Warranty leakage analytics view
- Advanced benchmarking custom queries
- Multi-location onboarding tools (bulk imports, templates)

### 6.3 Technician Mobile Flow (Unchanged)

VIN/QR scan opens job. Outcome entry uses Section 10A dropdowns. Offline queue, sync on reconnect. Identical at all tiers; technicians are unlimited.

---

## 7. Non-Functional Requirements

Carried forward from `RVS_Technical_PRD.md` v3.0. Performance targets, availability SLAs, Cosmos RU budgets, and security requirements apply to Solo and Professional tiers.

**SLA:** 99.5% monthly uptime for both Solo and Professional. (Premium tier upgrades to 99.9%.)

---

## 8. Implementation Sequencing (Solo + Professional)

Detailed in [`RVS_Implementation_Plan_v2.md`](RVS_Implementation_Plan_v2.md). Summary:

| Sprint | Focus |
|---|---|
| 1–2 | Solution scaffold, Cosmos schema, Auth0, ClaimsService, middleware |
| 3–4 | Tenant/location services, slug routing, taxonomy enforcement |
| 5–7 | Intake API + AI Wave 1 + asset ledger writes |
| 8–9 | Solo manager dashboard (queue, detail, attachments) |
| 10 | Magic-link customer status |
| 11 | Technician mobile app (offline sync, Section 10A capture) |
| 12 | Notification webhook + ACS + capability assessment |
| 13–14 | Self-serve signup + Stripe billing infrastructure + 30-day trial |
| 15 | Self-service user provisioning (Auth0 Management API) |
| 16–17 | Multi-location data model exposed in UI; cross-location queue and search |
| 18 | Drag-and-drop Service Board |
| 19 | Cross-location asset history view |
| 20 | Regional manager role + `regionTag` enforcement |
| 21–22 | Cross-location analytics dashboard |
| 23 | Warranty leakage analytics |
| 24 | Multi-location onboarding tools (bulk import, templates) |
| 25 | Polish, design partner onboarding, telemetry, deployment |

**Total: ~25 sprints.** Phase 2 (Premium tier) begins after Phase 1 ship criteria met.

---

## 9. Success Criteria

**Phase 1 ships when:**
- 5 design partners actively using Solo or Professional
- Asset ledger contains ≥1,000 structured Section 10A events
- Taxonomy adherence rate ≥95%
- ≥80% of submitted intakes complete in <3 minutes
- ≥75% of submitted intakes include at least one photo or video
- P95 intake API latency <3 seconds
- Stripe billing flows tested end-to-end (trial signup, trial-end conversion, payment failure, cancellation)
- Self-service user provisioning works end-to-end
- Anti-corpus-theft verification gate operational (manual queue functional even if benchmarking is not yet shipping)
- Zero P1 incidents (data loss, security breach) in last 30 days

These criteria gate the start of Premium tier work.

---

## 10. Deprecated From v1.2 — Reasons

For future-self reference (carried forward from v2):

| v1.2 Feature | Removed Because |
|---|---|
| Tiered subscription pricing ($199/$349/$499) | Replaced by Solo/Pro/Premium/Enterprise Scale model |
| SFTP DMS export | Premium tier feature; Solo/Pro use CSV download |
| Two-way SMS, ad-hoc messaging, broadcast | Kenect / ServiceNomad territory |
| Customer chat dialog on status page | Same — coexistence via webhook |
| In-dashboard SMS composition | Same — coexistence via webhook |
| Free-text Section 10A | Required strict taxonomy for OEM data thesis |
| Customer Auth0 accounts | Anonymous + magic-link sufficient |
| Per-tenant customizable lookup sets | Taxonomy must be platform-managed |
| Private labeling (custom domains) | Premium / Enterprise Scale feature |

Removed in v3 specifically:

| v2.0 Feature | Removed Because |
|---|---|
| Free tier (no charge) | Cash runway constraint requires revenue from day one |
| 50 SR/month Free cap | No longer applicable; Solo at 300 SR/loc/mo |

---

## 11. Open Questions

| # | Question | Owner | Due |
|---|---|---|---|
| OQ-01 | Solo tier monthly SR cap value (currently 300/loc) — pressure-test against design partner usage | GTM | After 3 design partners active |
| OQ-02 | Should magic-link tokens be stored hashed in Cosmos? | Engineering | Before Phase 1 ship |
| OQ-03 | Section 10A taxonomy v1 — final controlled vocabulary list | Domain SME | Sprint 4 |
| OQ-04 | ToS language for cross-dealer aggregation, OEM licensing, liquidated damages for benchmarking misuse | Legal counsel | Phase 0 (before any design partner signs) |
| OQ-05 | Verification queue staffing — initially founder, eventually success engineer | Operations | Sprint 18+ |
| OQ-06 | Pro→Premium transition discount mechanics — how is the 50%-off-first-6-months applied in Stripe | Engineering | Phase 2 design |
| OQ-07 | Should benchmarking queries that return "insufficient data" (k-anon suppressed) count against monthly query quotas? | Engineering | Phase 2 |

---

*End of RVS_PRD.md v3.0 (Solo + Professional tiers).*
