# RVS vs. DMS — The Coexistence Value Proposition

**Version:** 3.4
**Date:** April 30, 2026
**Status:** Updated for 2025 platform reality, v3.4 doc set alignment, post-pivot positioning
**Supersedes:** v3.3 (earlier same-day), v3.0 (April 30, 2026), v2.0 (same-day), v1.0 (March 2026)

> **What changed in v3.4 (same-day refinement):** Version sync only. The v3.4 doc set introduced Enterprise Scale tier feature additions (dedicated solutions engineer per FR-ES-011, custom SLA negotiation per FR-ES-012, documented OEM revenue-share clause per FR-ES-007 rewritten). Those additions are referenced in §5.4 Enterprise Scale pitch where the bundled Critical support and dedicated success/solutions engineer make the price gap legible. No price changes. Pricing tables in §5 are unchanged from v3.3.

> **What changed in v3.3 (same-day refinement):** §5 pitches updated to reflect v3.3 banded per-location pricing. Pro pricing: $79/$69/$59 by 1-9/10-24/25-49 loc bands (vs v3.0's flat $299/mo + per-loc surcharge). Premium pricing: $119/$109/$99 by same bands (vs v3.0's $799/mo + per-loc + per-user). Per-user pricing language removed entirely (no per-user fees at any tier in v3.3). Enterprise Scale floor referenced at $150/loc; Critical support tier bundling referenced. Implementation fees now mentioned as one-time banded fees ($5K/$7.5K/$10K Premium; $15K/$25K/$40K Enterprise Scale).

> **What changed in v3.0:** v2 referenced "Enterprise tier" and "Free tier" throughout. v3 updated these references to the four-tier model — Solo / Professional / Premium / Enterprise Scale. The structural arguments were unchanged; tier mapping was clarified. Pitches in §5 rewritten to match the four-tier customer mapping.

---

## 1. The Headline (Unchanged)

**RVS does not compete with the DMS. RVS sits in front of the DMS, captures the data the DMS was never built for, and feeds the DMS structured events.**

The DMS handles accounting, parts, warranty claims, F&I — the back office. RVS handles intake, structured failure data, multi-location coordination, and the cross-dealer dataset. Different jobs. Different data. Different layers.

This positioning held in 2024. It holds even more clearly in 2026 because the 2025 DMS expansions did not address the gap RVS targets — they tightened the DMS's grip on its own job and made cleaner integration partners possible.

---

## 2. What the DMS Got Right in 2025 (And Why It Doesn't Change Our Position)

In 2025, both IDS and Lightspeed shipped material upgrades. Honest acknowledgment of what they did:

**IDS Astra G2 (2025 highlights):**
- Free OEM VIN decoding via VINRV partnership (April 2025)
- IDS Pay (embedded payments)
- Continued investment in workflow tools

**Lightspeed (2025 highlights):**
- VIN partnerships with Winnebago, Grand Design, Brinkley, Jayco, KZ, Alliance
- Lightspeed Service Scheduler (explicitly markets RECT reduction)
- Service Tech Video (technician documentation)
- AI-powered insights features in some modules

**What this means for RVS positioning:**

Some of the gaps RVS used to point at have closed. We are no longer winning by saying "VIN decoding is hard in your DMS" — it isn't anymore. We are no longer winning by saying "your DMS doesn't have a tech video tool" — Lightspeed has one.

**What this does NOT mean:** the structural argument for RVS hasn't weakened. The DMS is still a per-location accounting and work-order system. Even with Service Scheduler and Service Tech Video, the DMS:

- Captures complaint/cause/correction as free text — no structured failure taxonomy
- Operates per-location with no cross-location coordination layer
- Cannot perform cross-dealer benchmarking (each tenant's database is isolated)
- Is not architected for OEM-grade analytical data licensing
- Has not committed to anonymized cross-dealer aggregation as a strategic asset

**RVS's value proposition is not "we have features the DMS doesn't." It's "we are a fundamentally different category of product — service intelligence, not service operations — and we coexist with the DMS the way a CRM coexists with an ERP."**

This is a stronger position than feature-parity competition. Feature parity erodes; category positioning compounds.

---

## 3. The Real Differences That Still Matter

### 3.1 DMS Captures Billing Data; RVS Captures Service Intelligence

In a typical DMS work order:

```
Complaint:    free text (customer's words)
Cause:        free text (technician's diagnosis)
Correction:   free text (what was done)
Labor lines:  hours and rate
Parts lines:  parts and quantity
```

In an RVS structured service event:

```
Issue category:    controlled vocabulary (slide-system, electrical, etc.)
Component:         controlled vocabulary (hydraulic-pump, slide-motor, etc.)
Failure mode:      controlled vocabulary (seized, leaking, intermittent, etc.)
Repair action:     controlled vocabulary (replace, rebuild, recalibrate, etc.)
Parts:             part number list
Asset context:     VIN, manufacturer, model, year, ownership duration, mileage
Geographic:        state, region (anonymized for aggregation)
Lineage:           taxonomy version, AI categorization confidence, technician overrides
```

The same information lives in both systems. But the DMS version is queryable as text; the RVS version is queryable as data. **Across 100 repairs**, the DMS has 100 unique-ish text strings. RVS has 100 events keyed against five controlled vocabularies, fully aggregable.

A DMS report can answer "how much warranty revenue this month?" An RVS analytics view can answer "what failure mode is over-represented in 2023 model-year units of this manufacturer in months 12–24?"

Both questions matter. They're different questions, and they require different data architectures.

### 3.2 The DMS Operates Per-Location; RVS Operates Across the Corporation

Even at multi-location dealer groups, each DMS instance is fundamentally a single-location accounting system. Corporate visibility means consolidating reports manually or running expensive BI projects across multiple databases.

RVS is architected from the ground up as multi-tenant with the corporation as the tenant boundary. A 30-location dealer group sees:
- A unified service request queue with regional and per-location filters
- Cross-location asset history (one VIN, one customer, every visit at any of 30 locations)
- RECT and warranty leakage benchmarking across all 30 locations
- Regional manager hierarchies with `regionTag`-scoped role-based access
- One service intelligence dataset, not 30 separate ones

This is the structural feature the Professional and Premium tiers sell. It is the one differentiator that does not depend on what the DMS ships next quarter — because retrofitting a per-location accounting system into a multi-location coordination platform is a multi-year architectural rewrite, not a feature.

### 3.3 The DMS Cannot Aggregate Across Dealers; RVS Can (and Will)

This is the thesis that fundamentally changes the conversation:

**RVS is being built as a structured failure-data platform that anonymizes and aggregates across dealers, with the explicit commercial intent to license the resulting dataset to OEMs.**

The DMS cannot do this. It would require:
- Rewriting the work order schema to enforce structured taxonomies
- Building an anonymization pipeline with k-anonymity guarantees
- Establishing customer ToS language that grants cross-dealer aggregation rights
- Negotiating commercial relationships with OEMs as a data customer category

Each of these is a multi-quarter strategic commitment, and none of them aligns with the DMS's identity as a per-dealer accounting system. **It is not impossible — it is just unlikely, because it would mean the DMS becoming a different kind of company.**

For the dealer, this means RVS offers something the DMS structurally cannot: visibility into industry-level failure patterns through tiered benchmarking (basic at Solo, advanced at Pro, full custom at Premium), and (eventually) a stake in the OEM data ecosystem the dealership contributes to.

### 3.4 Intake Data Lives in RVS; the DMS Sees Only the Final Repair

This was true in v1.0 of this document and remains true. The customer's first symptom description, photos, urgency, contact preference, and pre-arrival diagnostic context all live in RVS. The DMS gets the work order after the unit arrives and the technician opens a job.

Even with Lightspeed's Service Scheduler reducing the friction of getting the unit into the bay, the DMS's data starts when the bay door opens. RVS's data starts when the customer first calls — which means RVS has a layer of context the DMS will never natively have.

---

## 4. The Coexistence Architecture

```
Customer
   ↓
RVS Intake (anonymous web portal)
   ↓
Structured Service Request (with AI categorization, photos, magic-link)
   ↓
   ├──→ AssetLedgerEntry (RVS dataset, controlled vocabulary)
   │        ↓
   │    industryDataset (anonymized)
   │        ↓
   │    Enterprise benchmarking + future OEM licensing
   │
   └──→ DMS Work Order (RVS → DMS bidirectional integration)
            ↓
        Repair execution, accounting, warranty filing, parts, F&I (DMS handles)
            ↓
        Final repair data → RVS for ledger enrichment
```

The DMS keeps doing what it does well. RVS adds the layer that doesn't exist today and would be expensive for the DMS to build. **Both systems are stronger together than either is alone.**

The Premium and Enterprise Scale tier bidirectional integration is real (per `RVS_Premium_PRD.md` FR-PR-008). For Solo and Professional tier dealers, the integration is manual CSV download — sufficient for the operational scale of those tiers.

---

## 5. The Pitches — By Tier

### 5.1 The Solo Pitch (1–4 Location Dealer or Mobile Operator)

> Your DMS handles the work order — or maybe you don't have a DMS yet because you're a mobile operator. Either way, RVS handles structured intake at $39/location/month. Customer submits a service request from their phone with photos and a structured description. AI categorizes it. You open a triaged queue. Your tech sees a pre-diagnosed job. Daily CSV download to your DMS or accounting system. Basic industry benchmarking included. **The pitch is: structured intake at a price comparable to a CRM tool, plus contribution to (and benefit from) an industry-wide service dataset.**

This pitch competes against doing nothing (phone-based intake), against $30 CRM tools (different category), and against ServiceNomad (different price point). It does not compete against the DMS.

### 5.2 The Professional Pitch (5–15 Location Dealer Group)

> Your DMS is the system of record at each location. RVS Professional is the system of record across the corporation, at $79/location/month for 1-9 locations, dropping to $69/location at 10-24 locations and $59/location at 25-49 locations. Cross-location queue and search. Regional manager dashboards. Cross-location asset history. Warranty leakage analytics that typically pay for the platform multiple times over. Advanced industry benchmarking. **The pitch is: the multi-location coordination and warranty recovery layer your DMS structurally doesn't offer, at a price comparable to a mid-tier DMS subscription.** A 5-location group pays $395/month; a 10-location group pays $690/month; a 25-location group pays $1,475/month.

This pitch competes against internal BI projects and against paying consultants to consolidate reports. It does not compete against the DMS.

### 5.3 The Premium Pitch (Sophisticated Dealer Group with IT)

> Your DMS is the operational system. Your IT team has SAML SSO requirements, audit log compliance needs, and integration architecture standards. RVS Premium adds bidirectional API integration with IDS or Lightspeed, SAML and SCIM, comprehensive audit log, IP allowlisting, 99.9% SLA, and dedicated success management. Premium pricing is per-location: $119/loc for 1-9 loc, $109/loc for 10-24 loc, $99/loc for 25-49 loc. A 5-location dealer group pays $595/month; a 15-location group pays $1,635/month; a 25-location group pays $2,475/month. All users (technicians, advisors, managers) are unlimited at every tier — no per-seat fees. Implementation fee is one-time and banded by location count ($5K/$7.5K/$10K). **The pitch is: enterprise-grade compliance and integration on top of the multi-location coordination Pro provides — purpose-built for dealer groups whose IT and procurement teams expect SaaS to behave like SaaS, with simple per-location pricing that doesn't penalize you for having staff.**

This pitch competes against internal IT-led custom solutions and (eventually) against ServiceNomad if they pivot to enterprise. It does not compete against the DMS.

### 5.4 The Enterprise Scale Pitch (50+ Location Dealer Group)

> At 50+ locations, the conversation is different. Custom contract. Multi-DMS support simultaneously. Custom analytics builder. Critical support tier bundled (1-hour response, dedicated CSM channel, 24/7 on-call for production-impacting incidents — exclusive to ES, not available as a Premium add-on). **Dedicated solutions engineer** — a named member of our engineering team committed to your account at 0.10–0.50 FTE depending on contract size, owning DMS integration architecture and technical escalation. **Custom SLA terms** — uptime targets up to 99.99%, service credits for SLA breach, named maintenance windows, mutual NDA-protected post-incident reviews. Quarterly executive business review. **OEM data partnership coordination with documented revenue-share clause** — when your data represents ≥15% of an OEM-licensed aggregate, you receive 5–10% of that OEM contract's annual value, paid quarterly. Pricing is negotiated within published per-location range: $150/loc/month floor at 50 locations, scaling to $300/loc/month at the largest sizes. A 50-location starting contract begins at $7,500/month; a 100-location group typically lands $15K-$25K/month. Implementation fee one-time and banded ($15K/$25K/$40K). **The pitch is: a sales-led commercial relationship with the platform that owns your industry's structured service intelligence layer — with dedicated engineering, custom SLA terms, and economic alignment to OEM data licensing built into the contract.**

This pitch is a custom-contract conversation, not a published-pricing pitch. The path is direct sales engagement, not self-serve. The price gap from Premium ($99/loc at 25-49 loc band) to ES ($150-$300/loc) is justified by genuine product/service deliverables (multi-DMS engineering, custom analytics build, dedicated solutions engineer, bundled Critical support, custom SLA) and contract structure (OEM revenue-share clause), not tier-naming.

---

## 6. The Sales Objection Table (Specific to DMS Comparisons)

| Objection | Response |
|---|---|
| *"Doesn't my DMS already do this?"* | The DMS captures the work order. RVS captures the diagnosis — what failed, how, and how often across thousands of similar units. Different question, different data, different layer. |
| *"Lightspeed has a Service Scheduler that reduces RECT."* | Scheduling reduces RECT after the unit arrives. We reduce RECT before it arrives — by giving the tech the symptoms, photos, and category before the bay door opens. Different lever, complementary effect. |
| *"IDS just released free VIN decoding."* | Right, and so do we. VIN decoding is parity now. The differentiation is what happens after the VIN is captured: structured failure taxonomy, cross-location asset history, aggregable analytics. |
| *"Lightspeed Service Tech Video lets technicians document repairs."* | Documentation that lives in your DMS as text or video is searchable per-location but not aggregable across dealerships. Our structured Section 10A taxonomy is. Different artifacts, different uses. |
| *"My DMS has analytics."* | DMS reports answer financial questions: revenue, hours, parts margins. Service intelligence answers operational and engineering questions: which models fail, which repairs are slow, which parts to stock. Different analytical model. |
| *"Are you trying to replace my DMS?"* | No, never. We coexist with IDS, Lightspeed, EverLogic, Motility. Replacing the DMS is a 10-year project we don't want. We sit in front and feed structured data into your existing system. |
| *"Why would I add another system?"* | Because nothing in your stack today gives you industry-level failure data, multi-location coordination, or OEM-grade benchmarking. If you have all three already, you don't need RVS. |
| *"What if my DMS provider builds this?"* | They might build a service intake module. They are unlikely to build a cross-dealer anonymized aggregation platform with OEM commercialization — that's a different company. |

---

## 7. The Forbidden Phrases (Removed from Sales Conversation)

These claims were defensible in 2024 but are no longer:

- ❌ "VIN scanning is hard in your DMS." (IDS and Lightspeed have free VIN decoding now.)
- ❌ "Your DMS has no payment processing." (IDS Pay exists; Lightspeed has options.)
- ❌ "Your DMS has no AI." (Multiple DMS vendors have shipped AI-flavored modules.)
- ❌ "Your DMS lacks technician video / mobile tools." (Lightspeed has these.)
- ❌ "We replace your DMS." (We do not, ever.)
- ❌ "DMS systems are stuck in 1995." (They are not. They have shipped meaningfully in the last 18 months.)

The argument has shifted from "we have things the DMS lacks" to **"we are a different category of product, and the DMS cannot be both an accounting system and a multi-dealer service intelligence platform without becoming a different company."** The second argument is harder to dismiss because it is structural, not feature-list comparison.

---

## 8. The Partner Path — Why Coexistence Is Strategic, Not Just Polite

RVS is pursuing Technology Partner status with both IDS Astra and Lightspeed. The reason:

1. **Distribution channel.** A "Recommended Partner" badge in the IDS or Lightspeed marketplace is more credible than cold outreach.
2. **Defensibility.** Once integrated as a sanctioned partner, the cost for the DMS to build a competing intake module rises (they would have to break their own partner ecosystem).
3. **Customer trust.** Dealers are conservative buyers. "Works with your DMS, recommended by your DMS provider" is the lowest-friction sale.
4. **OEM alignment.** Many DMS partner programs already include OEM-aligned brands. Being a sanctioned partner positions us inside conversations we would otherwise have to fight to enter.

The Premium tier's first DMS bidirectional integration (Phase 2 of the implementation plan) is the gating commercial milestone for this strategy. Whether IDS or Lightspeed admits us first determines the immediate path; pursuing both concurrently is the right hedging strategy. Multi-DMS support is a Phase 3 / Enterprise Scale tier feature.

---

## 9. The One-Sentence Positioning

> **Your DMS is the system of record for the transaction. RVS is the system of record for the repair — the structured failure data, the technician's outcome, and the cross-dealer pattern your DMS was never built to capture. Different jobs, complementary stacks, no replacement.**

If a dealer is told that and isn't curious enough to want to see a demo, they're not the customer. If they are, the conversation flows naturally from there into intake portal demo, asset history demo, and (for Enterprise prospects) the cross-location benchmarking demo.

---

## 10. Document Status

This document supersedes v1.0 (March 2026). It is intended for sales conversations, partnership discussions, and onboarding material that involves DMS comparisons. It should be reviewed quarterly because the DMS landscape moves — what's true in April 2026 may need updating by Q4 2026 if IDS or Lightspeed ships a structurally surprising feature.

The companion documents `RVS_Context.md` v3.0, `RVS_Competitive_Strategy.md` v3.0, and `RVS_Premium_PRD.md` v1.0 are the authoritative sources for any tension between this document and the broader strategy.

---

*End of RVS_vs_DMS_value_prop.md v2.0.*
