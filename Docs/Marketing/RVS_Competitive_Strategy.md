# RVS Competitive Strategy

**Version:** 3.4
**Date:** April 30, 2026
**Status:** Authoritative — supersedes v3.3 (earlier same-day), v3.0 (earlier same-day), v2.0 (same-day), and the standalone Kenect, DMS, and ServiceNomad one-pagers.

> **What changed from v3.3 (same-day refinement):** Enterprise Scale tier rationale sharpened. Three additions to the ES feature set make the price gap from Premium ($99/loc at 25-49 loc band) to ES ($150-$300/loc, floor $150) justified by genuine product/service deliverables rather than tier-naming: (1) dedicated solutions engineer (named RVS engineering team member assigned per ES customer, 0.10-0.50 FTE depending on contract size), (2) custom SLA negotiation (higher uptime targets up to 99.99%, service credits, named incident response procedures), (3) documented OEM revenue-share clause (5-10% of OEM contract value when ES customer data represents ≥15% of an OEM-licensed aggregate). Critical support clarified as ES-exclusive (NOT available as a Premium add-on). No pricing changes; no Premium changes.

> **What changed from v3.0 (same-day refinement → v3.3):** Pricing model restructured. Flat-fee + per-location-surcharge model in v3.0 replaced with pure per-location pricing with volume bands. Per-user pricing eliminated entirely (all tiers, all roles unlimited). Enterprise Scale floor lowered to $150/loc at 50 locations. Pro SR cap reduced to 600/loc/mo; Premium SR cap added at 1,000/loc/mo. Implementation fees banded by location count. Pro→Premium and Premium→Enterprise Scale transition discounts standardized at 50% off first 3 months. Tiered support SLAs (Standard/Priority/Critical) introduced as the revenue-capture mechanism for high-engagement customers.

> **What changed from v2 (earlier today):** The Free + Enterprise model from earlier today was restructured into a four-tier model: **Solo / Professional / Premium / Enterprise Scale**, plus the OEM Data Licensing track. The pivot was driven by three considerations: (1) cash runway constraints required revenue from day one, (2) ServiceNomad's audit-required pricing leaves the modal husband-wife operator unserved at the $39/mo price point, and (3) modular feature bundling captures more total revenue than a binary tier model while remaining defensible against competitor scope creep. Anti-corpus-theft protections were added throughout.

---

## 1. Purpose

Single source of truth for positioning language, sales objection handling, roadmap discipline, and pricing tier structure. Any tension between this document and product or marketing materials should resolve in favor of this document.

---

## 2. The Strategic Position in One Sentence

> **RVS is the structured service intelligence platform for the RV service industry — coexistent with the DMS, coexistent with conversational tools, priced to serve operators from solo mobile techs to multi-state dealer groups, and architected to license aggregated failure data to OEMs.**

Three commitments, baked in:

1. **Service intelligence**, not service operations. We are not an operating system. We do not run the shop.
2. **The dataset is the strategic asset.** Every customer segment contributes; every product decision is checked against whether it strengthens the dataset.
3. **OEM data licensing is the long-term commercial vehicle.** Multi-tier SaaS is the runway-protecting cash engine while the dataset matures.

---

## 3. The Competitive Map

```
┌──────────────────────────────────────────────────────────────────┐
│  CUSTOMER-FACING LAYER                                           │
│                                                                  │
│   Kenect, Podium       → conversations, reviews, payments        │
│   ServiceNomad         → operating system, voice AI, scheduling  │
│   QuoteIQ, RV Service  → SMB CRM and invoicing tools             │
│   Suite                                                          │
│   RVS                  → structured intake + service intelligence│
│                          + cross-location coordination           │
│                                                                  │
│   ════════════ feeds structured data downward ═══════════        │
│                                                                  │
│   IDS Astra, Lightspeed → DMS: accounting, parts, RO, warranty   │
│                                                                  │
│  BACK OFFICE                                                     │
└──────────────────────────────────────────────────────────────────┘
```

Three layers, distinct jobs.

---

## 4. Competitor-by-Competitor Position

