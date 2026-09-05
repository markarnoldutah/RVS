# RVS Implementation Plan

**Version:** 3.4
**Date:** April 30, 2026
**Status:** Authoritative — supersedes v3.3 (earlier same-day), v3.0 (earlier same-day), v2.0 (same-day), and v1.x

> **What changed in v3.4 (same-day refinement of v3.3):** Phase 3 hiring plan updated to reflect Enterprise Scale tier additions from `RVS_Premium_PRD.md` v1.2 (v3.4). FR-ES-011 commits a dedicated solutions engineer per ES customer (named RVS engineering team member, ~0.10–0.50 FTE depending on contract size). New §6.4 "Dedicated Solutions Engineer Capacity Model" documents the FTE allocation, cumulative load by scenario (base case M36: ~0.70 FTE; optimistic M36: ~3.0 FTE), and the implication for Phase 3 backend engineer #2 hire timing — first ES contract requires backend engineer #2 onboarded before contract effective date. No pricing changes; no Phase 1 or Phase 2 changes.

> **What changed in v3.3 (same-day refinement of v3.0):** Pricing model alignment. Sprint 13-14 explicitly references banded per-location billing logic (Solo flat $39; Pro $79/$69/$59 by 1-9/10-24/25-49 loc). Phase 2 sprint themes restructured: per-named-user metering for Premium REMOVED (no per-user pricing in v3.3); support tier infrastructure ADDED (Standard/Priority/Critical billing per FR-BILL-07); banded implementation fee billing ADDED (FR-BILL-09). Net engineering scope: ~neutral (~1 sprint saved on per-user metering, ~1 sprint added on support tier infrastructure). Phase 1 still ~25-27 sprints; Phase 2 themes shift but Phase 2 duration unchanged. Revenue targets in §7.1 maintained but rationale updated to reflect v3.3 ACV mix shift (smaller Premium ACVs at low loc counts, larger Premium ACVs at high loc counts via banded pricing). Implementation fees added to runway narrative as one-time cash contributions distinct from ARR.

> **What changed in v3.0 (earlier same-day):** v2 specified a 17-sprint Phase 1 targeting Free tier ship. v3 elongated Phase 1 to ~25 sprints to accommodate the four-tier model (Solo + Professional ship together, Premium and Enterprise Scale in later phases) plus Stripe billing infrastructure, self-service user provisioning, and verification gate. First Solo paying customer ~month 6–7. First Pro paying customer ~month 7–8. First Premium customer ~month 9–12. The runway implications are real and disclosed honestly in §7.2.

---

## 1. Purpose

Translate strategy into an execution sequence. This document specifies what gets built, in what order, with what gating criteria, and at what cost. It is the single source of truth for sprint planning, hiring decisions, and runway management.

For strategic context: [`RVS_Context.md`](RVS_Context.md) and [`RVS_Competitive_Strategy.md`](RVS_Competitive_Strategy.md). For requirements: [`RVS_PRD.md`](RVS_PRD.md) and [`RVS_Premium_PRD.md`](RVS_Premium_PRD.md). For architecture: [`RVS_Technical_PRD.md`](RVS_Technical_PRD.md).

---

## 2. The Three-Phase Structure

```
Phase 0 — Repositioning (Weeks 1-2)
    Legal foundation, taxonomy v1, design partner outreach
    → No engineering

Phase 1 — Solo + Professional MVP (Sprints 1-25, ~6 months)
    Self-serve Solo signup, multi-location coordination, asset ledger writes
    → First Solo paying customer ~month 6-7
    → First Pro paying customer ~month 7-8

Phase 2 — Premium + Anonymization (Months 7-12)
    SAML, SCIM, audit log, DMS integration, anonymization pipeline,
    tiered benchmarking, verification gate
    → First Premium paying customer ~month 9-12

Phase 3 — Enterprise Scale + OEM Pilot (Months 13-18)
    Multi-DMS, custom analytics, SOC 2 Type I, first Enterprise Scale customer,
    first OEM pilot
    → First Enterprise Scale customer ~month 15-18
    → First OEM contract signed ~month 18
```

