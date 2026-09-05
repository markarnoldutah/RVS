# RV Service Flow (RVS) — Platform Context

**Version 3.4 — April 30, 2026**

A platform overview for developers, investors, partners, and stakeholders. This document supersedes Version 3.3 (earlier same-day), 3.0, 2.x, and 1.x. For competitive positioning, see [`RVS_Competitive_Strategy.md`](RVS_Competitive_Strategy.md). For OEM strategy, see [`RVS_OEM_GoToMarket.md`](RVS_OEM_GoToMarket.md).

> **What changed from v3.3 (same-day refinement):** Enterprise Scale tier sharpened with three feature/contract additions to justify the per-location rate above Premium. The pricing structure is unchanged. The features are: (1) **Dedicated solutions engineer** — named RVS engineering team member assigned per ES customer, ~0.10–0.50 FTE depending on contract size, owns DMS integration architecture, custom analytics enablement, and technical escalation. This human capital is the largest single cost driver justifying the ES per-location rate. (2) **Custom SLA negotiation** — higher uptime targets (up to 99.99% in Phase 4+), service credits for SLA breach, custom incident response procedures, named maintenance windows, mutual NDA-protected post-incident reviews. (3) **OEM revenue-share clause** documented in ES Master Services Agreement — 5–10% of OEM contract value when a customer's data represents ≥15% of an OEM-licensed aggregate, paid quarterly in arrears. The previously vague "case-by-case revenue share arrangements" is now a named contract clause. Critical support remains exclusive to Enterprise Scale (clarified in v3.4 — explicitly NOT available as a Premium add-on).

> **What changed from v3.0 (same-day refinement → v3.3):** Pricing model restructured from "flat fee + per-location surcharge" per tier into pure per-location pricing with volume bands within each tier. Per-user pricing eliminated entirely (all tiers, all roles unlimited). Enterprise Scale floor lowered to $150/loc at 50 locations. Pro SR cap reduced to 600/loc/mo; Premium SR cap added at 1,000/loc/mo. Implementation fees banded by location count. Pro→Premium and Premium→Enterprise Scale transition discounts standardized at 50% off first 3 months. Tiered support SLAs (Standard/Priority/Critical) introduced as the revenue-capture mechanism for high-engagement customers, replacing per-user pricing economically.

> **What changed from v2 (earlier today):** v2 documented a Free + Enterprise tier model. Cash runway constraints made Free untenable. v3 restructured into four tiers — **Solo / Professional / Premium / Enterprise Scale** — plus the OEM Data Licensing track. Anti-corpus-theft protections were added throughout. The architectural foundation is unchanged; the commercial model is more conventional and runway-defensible.

---

## 1. The Problem and the Strategic Opportunity

### 1.1 The Operational Problem

RV dealership service departments and independent shops operate with three persistent inefficiencies:

1. **Phone-based intake** that produces incomplete diagnostic information before the unit arrives
2. **Free-text notes** that capture billing-quality data but not analytics-quality data
3. **Per-location data silos** that prevent multi-location dealer groups from operating as a coordinated system

Beyond these, the broader market suffers from a fourth issue: **the absence of structured cross-dealer service event data** that would let OEMs, parts suppliers, insurers, and aftermarket businesses make data-informed decisions.

### 1.2 The Strategic Opportunity

The RV service software market in 2026 is more contested than 12 months ago:

- **Kenect** (with Auto Labs, post-March 2025) has voice AI, automated scheduling, and customer-conversation primacy across 10,000+ dealerships in multiple verticals.
- **IDS Astra G2 and Lightspeed** have shipped major upgrades in 2025: free OEM VIN decoding, embedded payments, AI-powered insights, technician video tools, and (for Lightspeed) a Service Scheduler explicitly marketed against RECT.
- **ServiceNomad** (formerly RVTechAI) has shipped a six-layer "RV Service Operating System" with a working voice AI front desk and is signing both single-shop and dealer customers — but its consultative-sale pricing leaves the modal mobile RV operator unserved.
- **QuoteIQ, RV Service Suite** and similar SMB CRM tools serve the $30–$100/mo price band but lack structured failure data architecture.

In this contested landscape, two strategic positions remain genuinely defensible:

1. **The structured failure dataset** — a cross-dealer, asset-keyed ledger with rigorous Section 10A taxonomy. No DMS captures this. Kenect's data is conversations. ServiceNomad has the data but isn't framing it as a moat. The window to build this asset before someone else commits to it is real but bounded — probably 12–24 months.