### 4.1 Kenect (and similar: Podium, Text Request)

Customer-conversation platform. Texting, reviews, payments, voice AI receptionist (post-Auto Labs acquisition, March 2025). 10,000+ dealerships across multiple verticals.

**Threat level:** Low-to-medium. Peer in the "in front of DMS" layer.

**Position:** Coexist via webhook. They handle conversations, we handle structured data and technician workflow.

**The line:** *"Kenect handles the conversation. RVS captures what the technician needs to fix it — and turns every repair into a data point your DMS doesn't have."*

### 4.2 IDS Astra G2 and Lightspeed DMS

30–40-year incumbent DMS systems. ~1,200 (IDS) and ~4,500 (Lightspeed) dealer customers. Lightspeed publishes pricing at $450–$3,000+/month for the entire DMS — this is the price anchor that bounds RVS pricing for any customer paying for both.

**Threat level:** Medium.

**Position:** Coexist. We sit in front. We feed them. We do not replace them.

**Pricing implication:** RVS Premium and Enterprise Scale must be priced sensibly relative to Lightspeed DMS. A 25-location group paying ~$2,500/mo for Lightspeed cannot rationally also pay $5K/mo for RVS as an "intelligence layer above the DMS." Premium pricing reflects this — at 25 locations, RVS Premium is ~$3K/mo, comparable to (not 2× more than) the underlying DMS.

### 4.3 ServiceNomad

RV-specific service operating system. Founded by Rich Mahre (owner of Boss Bull Mobile RV Services, Austin, TX). Six-layer platform: communication, scheduling, voice AI, estimates/ESC/approvals, job progression, invoicing/payments. ~32 paying customers as of March 2026. **Pricing is "Book an Operating System Audit"** — consultative-sales, not self-serve. They are NOT competing for the modal $39/mo operator.

**Threat level:** Medium-to-high in the upper-SMB segment. Lower than v2 assessment in the modal-operator segment, where ServiceNomad's pricing model excludes them entirely.

**Position:** Coexist via integration; differentiate via approach. ServiceNomad runs the shop end-to-end. RVS is the intelligence layer with multi-tenant aggregation thesis ServiceNomad has not committed to.

**Pricing implication:** RVS Solo at $39/loc/mo serves customers ServiceNomad cannot economically reach. RVS Professional at $79/loc (decreasing to $59/loc at 25-49 loc) serves multi-location operators. ServiceNomad's pricing remains opaque, so head-to-head pricing comparisons aren't possible — but on observed deal patterns (audit-required + sales-led), they're targeting customers who would pay $500–$2,000/mo. RVS Professional sits below that band at small sizes (5-loc Pro is $395/mo) and within it at larger sizes (25-loc Pro is $1,475/mo). RVS Premium sits within the upper portion of that band (5-loc Premium is $595/mo; 15-loc Premium is $1,635/mo).

### 4.4 QuoteIQ, RV Service Suite (SMB CRM tools)

CRM and invoicing tools for the SMB / mobile-tech segment. Published pricing $29.99–$98.99/mo at QuoteIQ; lifetime license + monthly options at RV Service Suite. This is the price band where the modal husband-wife operator actually buys.

**Threat level:** Medium for Solo tier specifically.

**Position:** Different category. Their product is CRM + estimating + invoicing. Our Solo product is structured intake + technician workflow + dataset contribution + basic benchmarking. Customer can use both in parallel.

**The line:** *"QuoteIQ is your CRM and invoice tool. RVS is your structured intake and dataset benchmarking. They're not competing — they're complementary. Some of our Solo customers use QuoteIQ for billing and RVS for everything else."*

This is intentionally an "and" not "or" position. The Solo tier doesn't try to win the SMB CRM/invoicing battle — it stakes out a different value proposition (structured data, technician workflow, anonymized industry benchmarking).

---

## 5. The Customer Segments and Tier Mapping