Each phase has a hard ship gate. Phase n+1 does not start until Phase n meets its ship criteria. This is a hard discipline — chasing later-phase work before earlier-phase ships is the most reliable failure mode for solo founders.

---

## 3. Phase 0 — Repositioning (Weeks 1–2)

**Owner:** Founder (no engineering work)

**Deliverables:**

| Item | Description | Owner | Done When |
|---|---|---|---|
| ToS / MSA / DPA review | Counsel reviews and finalizes language for cross-dealer aggregation, OEM commercialization, liquidated damages clause for benchmarking misuse, no-opt-out position | Legal counsel | Approved templates in hand |
| Section 10A taxonomy v1 | Domain SME (RV service experience) finalizes initial controlled vocabulary list across categories, components, failure modes, repair actions | Founder + SME | Document published, JSON schema generated |
| Competitor language audit | Review existing collateral for forbidden phrases per `RVS_Competitive_Strategy.md` §10; remove all instances | Founder | Audit complete, marketing materials updated |
| Design partner identification | Identify 3-5 candidate dealerships across Solo and Pro tiers in Mountain West for design partner relationships | Founder | List of qualified candidates with first-conversation status |
| OEM target profiling | Profile Tier A OEM targets (Brinkley, Alliance, Grand Design) per `RVS_OEM_GoToMarket.md`; identify warm-introduction paths | Founder | Profiles complete, intro paths documented |
| Stripe billing setup | Set up Stripe production account, configure tax handling (Stripe Tax), payment method requirements, webhook endpoints for Phase 1 integration | Founder | Account live, webhook URLs registered |
| Auth0 production tenant | Provision Auth0 production tenant, configure Free plan settings, set up Login Action for `app_metadata` claim injection | Founder | Tenant configured, basic claim flow tested |

**Phase 0 ship criterion:** all of the above complete. No engineering work has begun.

---

## 4. Phase 1 — Solo + Professional MVP (~25 sprints, ~6 months)

**Owner:** Founder (solo development)

**Goal:** Ship Solo and Professional tiers in production. Self-serve Solo signup with Stripe trial. Cross-location coordination features for Pro tier customers. Asset ledger writes from day one. Industry benchmarking deferred to Phase 2.

### 4.1 Sprint Map

| Sprint | Focus | Output |
|---|---|---|
| **1** | Solution scaffold | .NET 9 solution, Aspire AppHost, Cosmos local emulator, Auth0 dev tenant, CI/CD baseline |
| **2** | Cosmos schema | All 9 containers (+ `verificationQueue` for FR-VG-01) provisioned via IaC; partition keys verified |
| **3** | Auth & middleware | Auth0 JWT validation, ClaimsService abstraction, multi-tenant middleware, ProblemDetails error envelope |
| **4** | Tenant + location services | CRUD for `Dealership`, `Location`, `TenantConfig`; slug routing via `slugLookup` cache |
| **5** | Taxonomy enforcement | `lookupSets` provisioning, taxonomy validation pipeline, version metadata |
| **6** | Intake API foundation | `POST api/intake/{slug}/service-requests` with 7-step orchestration |
| **7** | AI Wave 1 | VIN extraction, transcript cleanup, category suggestion, AI technician summary; gpt-4o-mini integration |
| **8** | Asset ledger writes | Mandatory ledger writes within orchestration; async pipeline; failure alerting |
| **9** | Solo manager dashboard | Single-location queue, search, detail, attachments view (Blazor WASM) |
| **10** | Magic-link customer status | Anonymous status page, magic-link token validation |
| **11** | Technician mobile app | MAUI Blazor Hybrid, offline-first SQLite, VIN/QR scan, photo capture, Section 10A capture wizard |
| **12** | Notification + webhook + capability assessment | ACS email/SMS, outbound webhook with HMAC signing, capability assessment endpoint |
| **13** | Stripe billing infra (1 of 2) | Stripe customer/subscription provisioning, payment method capture at signup, trial start, banded per-location billing logic (Solo flat $39; Pro $79/$69/$59 by 1-9/10-24/25-49 loc bands) |
| **14** | Stripe billing infra (2 of 2) | Trial end conversion (Solo or Pro per FR-020), failed payment grace period, dashboard billing settings, Stripe webhook handling, tier transition endpoints (Solo→Pro upgrade flow) |
| **15** | Self-service user provisioning | Auth0 Management API integration, invitation flow, role assignment UI, deactivation |
| **16** | Multi-location data model in UI (1 of 2) | Pro tier UI scaffolding: tenant has multiple locations, location switcher, multi-loc queue endpoint |
| **17** | Multi-location data model in UI (2 of 2) | Cross-location queue UI, search filtering by `locationId[]`, regional manager `regionTag` claim |
| **18** | Service Board (drag-and-drop) | Kanban-style status columns, optimistic concurrency, long-polling refresh |
| **19** | Cross-location asset history | `GET api/assets/{assetId}/history` with tenant scope; UI view sorted by service date |
| **20** | Regional manager hierarchy | `dealer:regional-manager` role with `regionTag`, server-side filtering, regional admin UI |
| **21** | Cross-location analytics (1 of 2) | RECT, volume, top failure modes by location/region/month; Cosmos aggregation queries |
| **22** | Cross-location analytics (2 of 2) | Drill-down corporate→region→location→SR; CSV export; period comparison |
| **23** | Warranty leakage analytics | Identification rules for likely-mislabeled SRs; review queue UI; this is Pro's load-bearing ROI feature |
| **24** | Multi-loc onboarding tools | Bulk location import (CSV), location templates, bulk user import |
| **25** | Polish, telemetry, design partner onboarding | Performance tuning, App Insights baselines, customer-facing documentation, runbook |
| **26-27** | Reserve / slip buffer | Estimated 1-2 sprint buffer for the inevitable unplanned work |