2. **The full-spectrum service intelligence platform** — from solo mobile operators to multi-location dealer groups, with tiered features at each level. No competitor today serves the full spectrum well: ServiceNomad excludes the modal small operator on price; QuoteIQ doesn't address multi-location coordination; IDS/Lightspeed serve only the multi-location dealer market and only as a DMS.

**RVS pursues both strategically: tiered SaaS revenue is the cash engine and dataset contribution mechanism; the dataset is the long-term moat and the OEM commercialization vehicle.**

### 1.3 What RVS Is Not

To prevent scope drift, RVS explicitly is not:

- A DMS replacement. It coexists with IDS, Lightspeed, EverLogic, Motility.
- A messaging or customer-conversation platform. Kenect, Podium, Text Request own that layer.
- A full service operating system. ServiceNomad owns that framing.
- A CRM or invoicing tool. QuoteIQ, RV Service Suite, FreshBooks, etc., own that layer.
- A scheduling, ESC-approval, or payments tool. The DMS or ServiceNomad handles these.
- A consumer-facing app. Customer interaction is via a web-based intake portal.
- A multi-vertical (marine, heavy equipment, agricultural) platform — until the RV thesis is validated and OEM revenue is real.

This list is enforced via the Yes/No filter in `RVS_Competitive_Strategy.md` §7.

---

## 2. Product Architecture: Three Surfaces, Four Tiers

### 2.1 The Four Commercial Tiers

```
                    Solo            Professional     Premium          Enterprise Scale

Per-loc 1–9         $39             $79              $119             n/a
Per-loc 10–24       $39             $69              $109             n/a
Per-loc 25–49       $39             $59              $99              n/a
Per-loc 50+         n/a             → Ent. Scale     → Ent. Scale     $150–$300 negotiated

SR cap/loc/mo       300             600              1,000            unlimited
SLA                 99.5%           99.5%            99.9%            99.9% + 24/7
User pricing        unlimited       unlimited        unlimited        unlimited
Trial               30-day, CC      30-day, CC       sales-led pilot  sales-led pilot
Sales motion        self-serve      self-serve       sales-assisted   sales-led custom
```

**Solo ($39/loc/mo, all volumes):** Customer intake portal, AI Wave 1 (VIN extract, transcript cleanup, category suggestion), manager dashboard (single-location queue, search, detail), technician mobile app with Section 10A capture, magic-link customer status, outbound notification webhook, CSV download, basic industry benchmarking dashboard read access. 30-day trial, credit card required at signup. Self-serve checkout via Stripe.

**Professional ($79/$69/$59 per location/mo by volume band):** Everything in Solo, plus cross-location queue and search, drag-and-drop Service Board, cross-location asset history, regional manager hierarchy with `regionTag` enforcement, multi-location onboarding tools, cross-location operational analytics, warranty leakage analytics, advanced industry benchmarking (custom queries), 600 SRs/loc/mo cap. 30-day self-serve trial available at signup or as upgrade from Solo.

**Premium ($119/$109/$99 per location/mo by volume band):** Everything in Professional, plus SAML SSO via Auth0 Organizations, SCIM provisioning, IP allowlisting, comprehensive audit log + export, SOC 2 attestation, bidirectional DMS integration (one partner: IDS or Lightspeed), DMS reconciliation dashboard, mixed-DMS support within tenant, field-level access control, 99.9% SLA, dedicated success manager, quarterly business review, 1,000 SRs/loc/mo cap. Sales-assisted pilot (no self-serve trial).