| Segment | Profile | Owned By | RVS Tier |
|---|---|---|---|
| Solo / mobile / operator-couple | 1 location, 1–3 techs, often husband-wife | QuoteIQ, RV Service Suite | **Solo** ($39/loc/mo) |
| Single-shop independent | 1 location, 5–15 techs | ServiceNomad upper-SMB / QuoteIQ | **Solo** or **Professional** if they want benchmarking depth |
| Small dealer group | 2–4 locations, 1 corp | Contested | **Solo** at all locations OR **Professional** |
| Mid-market dealer group | 5–15 locations | Underaddressed by current competitors | **Professional** (entry) or **Premium** (with IT requirements) |
| Multi-state dealer group | 16–49 locations | DMS-first; no real coordination layer | **Premium** (most common) |
| Large dealer group | 50+ locations | Camping World, Blue Compass scale | **Enterprise Scale** (sales-led) |
| OEMs | Brinkley, Alliance, Grand Design, Forest River, Thor | Currently buy J.D. Power, internal | **OEM Data Licensing** (long-term track) |

**Every customer segment from solo operator through 100-location dealer group has a defined tier.** No segment is left unserved. No segment is offered a price they can't justify.

---

## 6. The Four-Tier Pricing Model

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

**Per-location pricing with volume bands.** Pro and Premium have three bands (1–9 / 10–24 / 25–49 locations). Solo is flat at $39 across all sizes (Solo customers above 9 locations are rare; they upgrade to Pro). Enterprise Scale is custom-contract at 50+ locations with a $150/loc floor and ceiling around $300/loc, negotiated per deal.

**No per-user pricing at any tier.** All users — technicians, advisors, managers, owners, regional managers, corporate admins — are unlimited at every tier. This protects the dataset thesis (technicians are the data-capture users; gating them would damage Section 10A coverage) and simplifies the pricing model for procurement teams. Revenue from larger customers is captured through volume-banded per-location pricing and tiered support SLAs.

**Annual prepay:** 15% off across all tiers (subscription only; implementation fees full price).

**Implementation fees** (one-time, banded by location count):

| Tier | Locations | Implementation fee |
|---|---|---|
| Solo / Professional | All | None (self-serve) |
| Premium | 1–9 | $5,000 |
| Premium | 10–24 | $7,500 |
| Premium | 25–49 | $10,000 |
| Enterprise Scale | 50–99 | $15,000 |
| Enterprise Scale | 100–249 | $25,000 |
| Enterprise Scale | 250+ | $40,000 |

Implementation add-ons (additional DMS integration beyond first, custom data migration, custom analytics dashboard config) priced separately and scoped at signing. Add-ons added during implementation invoice at published rate plus 25% expedite premium (sales lever to discourage scope creep).

**Support tiers** (Phase 2 ship, alongside success engineer hire):

| Support tier | Monthly cost | Response SLA | Channels | Included with |
|---|---|---|---|---|
| Standard | Included | 1 business day | Email, portal | Solo, Pro, Premium |
| Priority | +$500/mo | 4 hours, business hours | Email, portal, phone | Optional add-on at Pro and Premium |
| Critical | +$1,500/mo | 1 hour, business hours | All + dedicated CSM channel | Bundled with Enterprise Scale |

Support tier upgrades are the primary revenue-capture mechanism for high-engagement customers (replacing per-user pricing). A modal Premium customer pays Standard support; a heavy-staff Premium customer with high operational urgency typically upgrades to Priority. Most Pro customers won't need an upgrade.

**Transition discounts:** 50% off first 3 months for customers upgrading Pro→Premium or Premium→Enterprise Scale. Sales lever, not published. Applies to subscription only, not implementation fees.

### Why this structure

**1. Solo at $39/loc/mo (flat) captures runway-critical revenue from day one.** The modal husband-wife operator can pay $39. They can grow to 4 locations on Solo for $156/mo. The price is calibrated to where the SMB market actually buys — RVFix's $39.99/mo membership is the explicit market reference point.

**2. Professional at $79/loc (decreasing to $59/loc at 25-49 loc) serves the 5–15 location operator who needs cross-location coordination but doesn't need IT-grade compliance.** A 10-location Pro customer pays $690/mo (10 × $69) — comparable to mid-tier Lightspeed DMS, defensible as the intelligence layer above the operational DMS. The volume bands give larger Pro customers the per-location discount procurement teams expect.