**Verification gate workflow:** built incrementally across Sprints 13-15 alongside billing and user provisioning, since both flows surface admin queues. The verification queue admin UI ships in Sprint 25 polish; the API and data model ship in Sprint 13-14 alongside Stripe.

**Audit log infrastructure:** stub event capture starts in Sprint 12 alongside webhook (both write to similar event-log patterns). Full audit log container provisioning in Sprint 15. Premium-tier audit log query/export endpoints don't ship until Phase 2, but capture is on from Phase 1.

### 4.2 Phase 1 Ship Criteria

Per `RVS_Technical_PRD.md` §3.2:

- 5 design partners actively using Solo or Professional
- ≥2 of 5 design partners on Pro tier with multi-location features in active use
- Asset ledger ≥1,000 events
- Taxonomy adherence ≥95%
- Intake P95 latency <3s
- Stripe billing flows tested end-to-end (signup → trial → conversion → renewal → cancellation → failed payment recovery)
- Self-service user provisioning works end-to-end
- Verification gate operational with documented review SLA
- Audit log infrastructure capturing required event types
- Zero P1 incidents in last 30 days

**Phase 1 ship gate:** all of the above met. No Phase 2 work begins until then.

### 4.3 Phase 1 Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Sprint slips on AI integration (Sprint 7) | Have rule-based category fallback ready; AI degradation must not block intake |
| Stripe billing edge cases consume more time than budgeted | Budget 1 full sprint of buffer (Sprint 26 reserve); engage Stripe support early on tax/refund/proration questions |
| Design partner recruitment takes longer than 5 months | Run partner outreach in parallel with Phase 1 build, not after; have 8-10 candidate partners by Sprint 12 |
| Multi-location features (Sprint 16-24) reveal architectural gaps | Architecture review at Sprint 15; rework windows in Sprint 26-27 if needed |
| Verification queue manual labor exceeds founder bandwidth | Founder owns initially; success engineer hire targeted Sprint 18-25; semi-automated state license registry checks via Sprint 22+ |

---

## 5. Phase 2 — Premium + Anonymization (Months 7–12)

**Owner:** Founder + first hire (success engineer or Premium tier customer success); engineering contractors as needed

**Goal:** Ship Premium tier in production. Auth0 Organizations, SAML, SCIM, audit log query/export, IP allowlisting, bidirectional DMS integration with one partner. Anonymization pipeline operational. Tiered industry benchmarking shipping. Verification gate fully operational with backlog cleared.