**Enterprise Scale (50+ locations, custom contract):** Everything in Premium, plus: multiple DMS integrations simultaneously, custom analytics and report builder, custom data exports (S3/SFTP/Blob), Critical support tier bundled (1-hour response, dedicated CSM, 24/7 P1 — exclusive to Enterprise Scale, not available as a Premium add-on), **dedicated solutions engineer** (named RVS engineering team member, ~0.10–0.50 FTE per customer depending on contract size — owns DMS integration, custom analytics, technical escalation), **custom SLA negotiation** (higher uptime targets up to 99.99%, service credits, custom incident response procedures, named maintenance windows), quarterly executive business review, co-marketing opportunities, **OEM data partnership coordination with documented revenue-share clause** (5–10% of OEM contract value when customer's data represents ≥15% of an OEM-licensed aggregate), unlimited SRs. Reference contract: $150–$300/loc/mo (negotiated; floor $150 at 50 locations).

**Future: OEM Data Licensing Tier** — separate commercial track ($50K pilot to $3M/year strategic partnership). Detailed in `RVS_OEM_GoToMarket.md`.

**Per-user pricing:** None. All tiers include unlimited users including unlimited technicians (which protects the dataset thesis) and unlimited non-technicians (which simplifies pricing and procurement). Self-service user provisioning works through the dashboard at every tier.

**Implementation fees** (one-time, banded by location count):

| Tier | Locations | Implementation fee |
|---|---|---|
| Solo | All | None (self-serve) |
| Professional | All | None (self-serve) |
| Premium | 1–9 | $5,000 |
| Premium | 10–24 | $7,500 |
| Premium | 25–49 | $10,000 |
| Enterprise Scale | 50–99 | $15,000 |
| Enterprise Scale | 100–249 | $25,000 |
| Enterprise Scale | 250+ | $40,000 |

Implementation add-ons (additional DMS integration, custom data migration, custom analytics dashboard config) priced separately and scoped at signing.

**Support tiers** (Phase 2 ship, alongside success engineer hire):

| Support tier | Monthly cost | Response SLA | Channels | Included with |
|---|---|---|---|---|
| Standard | Included | 1 business day | Email, portal | Solo, Pro, Premium |
| Priority | +$500/mo | 4 hours, business hours | Email, portal, phone | Optional add-on at Pro and Premium |
| Critical | +$1,500/mo | 1 hour, business hours | All + dedicated CSM channel | Bundled with Enterprise Scale |

**Annual prepay:** 15% off across all tiers (subscription only; implementation fees full price).

**Transition discounts:** 50% off first 3 months for customers upgrading Pro→Premium or Premium→Enterprise Scale. Sales lever, not published. Applies to subscription only, not implementation fees.

### 2.2 The Three Customer-Facing Surfaces

The three surfaces are unchanged from prior versions; what each surface exposes is gated by tier.

**Customer Intake Portal — `RVS.Blazor.Intake`** (Blazor WebAssembly Standalone PWA)
- Anonymous access at `https://rvintake.com/{locationSlug}`
- Magic-link customer status page within the same app
- Mobile-first, single-page wizard
- Description-first AI category suggestion, structured follow-up questions, photo/video upload
- No customer account, no login, no app install
- Functionally identical at all tiers (Solo through Enterprise Scale)

**Service Manager Dashboard — `RVS.Blazor.Manager`** (Blazor WebAssembly Standalone)
- Authenticated dealer staff (Auth0 JWT)
- Solo: queue, search, detail, attachments, basic benchmarking dashboard
- Professional adds: Service Board (drag-and-drop), cross-location queue, regional dashboards, batch outcomes, multi-location onboarding, warranty leakage analytics
- Premium adds: audit log views, DMS integration management, SAML configuration UI, advanced compliance tooling
- Enterprise Scale adds: custom analytics builder, executive reporting

**Technician Mobile App — `RVS.MAUI.Tech`** (MAUI Blazor Hybrid)
- Offline-first, employer-provisioned via MDM
- VIN/QR scan to job, photo capture, voice notes
- Section 10A outcome capture with controlled vocabularies
- Local SQLite queue, sequential PUT-replay sync on reconnect
- Functionally identical at all tiers; technicians are unlimited at all tiers

### 2.3 The Strategic Asset: The Asset Ledger

The fourth surface, invisible to customers, is the **append-only asset ledger** — partitioned by `assetId` (e.g., `RV:1ABC234567`), capturing every service event as a normalized, taxonomy-enforced record.

**Every customer at every tier writes to the asset ledger.** This is the dataset that powers:

- Cross-location asset history (Professional+ feature)
- Industry benchmarking (basic at Solo, advanced at Pro+, custom at Premium+)
- Predictive maintenance suggestions (deferred)
- **OEM data licensing** (long-term commercial vehicle)

The ledger is not a product feature. It is the company's strategic asset. Every architectural decision in the platform is checked against whether it strengthens or weakens the ledger.

Anti-corpus-theft protections (verification gates, tiered query depth, variable k-anonymity, audit logs, ToS liquidated damages clauses) are documented in `RVS_data_moat.md` §6.

---

## 3. Multi-Tenant Architecture (Unchanged)

The corporation-as-tenant model from v1/v2 is unchanged and is even more strategically aligned than it was:

```
Blue Compass RV (Corporation = Auth0 Organization = Cosmos partition)
├── Blue Compass RV - Salt Lake City (Location)
├── Blue Compass RV - Denver (Location)
├── Blue Compass RV - Tampa (Location)
└── ... 100+ more locations              [Enterprise Scale]

Bish's RV Group (Corporation = separate tenant)
├── Bish's - Idaho Falls (Location)
└── ... regional locations               [Premium]

Mountain Family RV (Corporation, 8 locations)
└── ... regional locations               [Professional]

Happy Trails RV (single-location corporation)
└── Happy Trails - Boise (Location)      [Solo]

Don's Mobile RV Service (single-location, mobile)
└── Mobile dispatch from home base       [Solo]
```

The architecture supports tenants from one location (Solo) through 100+ locations (Enterprise Scale) without structural changes. Cosmos partition strategy, the nine-container schema, and the Auth0-based identity model are unchanged. Tier-specific differences are at the application/feature-flag layer, not the data layer.

### 3.1 Data Aggregation and Anonymization Layer

```
Tenant data (private, per-tier feature access)
    ↓ aggregation pipeline
Anonymized industry dataset (RVS-owned, queryable)
    ↓ benchmarking API (tier-gated query depth + verification gate)
    ↓ OEM data licensing (commercial track)
```

The anonymization pipeline runs against the asset ledger on a periodic schedule (initially nightly batch; long-term streaming). It strips dealer-identifying information, applies variable k-anonymity (k=5 for generic queries up to k=25 for OEM-relevant queries), and produces a queryable industry dataset. **This pipeline is a Tier-1 architectural commitment, not a future enhancement.**

Detailed design in [`RVS_data_moat.md`](RVS_data_moat.md).

---

## 4. Roles and Permissions

Auth0-based RBAC with the following roles:

- `platform:admin` — RVS internal, cross-tenant
- `dealer:corporate-admin` — corporation-wide, all locations (Premium / Enterprise Scale only)
- `dealer:owner` — single-corporation owner (all tiers)
- `dealer:regional-manager` — `regionTag`-scoped subset of locations (Professional+)
- `dealer:manager` — single-location full access
- `dealer:advisor` — single-location SR management
- `dealer:technician` — single-location, Section 10A only — **unlimited at all tiers**
- `dealer:readonly` — single-location, read-only

Customers remain anonymous, accessing intake and status via direct URL and magic-link tokens.

**Per-user pricing:** None at any tier. All users (technicians, advisors, managers, owners, regional managers, corporate admins) are unlimited at every tier. This protects the dataset thesis (technicians are the data-capture users; gating them would damage Section 10A coverage) and simplifies the pricing model for procurement teams. Revenue from high-engagement customers is captured through volume-banded per-location pricing and optional Priority/Critical support tier upgrades, not per-seat charges.

Self-service user provisioning via Auth0 Management API is required at all tiers from Phase 1.

**Enterprise tier identity (Premium+):** SAML SSO via Auth0 Organizations, SCIM provisioning. Solo and Professional tier customers use Auth0 with `app_metadata` tenant scoping; Premium customers transition to Auth0 Organizations.

---

## 5. The Nine-Container Cosmos Schema (Unchanged Foundation, Two Additions)

| # | Container | Partition Key | Purpose | Tier |
|---|---|---|---|---|
| 1 | `serviceRequests` | `/tenantId` | Core SR data | All |
| 2 | `customerProfiles` | `/tenantId` | Tenant-scoped customer records | All |
| 3 | `globalCustomerAccts` | `/email` | Cross-dealer identity, magic-link tokens | All |
| 4 | `assetLedger` | `/assetId` | **The strategic asset.** Append-only service event ledger. | All write; tier-gated read |
| 5 | `dealerships` | `/tenantId` | Corporation profiles | All |
| 6 | `locations` | `/tenantId` | Physical service sites | All |
| 7 | `tenantConfigs` | `/tenantId` | Tenant settings, access gate, **tier**, billing config | All |
| 8 | `lookupSets` | `/category` | Taxonomies (strict enforcement) | All |
| 9 | `slugLookup` | `/slug` | Fast slug resolution | All |

### 5.1 New Container (Premium+): `auditLog`

Partition key `/tenantId`. Append-only record of all data access and mutation events for SOC 2 / enterprise audit requirements. Provisioned at Premium tenant onboarding.

### 5.2 New Container (data moat): `industryDataset`

Partition key `/datasetVersion`. The output of the anonymization pipeline. Queryable for tier-gated benchmarking and OEM licensing. Read-only at the application layer; written only by the aggregation pipeline.

---

## 6. Section 10A Taxonomy (Strict Enforcement, Unchanged)

The taxonomy enforcement model from v2 is unchanged. Section 10A controlled vocabularies are stored in `lookupSets`, versioned, platform-managed (no per-tenant customization). Free-text entries are rejected at the API layer with HTTP 400.

**This is enforced at every tier.** A Solo customer's Section 10A entries follow the same taxonomy as a Premium customer's. The dataset thesis requires uniform structure across all contributors.

---

## 7. The Authentication & Identity Model

| Tier | Auth0 Plan | Identity Strategy |
|---|---|---|
| Solo | Free | `app_metadata` tenant scoping |
| Professional | Free | `app_metadata` tenant scoping |
| Premium | Essentials B2B or higher | Auth0 Organizations, per-org SAML, SCIM |
| Enterprise Scale | Professional or Enterprise | Auth0 Organizations, custom IdP, SCIM, additional security |

The `ClaimsService` abstraction is identical for all tiers. The difference is purely Auth0-side configuration. Migration from `app_metadata` to Auth0 Organizations is performed at Premium tier upgrade.

---

## 8. Notification Strategy (Unchanged from v2)

**Per-tenant `NotificationProvider` enum (all tiers):**
- `RvsNative` — RVS sends transactional email and SMS via Azure Communication Services (default)
- `KenectWebhook` — RVS fires outbound webhooks; Kenect (or equivalent) routes the customer message
- `Disabled` — RVS sends no customer notifications; integration partner handles all delivery

Outbound webhook fires on:
- Service request submitted
- Service request status changed
- Advisor note added (if customer-facing)
- Service request completed

**No two-way SMS, no broadcast, no marketing, no in-dashboard messaging.** This is enforced by the Yes/No filter and is non-negotiable.

---

## 9. AI Strategy

**Wave 1 (Phase 1, all tiers):**
- VIN extraction from photo (Azure OpenAI gpt-4o-mini)
- Speech-to-text and transcript cleanup
- Issue category suggestion (description-first)
- AI-generated technician summary at intake submission

**Wave 2 (Phase 2, Premium tier):**
- Cross-location pattern detection (predictive intake hints based on similar VINs in the ledger)
- Anomaly flagging for warranty leakage candidates
- AI-assisted Section 10A categorization for technicians (suggested classifications from photo + voice notes)

**Explicitly out of scope:**
- Voice AI / inbound call handling (ServiceNomad territory)
- Generative customer-facing chat (Kenect/ServiceNomad territory)
- Diagnostic chatbots replacing technician judgment (liability and model accuracy concerns)

All AI calls are server-side, wrapped in `AiOperationResponseDto<T>` envelopes, and degrade gracefully on failure.

---

## 10. Implementation Roadmap

Three operational phases, each with hard scope discipline. Detailed in [`RVS_Implementation_Plan_v2.md`](RVS_Implementation_Plan_v2.md).

### Phase 0: Repositioning (Weeks 1–2, no engineering)

ToS / MSA / DPA legal foundation; counsel review of liquidated damages clauses for benchmarking data misuse; Section 10A taxonomy v1 finalization; competitor language audit; design partner identification.

### Phase 1: Solo + Professional MVP (Sprints 1–25, ~25 weeks)

**Solo + Professional tiers in production.** Stripe billing, self-serve signup, multi-location data model exposed in UI, cross-location queue, regional manager hierarchy, warranty leakage analytics, asset ledger writes from day one. Industry benchmarking deferred to Phase 2 (no anonymization pipeline yet).

**Ship criteria:** 5 design partner customers actively using the platform across Solo and Professional tiers, ledger has ≥1,000 events, taxonomy adherence ≥95%.

### Phase 2: Premium + Anonymization (Months 7–12)

**Premium tier shippable.** Auth0 Organizations migration, SAML SSO, SCIM, audit log, IP allowlisting, bidirectional DMS integration (one partner). Anonymization pipeline operational. Tiered industry benchmarking (basic at Solo, advanced at Pro, full custom at Premium). Verification gate for benchmarking access.

**Ship criteria:** 1 paying Premium customer signed; first OEM exploratory conversation initiated.

### Phase 3: Enterprise Scale + OEM Pilot Readiness (Months 13–18)

Enterprise Scale tier sales-ready. First OEM pilot under contract. Dataset coverage at OEM-grade for at least one Tier A target (Brinkley, Alliance, or Grand Design).

**Ship criteria:** First OEM pilot under contract, 50,000+ ledger events, ≥5% installed-base coverage of one major OEM.

### Deprecated From v1 Roadmap

- ❌ Phase 4 — Scheduling and assignment (ServiceNomad territory)
- ❌ Phase 5 — Parts integration (DMS territory)
- ❌ Phase 6 — Industry expansion to marine, heavy equipment, agricultural (deferred until OEM thesis validated)

These remain documented in `RVS_MultiIndustry_Expansion.md` as future possibilities but are removed from the active roadmap.

---

## 11. Business Model

**Year 1 (Phase 1–2):** Revenue ramps from month 5–6 as Solo trials convert. Phase 1 ship → first Solo paying customer ~month 6. Phase 2 ship → first Premium customer ~month 9–12. Target month 12 ARR: $50K–$150K.

**Year 2 (Phase 3):** Solo revenue scales; Professional tier ramps; first Premium customers; first OEM pilot under contract. Target month 24 ARR: $500K–$1M.

**Year 3+:** Enterprise Scale customers material; OEM data licensing meaningful revenue. Target ARR: $1.5M–$3M plus first OEM contract.

These are planning targets, not commitments. They depend on dataset growth, which depends on tiered adoption, which depends on execution.

**The honest read:** Year 1 is a slow-growing SaaS with a $39/loc Solo tier and a small number of Professional customers. The strategic value is in the dataset, which is invisible from the outside until it isn't. The four-tier model funds Year 1 ARR sufficiently to bridge to Year 2 Premium revenue and Year 3 OEM validation.

---

## 12. Why This Will Work (and Why It Might Not)

**Why it could work:**

- The four-tier model serves every customer segment from solo operator to multi-state dealer group.
- Solo tier captures runway-critical revenue from day one at a price the modal SMB market actually pays.
- Professional tier serves the underaddressed mid-market dealer group segment.
- Premium tier serves IT-involved sophisticated dealer groups that no current competitor specifically targets.
- Enterprise Scale preserves pricing power at the high end.
- Geographic concentration in the Mountain West gives us a defensible regional beachhead.
- DMS partner programs (IDS, Lightspeed) are real distribution channels we can pursue.
- OEMs already buy data from third parties; the procurement template exists.
- The Yes/No filter prevents scope drift toward feature parity wars we cannot win.

**Why it might not:**

- Phase 1 elongation (~25 sprints / 6 months) tightens runway before first revenue.
- Solo tier may not generate sufficient revenue to bridge to Premium-tier profitability.
- ServiceNomad may pivot upmarket to multi-location before we ship Premium.
- IDS or Lightspeed may ship a competing intake module via partner ecosystem leverage.
- The OEM thesis may not validate — OEMs may decide they don't want third-party data.
- Solo founder bandwidth may break under the combined demands of dataset stewardship, multi-tier customer support, Enterprise sales, and Phase 1 engineering.
- Corpus theft via Solo signups, even with verification gates, is a real risk that requires ongoing operational vigilance.

The strategy is documented honestly because the failure modes are real. The North Star metric (dataset health) tells us early whether the strategy is working, separately from revenue.

---

## 13. Document Map

For deeper detail, consult:

- [`RVS_Competitive_Strategy.md`](RVS_Competitive_Strategy.md) v3.0 — positioning, competitors, tier mapping, sales objection handling, Yes/No filter
- [`RVS_PRD.md`](RVS_PRD.md) v3.0 — product requirements for Solo and Professional tiers
- [`RVS_Premium_PRD.md`](RVS_Premium_PRD.md) — product requirements for Premium and Enterprise Scale tiers
- [`RVS_Technical_PRD.md`](RVS_Technical_PRD.md) v3.0 — API contracts, data model, performance, security, anti-corpus-theft architecture
- [`RVS_data_moat.md`](RVS_data_moat.md) v3.0 — taxonomy, anonymization, ledger architecture, ToS language, verification gates
- [`RVS_OEM_GoToMarket.md`](RVS_OEM_GoToMarket.md) — OEM strategy, target accounts, deal structures
- [`RVS_Implementation_Plan_v2.md`](RVS_Implementation_Plan_v2.md) v3.0 — laser-focused execution plan
- [`RVS_vs_DMS_value_prop.md`](RVS_vs_DMS_value_prop.md) v2.0 — DMS coexistence positioning
- [`RVS_MultiIndustry_Expansion.md`](RVS_MultiIndustry_Expansion.md) — deferred future thesis, retained for reference only

---

*End of RVS_Context.md v3.0. Last updated April 30, 2026.*