**3. Premium at $119/loc (decreasing to $99/loc at 25-49 loc) adds the IT/compliance bundle.** SAML, SCIM, audit log, IP allowlisting, bidirectional DMS integration, 99.9% SLA, dedicated success manager. Premium is the level where procurement is involved. A 10-loc Premium customer pays $1,090/mo — clear step up from Pro that reflects the feature gap, not a layered fixed-fee math problem. The Pro→Premium step is roughly 1.5× per location, which matches the value gap (multi-location coordination → enterprise IT/compliance).

**4. Enterprise Scale at 50+ locations is sales-led and custom.** Floor $150/loc means a 50-location customer pays at least $7,500/mo. Pricing scales to $300/loc at the largest sizes. Enterprise Scale is a different feature tier — multi-DMS support simultaneously, custom analytics builder, custom data exports, **dedicated solutions engineer** (named RVS engineering team member, 0.10–0.50 FTE per customer), **custom SLA terms** (uptime targets up to 99.99%, service credits, named incident response), bundled Critical support (1-hour response, dedicated CSM, 24/7 P1 — exclusive to ES, not available as a Premium add-on), and a **documented OEM revenue-share clause** (5–10% of OEM contract value when ES customer data represents ≥15% of an OEM-licensed aggregate). The price gap from Premium to ES is justified by ~$50K–$150K of dedicated human capital per customer plus features that don't exist at Premium, not by tier-naming.

**5. Volume bands ARE the volume discount.** Larger customers in Pro and Premium benefit from lower per-location rates within their band — the procurement-expected reward for scale, transparent in published pricing rather than negotiated per deal.

**6. OEM Data Licensing is a separate commercial track** ($50K–$3M/year per `RVS_OEM_GoToMarket.md`). Not customer-tier pricing.

### Why no per-user pricing

The case for per-user pricing on Premium and Enterprise Scale is real (procurement-expected, captures cost-to-serve from larger orgs, expansion mechanic). The case against is stronger:

1. **Simplicity wins customer trust.** "$119 per location, all-inclusive" is a sentence anyone can repeat. The per-user math creates billing complexity, predictability problems for the customer, and customer service tickets.

2. **The dataset thesis demands unlimited technicians.** Per-tech pricing would be perverse (it disincentivizes Section 10A capture). And applying per-user differentially (techs unlimited, others paid) creates explanation overhead at every sales conversation.

3. **Engineering cost is real.** Active-user metering, role-based exclusion, mid-period proration — sprint of work plus ongoing edge-case maintenance. Eliminated in v3.3.

4. **The revenue captured is recoverable through other mechanisms.** Volume-banded per-location pricing already gives larger customers a per-location discount that recovers some of the per-user revenue at the high end. The Priority/Critical support tier upgrades capture revenue specifically from high-engagement organizations (which is the same customer profile that would have paid the most per-user fees).

The principled story: **"We charge per location because that reflects what you're operating. Users are free. We want every staff member who touches a service request — owner, advisor, manager, technician — to be on the platform. That's how the data stays clean and the operation works."**

### Why no published tier between Solo and Professional

A "Solo+" tier at $99/mo with limited multi-location features would dilute Professional's value and create three problems: (a) it's where ServiceNomad pricing pressure is most likely to land, (b) it weakens the Professional sales conversation by giving sophisticated operators a cheaper "adequate" alternative, and (c) it splits engineering attention.

The discipline: Solo is sufficient up to 4 locations; at 5+ locations you graduate to Professional. The 4→5 location transition is the cliff, not a sliding scale.

### Why no opt-out from data aggregation

Any customer using RVS contributes to the anonymized industry dataset. This is documented in `RVS_data_moat.md` and is non-negotiable. Customers who require true opt-out cannot use RVS. The dataset thesis is fragile if opt-out is permitted; the opt-out itself becomes a weapon competitors use against the strategy.

The customer's compensation: **basic industry benchmarking is included at every tier, including Solo.** Customers see they benefit from the dataset they contribute to. Advanced benchmarking (custom queries, drill-down) is gated behind paid features; corpus theft protections (`RVS_data_moat.md` §6) prevent abuse.