### 5.1 Phase 2 Sprint Themes

| Quarter / Block | Focus |
|---|---|
| Q1 (months 7-9) | Anonymization pipeline + variable k-anonymity engine + verification gate ship + tiered benchmarking API |
| Q1 (months 7-9) | Auth0 Organizations migration tooling, SAML configuration UI, IP allowlist middleware |
| Q1-Q2 (months 8-10) | Audit log search/export endpoints (FR-ENT-04, FR-ENT-05) |
| Q2 (months 10-12) | First DMS partner integration (IDS or Lightspeed); reconciliation dashboard; mixed-DMS support |
| Q2 (months 10-12) | Premium banded billing logic (FR-BILL-04 extension to Premium $119/$109/$99 bands); Pro→Premium upgrade flow with 50% off first 3 months transition discount mechanics (FR-BILL-08) |
| Q2 (months 10-12) | Support tier infrastructure (Standard/Priority/Critical billing per FR-BILL-07); banded implementation fee billing per FR-BILL-09 ($5K/$7.5K/$10K Premium; $15K/$25K/$40K Enterprise Scale) |
| Q2 (months 10-12) | SOC 2 Type I preparation (controls documentation, evidence collection, auditor selection) |

**Why DMS integration is Phase 2 not Phase 1:** DMS integration is Premium tier feature, and Premium customers don't exist until Phase 1 ships and a Pro customer upgrades to Premium. Building DMS integration before having a customer waiting for it is premature; the partner relationship and integration scope are clarified by the first real customer.

**Why support tier infrastructure ships in Phase 2 (not Phase 1):** Phase 1 ships with Standard support implicit (1 business day, founder-handled). Priority and Critical support tier billing is unnecessary until Premium customers exist (whose engagement profile justifies the upgrade) and until success engineer is hired (month 9 per §5.3). Adding support tier infrastructure to Phase 1 would be over-engineering ahead of need.

### 5.2 Phase 2 Ship Criteria

Per `RVS_Technical_PRD.md` §3.3 — full criteria including SAML validation, SCIM validation, anonymization pipeline operational, k-anonymity enforced, DMS bidirectional integration in production at a Premium design partner, ToS/MSA/DPA reviewed by counsel, 1 paying Premium customer signed.

### 5.3 Phase 2 Hires

By month 9:
- **Success engineer or customer success lead** ($90K-$130K base + equity) — owns design partner success, handles verification queue overflow, runs Premium tier onboarding

By month 12 (if revenue supports):
- **Backend engineer** ($120K-$160K base + equity) — focuses on DMS integration, SAML/SCIM, anonymization pipeline depth

If Phase 2 revenue is under $150K ARR by month 12, hold the second hire and continue solo. Founder bandwidth is the binding constraint.

---

## 6. Phase 3 — Enterprise Scale + OEM Pilot (Months 13–18)

**Owner:** Founder, success engineer, backend engineer, plus contracted resources for SOC 2 audit and OEM pilot infrastructure

**Goal:** Ship Enterprise Scale tier features. Sign first Enterprise Scale customer. Sign first OEM pilot under contract. Reach OEM-grade dataset coverage for at least one Tier A target.

### 6.1 Phase 3 Sprint Themes

| Quarter / Block | Focus |
|---|---|
| Months 13-14 | Multi-DMS support (both IDS and Lightspeed simultaneously); custom analytics builder design and implementation; custom data export endpoints |
| Months 14-15 | 24/7 support model setup (on-call rotation, support tooling, runbooks); Enterprise Scale onboarding playbook |
| Months 15-16 | SOC 2 Type I attestation (auditor engagement, evidence package, attestation letter) |
| Months 16-17 | First Enterprise Scale customer onboarding (sales-led; expected month 15-18) |
| Months 17-18 | OEM pilot infrastructure: data scientist's view of `industryDataset`, OEM-specific query templates, OEM data licensing API |
| Month 18 | First OEM pilot under contract ($50K-$150K) |

### 6.2 Phase 3 Ship Criteria