---

## 7. Roadmap Discipline: The Yes/No Filter (v3)

Every proposed feature passes this filter. If the answer to all four is "no," the feature does not ship.

1. **Does this strengthen the dataset?** (Better taxonomy, more coverage, higher data quality, better anonymization.)
2. **Does this serve a paid-tier feature bundle?** (Operator-level retention features for Solo, multi-location coordination for Professional, IT/compliance for Premium, custom analytics for Enterprise Scale.)
3. **Does this reduce a customer's cost of switching to RVS or staying on RVS?**
4. **Is this a Solo-tier-retention feature that protects against ServiceNomad/QuoteIQ?** Constraint: must be table-stakes, must not require multi-quarter engineering, must not contaminate the structured dataset.

Examples of features that fail the filter:

- Voice AI / inbound call handling — fails all four. ServiceNomad territory.
- Full invoicing system that becomes the customer's source of truth — fails 1 (contaminates dataset attribution), passes 4 only superficially.
- In-dashboard SMS composition — fails all four. Kenect territory.
- Customer iOS/Android native app — fails 1, 2; arguable on 3, 4.
- Marine / heavy equipment / agricultural verticals — fails 2 in the near term; deferred until OEM thesis validates.
- Service appointment scheduling — fails 1; passes 4 weakly. Defer until Solo retention shows it's needed.

Examples of features that pass:

- Cross-location asset history — passes 1 (links data across the ledger), 2 (Professional bundle).
- Section 10A controlled vocabularies — passes 1 (foundational for OEM data quality).
- Real bidirectional DMS integration — passes 2 (Premium bundle), 3 (switching cost).
- Anonymization architecture — passes 1 (precondition for OEM thesis).
- SAML SSO and SCIM — passes 2 (Premium bundle), 3 (procurement gate).
- Industry benchmarking dashboard — passes 1, 2.
- Basic invoice generator that doesn't claim to be the source of truth — passes 4 (Solo retention against QuoteIQ); requires careful scoping to avoid scope creep into actual invoicing.

The discipline that has to hold with the four-tier model: **paid-tier retention features** (filter 4 expanded) cannot drift into being a full second product. Anything that takes more than two sprints to ship and isn't already in the Yes/No filter on filters 1–3 grounds requires explicit strategic review.

---

## 8. The Geographic Beachhead Decision

**Primary beachhead:** Utah, Idaho, Colorado, Arizona, Nevada.

Reasoning unchanged from v2:

1. **Lightspeed is in Salt Lake City.** Local relationships and visibility advantage.
2. **ServiceNomad is in Austin and is concentrated in Texas.** Their density is lowest in the Mountain West.
3. **Operator density is real.** Mountain West has high RV ownership and a healthy mobile/dealer service ecosystem.
4. **Multi-location dealer groups with HQs in or near the region:** Bish's RV (Idaho HQ) and others.
5. **Travel and conference cost is lower** than national pursuit at this stage.

**Secondary beachhead (months 6–12):** Pacific Northwest (Oregon, Washington), then Florida and Texas. Texas only after the dataset gives us a credible story and Mountain West customers serve as references.

---

## 9. Sales Objection Handling — Updated