- 3+ paying Premium customers
- Multi-DMS support validated at a Premium customer
- Custom analytics builder in production
- 24/7 support model operational
- SOC 2 Type I attestation obtained
- First Enterprise Scale customer signed (sales-led)
- First OEM pilot under contract
- Asset ledger ≥50,000 events with ≥5% installed-base coverage of one major OEM

### 6.3 Phase 3 Hires

By month 15-18, if revenue supports:
- **Backend engineer #2** ($140K-$190K base + equity) — owns Enterprise Scale technical delivery (multi-DMS, custom analytics, custom data exports per `RVS_Premium_PRD.md` FR-ES-001 through FR-ES-003), absorbs solutions-engineer FTE allocation for first 1-2 ES customers
- **VP Sales / Head of GTM** ($180K-$240K base + commission + equity) — owns Premium and Enterprise Scale sales motion
- **Data scientist or analyst** ($150K-$200K base + equity) — owns dataset quality, OEM pilot deliverables, benchmarking accuracy

These hires depend on cumulative ARR reaching ~$1M and Premium tier validation.

### 6.4 Dedicated Solutions Engineer Capacity Model (v3.4)

`RVS_Premium_PRD.md` FR-ES-011 commits a named RVS engineering team member as a **dedicated solutions engineer** for each Enterprise Scale customer. FTE allocation per ES customer:

| ES customer size | Solutions engineer FTE per customer |
|---|---|
| 50–149 locations | 0.10–0.25 FTE |
| 150+ locations | 0.25–0.50 FTE |

Annualized cost basis (loaded compensation $200K-$280K for a backend engineer): $20K–$140K of dedicated engineering capacity per ES customer per year. This is the largest single cost driver justifying the ES per-location rate above Premium and is the largest non-revenue cost line item the model needs to absorb.

**Cumulative FTE load at base case (per `§7.1` ARR forecast):**

| Month | ES customers | Avg loc | Estimated FTE load | Annualized engineering cost |
|---|---|---|---|---|
| 18 | 1 | 55 | 0.15 | ~$36K |
| 24 | 2 | 65 | 0.30 | ~$72K |
| 36 | 4 | 80 | 0.70 | ~$168K |

**Optimistic case (10 ES customers at 110-loc avg by M36):** ~3.0 FTE load (~$720K annualized). At that scale, the solutions engineer role becomes a dedicated team rather than a side-of-desk allocation.

**Hiring implication:** The first Enterprise Scale customer (expected month 15-18 per `§6.1`) requires solutions-engineer capacity that exceeds founder bandwidth alone. The Phase 3 backend engineer #2 hire (this section, above) is timed specifically to absorb this load — it is not optional once an ES contract is signed. If ES sales-cycle outruns engineering hiring, the ES contract should be paced to match (delaying contract signature, scoping initial deployment narrowly) rather than overcommitting the founder.

**Implication for Phase 3 success engineer hire (per `§5.3`):** The success engineer hire from Phase 2 owns business relationship, adoption, verification queue, and Premium tier success management. The Phase 3 backend engineer #2 owns ES technical delivery. These are distinct roles with distinct skill profiles; do not collapse them into a single hire.

---

## 7. Revenue Targets and Runway Honesty

### 7.1 Conservative Revenue Targets

Targets recalibrated for v3.3 banded pricing. Net effect vs v3.0: small Solo and small Pro ACVs unchanged (1-9 loc bands match v3.0); large Pro ACVs increased modestly (volume bands kick in at 10+ loc); small Premium ACV decreased significantly (5-loc Premium dropped from $13.2K to $6.1K with per-user pricing eliminated); large Premium ACV broadly comparable (volume discount in bands offsets per-user fee elimination); Enterprise Scale ACV broadly comparable to v3.0 (floor lowered to $150/loc but ceiling matches). Aggregate revenue is ~neutral; mix shifts toward larger customers for material ARR contribution.

| Milestone | Cumulative ARR | Composition |
|---|---|---|
| Month 6 (Phase 1 ship) | $0-$5K | Pre-launch, design partners free |
| Month 9 | $25K-$60K | 10-15 Solo customers, 1-2 Pro |
| Month 12 | $50K-$150K | 20-40 Solo, 3-5 Pro, first Premium pilot |
| Month 18 | $200K-$400K | 60-100 Solo, 10-15 Pro, 2-3 Premium |
| Month 24 | $500K-$1M | 100-180 Solo, 25-40 Pro, 5-8 Premium, first Enterprise Scale |
| Month 36 | $1.5M-$3M | Premium dominates revenue; first OEM contract material |

These are conservative because v3 is a four-tier model that takes longer to ramp than a single-tier mass-market product. The Solo tier is necessary for runway and dataset growth but does not produce big-ticket ARR. Premium tier (especially at 10-25 loc where banded pricing produces $11K-$25K ACV) is where ARR scales meaningfully; Enterprise Scale and OEM are where it becomes exceptional.

**Implementation fees (one-time, banded per `RVS_Premium_PRD.md` FR-PR-014 and FR-ES-008) are NOT included in ARR figures above.** They contribute meaningfully to cash but not to recurring revenue. A reasonable Phase 2 cash addition from implementation fees: $25K-$75K cumulative by month 12 (assuming 3-5 Premium signings × $5K-$10K each).

### 7.2 The Runway Concern

Phase 1 elongation pushes first revenue to ~month 6-7. Honest accounting:

- **Month 0-6:** No revenue. Cash burn from founder labor (or salary if drawing one), Azure infrastructure (~$200-$500/mo at design partner scale), Auth0 Free (no cost), Stripe Test (no cost), counsel review (one-time ~$5K-$15K), tools/software (~$200/mo).
- **Month 6-9:** Slow Solo signups. Revenue ramps from $0 to ~$2K-$5K MRR ($24K-$60K ARR) by month 9. Insufficient to cover any significant cash outlay beyond infrastructure.
- **Month 9-12:** First Pro upgrades (5-loc Pro at $395/mo MRR each starts adding up; 10-loc Pro at $690/mo MRR more meaningful); first Premium pilot ($595-$1,090/mo MRR for 5-10 loc Premium, possibly with first-3-months 50% discount). MRR by month 12 in $5K-$15K range. First Premium implementation fee ($5K) provides one-time cash injection.
- **Month 12-18:** Sustained growth. MRR by month 18 in $20K-$40K range, beginning to support first hire. Cumulative implementation fees from 2-4 Premium signings ($10K-$30K cash, non-recurring) extend runway.