| Dealer says... | We say... |
|---|---|
| *"Doesn't my DMS already do this?"* | "Your DMS captures the repair order. RVS captures the diagnosis — what failed, how, and how often across thousands of similar units. Different question, different data. The DMS keeps doing what it does well; we add the layer it was never built for." |
| *"Lightspeed has a Service Scheduler that reduces RECT."* | "Scheduling reduces RECT after the unit arrives. We reduce RECT before it arrives — by giving the tech the symptoms, photos, and category before the bay door opens. Different lever." |
| *"Doesn't ServiceNomad already do this?"* | "ServiceNomad runs a single shop end-to-end. RVS is the intelligence layer above one or many shops — structured failure data, cross-location analytics if you grow into Professional, OEM-grade benchmarking. If you're a single shop and want a full operating system, ServiceNomad may be right for you. If you want structured data and the option to grow into multi-location coordination, that's us." |
| *"Doesn't Kenect already do this?"* | "Kenect texts your customer. We tell your technician what's actually broken before the rig rolls in. We send our notifications through their thread when you have Kenect — different layer, no overlap." |
| *"How is RVS different from a $30 CRM tool?"* | "QuoteIQ and RV Service Suite are CRM and invoicing tools. They're great for running your customer database. RVS is a different product — structured intake, technician workflow, structured failure data, industry benchmarking. Some of our customers use both. We're $39/location, comparable to a CRM tool, doing complementary things." |
| *"Why would I pay $595/mo for Premium?"* | "Premium at 5 locations is $595/mo. It's for the dealer group with IT involvement, SAML SSO requirements, audit log compliance, real DMS integration, warranty leakage analytics, and 99.9% SLA. If you don't have a CIO and don't need SAML, Professional at $395/mo for 5 locations is probably the right fit." |
| *"Will you integrate with my DMS?"* | "Premium and Enterprise Scale: real bidirectional API integration with IDS or Lightspeed. Solo and Professional: daily CSV download. We're pursuing Technology Partner status with both major DMS providers." |
| *"What about my 4-location operation?"* | "Solo at all four locations: $156/mo. You get the intake portal, technician app, dashboard, basic benchmarking, asset ledger contribution. If you want cross-location coordination, that's Professional at $316/mo (4 × $79). Either fits your operation." |
| *"What happens when I add my 5th location?"* | "Solo at 5 locations is $195/mo. Professional at 5 locations is $395/mo (5 × $79). The decision at the 5th location is whether you want cross-location coordination — most operators with 5+ locations want it. We have a 50% off first-3-months Pro upgrade discount for Solo customers transitioning, which lands the first 3 months at ~$198/mo." |
| *"What about volume discounts?"* | "They're built into the published pricing. Pro is $79/loc for 1-9 locations, $69/loc for 10-24, $59/loc for 25-49. Premium is $119/$109/$99 across the same bands. Larger operators automatically pay less per location — no negotiation required." |
| *"Why don't you charge per user?"* | "We charge per location because that reflects what you're operating. Users — technicians, advisors, managers, owners — are unlimited at every tier. We want every staff member who touches a service request on the platform. That's how the data stays clean and the operation works. Procurement teams sometimes find this unusual, but our pricing is honest about what we're charging for." |
| *"Are you trying to replace my DMS?"* | "No, ever. We coexist with IDS, Lightspeed, EverLogic, Motility. Replacing the DMS is a 10-year project we don't want. We sit in front and feed structured data into your existing system." |
| *"Why does my data feed an industry dataset?"* | "Your dealer-identifying information is anonymized. The aggregate industry dataset benefits you — basic benchmarking ('how does my RECT compare to industry?') is included at every tier. The dataset is the strategic asset that lets us offer this benchmarking to you for free at Solo and lets us license aggregated insights to OEMs over time. You contribute, you benefit, your identity is protected. This is documented in our ToS and reviewed by counsel." |
| *"Why does benchmarking take 1-2 days to activate?"* | "We verify every dealer credential before unlocking benchmarking access. It protects the dataset's value and makes sure aggregate insights aren't being scraped by competitors or aggregators. Real dealers pass verification quickly. The verification is also evidence-of-legitimacy that makes the data more valuable when we license it to OEMs." |
| *"What about faster support?"* | "Standard support is included at every tier — 1 business day response, email and portal. Pro and Premium customers can add Priority support for $500/mo, which gets you 4-hour response and phone. Enterprise Scale includes Critical support — 1-hour response with a dedicated CSM channel. Most customers don't need an upgrade; the dealer groups with high operational urgency add it as needed." |
| *"Where are your customers?"* | Honest answer in months 1–6: "Design partners in Utah, Idaho, and Colorado. We're deliberately starting in the Mountain West before expanding nationally." |

---

## 10. The Forbidden Phrases

These claims are no longer defensible. Remove from all collateral immediately:

- ❌ "No competitor has tackled service intake at this level."
- ❌ "RV service software is outdated — opportunity for modern workflow tools."
- ❌ "The RV Service Operating System."
- ❌ "Replacement for your DMS / your CRM / your messaging tool."
- ❌ "First mover" anything in RV service AI.
- ❌ "Free forever" (we no longer have a free tier; trials require credit card).
- ❌ "Single low price for everything" (we have four tiers, deliberately).

---

## 11. The Strategic Risk Map

| Risk | Likelihood | Severity | Mitigation |
|---|---|---|---|
| ServiceNomad ships multi-location enterprise features | Medium | High | Ship Professional and Premium faster than they pivot; build OEM relationships they can't shortcut |
| IDS or Lightspeed ships a "service intake module" | Medium | High | Become Technology Partner first; make integration with us part of *their* roadmap |
| Kenect / Podium adds structured intake | Low-medium | Medium | Webhook integration ready; we coexist not compete |
| Solo tier doesn't generate enough revenue to bridge to Premium-tier profitability | Medium-High | Critical | Aggressive Solo marketing; Solo→Pro upgrade conversion focus; per-user Premium pricing for ARR depth |
| OEM thesis doesn't validate (no OEM willing to pilot) | Medium | Critical | Adjacent monetization paths in `RVS_OEM_GoToMarket.md` §7; multi-tier SaaS revenue is the cushion |
| Multi-location buyers prefer to build internally | Low | High | Sales emphasis on time-to-value; reference customers; published case studies once we have them |
| Founder bandwidth for solo-dev work breaks under multi-tier engineering load | High | High | Phase 1 elongation accepted (~25–27 sprints); success engineer hire in Sprint 18+ |
| Corpus theft via Solo signup or trial accounts | Medium | High | Verification gate, tiered query depth, variable k-anonymity, audit logs, ToS liquidated damages — see `RVS_data_moat.md` §6 |
| ServiceNomad price cuts to compete with Solo | Low | Medium | We're not in their pricing band; their consultative-sale model is hard to discount without restructuring |
| Phase 1 elongation pushes first revenue past month 7 | Medium | Critical | Phase 1.5 phased Professional rollout; runway management |

---

## 12. The North Star Metric

**The dataset, not the ARR.**

Specifically: *coverage and quality of Section 10A structured service events in the asset ledger, weighted by recency and completeness.*

The four-tier pricing model is a means, not the end. ARR funds the dataset's growth. The dataset funds the company's strategic future. If after 18 months ARR is $2M but the ledger has 5,000 events, the OEM thesis is dead. If ARR is $500K but the ledger has 80,000 high-quality events with 10% coverage of any single major OEM, the OEM thesis is live and the company is fundamentally more valuable than a $2M-ARR SaaS.

**Internal metric to report monthly alongside ARR:**

```
Asset Ledger Health Metrics
─────────────────────────────────
Total service events:           [N]
Events with full Section 10A:   [N] ([%])
Distinct VINs covered:          [N]
Distinct OEM/model coverage:    [N] OEMs, [N] model-years
Top OEM coverage rate:          [%] of estimated installed base
Geographic spread (states):     [N]
Avg days between event and 10A: [days]
Taxonomy adherence rate:        [%]
Tier distribution of contributors: [Solo: N, Pro: N, Premium: N, Enterprise: N]
```

Tier distribution becomes a signal: a healthy contributor base spans tiers. If the dataset is 95% Solo-contributed, the strategic value is lower than if it's diversified across tiers (because Premium and Enterprise customers contribute higher-quality data, multi-location data, and have signed comprehensive data-sharing terms).

---

## 13. What This Document Replaces

This document supersedes:

- `RVS_Competitive_Strategy.md` v1.0 and v2.0 (April 30, 2026, earlier same-day)
- The "Free + Enterprise" pricing structure documented in v2
- The implicit "$5K wall" Enterprise pricing
- Any earlier mid-tier discussions

The standalone competitive one-pagers (Kenect, DMS, ServiceNomad) remain superseded.

---

*End of RVS_Competitive_Strategy.md v3.0.*