**The runway requirement to make this work:** the founder must have at least 9-12 months of personal runway (no salary draw) before Phase 1 ship, plus enough business capital to cover infrastructure and Phase 0 legal review. A reasonable estimate: **$50K-$100K total cash buffer** (plus founder's personal living costs covered by other means) to bridge to month 9-12 where revenue can begin to support the founder's salary draw.

If this runway is not available, the strategy needs adjustment:
- Compress Phase 1 by deferring some Pro features (e.g., warranty leakage analytics) to early Phase 2
- Accept slower Phase 1 (sprint per week instead of sprint per 2 weeks) and stretch timeline
- Take design partner pre-payments (3-6 months of Solo at signup) to fund early operations
- Raise a small bridge round from friends-and-family or a relevant strategic angel

This is documented honestly because pretending the runway problem doesn't exist makes it harder to solve.

### 7.3 Burn Profile by Phase

```
Phase 0 (weeks 1-2):
  Cash out:    $5-15K (counsel) + $500 (infrastructure) = ~$10K
  Cash in:     $0
  Net:         -$10K

Phase 1 (months 1-6):
  Cash out:    Founder cost (variable), infra ~$3K total, tools ~$1.5K, marketing $0
               (assuming founder draws no salary, ~$5K hard outlays)
  Cash in:     $0
  Net:         -$5K hard, plus founder opportunity cost

Phase 2 (months 7-12):
  Cash out:    Infra scaling to ~$1K/mo, tools ~$300/mo, possible first hire month 9-12
               at ~$10K-$15K/mo, SOC 2 prep ~$5K-$15K
               Total: $30K-$80K hard cost
  Cash in:     $30K-$70K of cumulative MRR booked (at $5-15K MRR by month 12)
  Net:         Roughly break-even on hard costs; founder still drawing nothing

Phase 3 (months 13-18):
  Cash out:    Infra ~$2K/mo, full-time staff $25-40K/mo, SOC 2 audit ~$15K-$30K,
               sales/marketing $5-10K/mo, OEM pilot infra $5-10K
               Total: $200-$400K hard cost
  Cash in:     $80-$160K cumulative MRR + first Enterprise Scale contract $20-50K +
               first OEM pilot $50K-$150K
               Total: $150-$360K
  Net:         Roughly break-even to mildly negative; founder may begin drawing salary
               from month 15 if revenue trajectory supports
```

The picture is: this is a long bridge to profitability, and the founder is the bridge. The four-tier model funds the bridge but doesn't shorten it.

---

## 8. Success Metrics by Phase

### 8.1 North Star (All Phases)

**Asset ledger health metrics** (per `RVS_data_moat.md` §9):
- Total service events
- Events with full Section 10A
- Distinct VINs covered
- OEM/model coverage
- Top OEM coverage rate
- Geographic spread
- Avg days between event and 10A
- Taxonomy adherence rate
- Tier distribution of contributors

If the dataset is healthy and growing, the strategy is working — even if revenue is below target.

### 8.2 Phase-Specific Targets

| Phase | Primary Metric | Secondary Metric |
|---|---|---|
| Phase 0 | Phase 0 deliverables checklist | Design partner candidates identified |
| Phase 1 | 5 active design partners; 1,000 ledger events | 95% taxonomy adherence; <3s P95 intake latency |
| Phase 2 | 1 paying Premium; 10,000 ledger events | First DMS bidirectional integration in production |
| Phase 3 | First OEM pilot signed; 50,000 ledger events | 5% installed-base coverage of one OEM; first Enterprise Scale customer |

---

## 9. Hire Plan and Runway Sensitivity

### 9.1 Hire Sequencing

| Hire | Earliest | Latest | Trigger | Cost (annualized) |
|---|---|---|---|---|
| Success engineer / CS lead | Month 9 | Month 12 | First Premium customer signed OR verification queue exceeds 4 hours/day | $90K-$130K |
| Backend engineer #1 | Month 12 | Month 18 | DMS integration depth requires dedicated owner OR cumulative ARR >$200K | $120K-$160K |
| VP Sales / Head GTM | Month 15 | Month 21 | First Enterprise Scale conversation reaches contract terms | $180K-$240K + commission |
| Data scientist | Month 15 | Month 24 | First OEM pilot signed | $150K-$200K |
| Backend engineer #2 | Month 18 | Month 30 | Multi-DMS support shipping AND backend bandwidth constrained | $120K-$160K |

**The discipline:** no hire is made before its earliest date even if revenue could support it; founder bandwidth is the binding constraint and these hires are about scaling beyond it. Equally: no hire is delayed past its latest date even if cash is tight; hires past these points imply the business isn't growing.

### 9.2 Runway Sensitivity

Best-case scenario (faster Pro adoption, first Premium by month 9):
- Month 12 ARR: $150K
- First hire month 9: success engineer
- Backend engineer hired month 14-15
- Cumulative cash burn through month 18: ~$300K against ~$300K cumulative revenue → roughly break-even

Base-case scenario (per §7.1 targets):
- Month 12 ARR: $80K
- First hire month 11-12
- Backend engineer hired month 16-18
- Cumulative cash burn through month 18: ~$300K against ~$200K cumulative revenue → ~$100K shortfall, requires bridge funding

Worst-case scenario (slow Solo adoption, no Premium until month 12):
- Month 12 ARR: $40K
- First hire delayed to month 14-15
- Backend engineer delayed to month 20+
- Cumulative cash burn through month 18: ~$200K against ~$100K cumulative revenue → ~$100K shortfall, requires bridge funding or strategy adjustment

**Conclusion:** in all but best-case, some external funding is required at month 12-15. Either:
- Friends-and-family or angel round ($150K-$300K)
- Premium customer prepayments (12-month annual prepay accepted)
- Founder personal capital injection
- Strategy reduction (defer Premium tier, slow growth)

Plan for this conversation by month 9-10, not month 14 when cash is critical.

---

## 10. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Phase 1 elongates beyond 25 sprints | Medium-High | High (delays revenue) | Sprint 26-27 buffer; scope cuts on Pro features as last resort |
| Design partner conversion slower than 5 in 6 months | Medium | High (no ARR foundation) | Pre-seeded partner list of 10+ before Sprint 13; founder owns sales |
| Stripe billing edge cases break trial conversion | Low-medium | High (revenue interruption) | Sprint 14 dedicated to edge cases; Stripe support engagement |
| Auth0 Free → Organizations migration breaks existing users | Low | High (Premium upgrade blocker) | Test extensively in Phase 1.5; staged rollout; support direct intervention |
| First DMS integration takes longer than 1 quarter | Medium | High (Premium ship delay) | Start partner-program conversations Phase 1; have one DMS in pre-integration discussion before Sprint 25 |
| Founder burnout in solo Phase 1 | Medium-High | Critical | Sustainable pace (40-50 hrs/wk); buffer sprints; hire by month 9 |
| Verification gate manual queue overwhelms founder | Medium | Medium | Targeted Sprint 22+ semi-automation against state license registries |
| Anonymization pipeline complexity exceeds Phase 2 budget | Medium | High | Variable k-anonymity is the hard part; budget Q1 of Phase 2 entirely for this; ship deferred over scope reduction |
| First Premium customer demands feature not in Premium PRD | High | Medium | Documented "Premium customer concession workflow"; up to 1 sprint of customer-specific work permitted; beyond that, becomes Enterprise Scale custom |
| Cash runway insufficient | Medium-High | Critical | Conversation with potential angels by month 9-10; annual prepay option for design partners; founder personal runway buffer |

---

## 11. Decision Logs and Document Hygiene

### 11.1 Authoritative Documents

The following documents are authoritative and should be the only sources consulted for their respective domains:

- Strategy and positioning: `RVS_Competitive_Strategy.md` v3.0
- Architecture: `RVS_Technical_PRD.md` v3.0
- Solo/Pro requirements: `RVS_PRD.md` v3.0
- Premium/Enterprise Scale requirements: `RVS_Premium_PRD.md` v1.0
- Data moat: `RVS_data_moat.md` v3.0
- OEM strategy: `RVS_OEM_GoToMarket.md` v1.0
- Implementation plan: this document
- Platform overview: `RVS_Context.md` v3.0

### 11.2 Deprecated / Archived

The following documents are retained for archival only and should NOT be consulted for active decisions:

- v1.x and v2.x of any document above
- `RVS_Enterprise_PRD.md` (replaced by `RVS_Premium_PRD.md`)
- Standalone competitive one-pagers on Kenect, DMS, ServiceNomad
- `RVS_MultiIndustry_Expansion.md` (deferred indefinitely; retained for future reference)

### 11.3 Open Decisions Tracked Across Documents

Cross-reference table for decisions that span documents:

| Decision | Owner | Doc(s) | Status |
|---|---|---|---|
| Section 10A v1 taxonomy final list | Domain SME | `RVS_data_moat.md` §3 | Phase 0 |
| Liquidated damages dollar amount | Counsel | `RVS_data_moat.md` §5.1 | Phase 0 |
| First DMS partner (IDS vs Lightspeed) | GTM | `RVS_Premium_PRD.md` OQE-01 | Phase 2 kickoff |
| SOC 2 Type I before first Premium or Type II after first 3 | Compliance | `RVS_Premium_PRD.md` OQE-02 | Pre-Premium pitch |
| Per-user pricing true-up frequency (monthly vs quarterly) | Engineering | `RVS_Premium_PRD.md` OQE-07 | Phase 2 kickoff |
| Pro→Premium transition discount Stripe mechanics | Engineering | `RVS_PRD.md` OQ-06 | Phase 2 design |

---

*End of RVS_Implementation_Plan_v2.md v3.0.*
