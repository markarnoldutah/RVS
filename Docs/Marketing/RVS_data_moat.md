# RVS Data Moat — Architecture, Operations, and Commercial Strategy

**Version:** 3.0
**Date:** April 30, 2026
**Status:** Authoritative — supersedes v2.0 (earlier same-day) and v1.0

This document supersedes the v2.0 data moat document. v2.0 was written under the Free + Enterprise pricing model and assumed two contributor tiers. v3.0 reflects the four-tier pricing model (Solo / Professional / Premium / Enterprise Scale) plus the OEM Data Licensing track, and adds a comprehensive new §6 on anti-corpus-theft architecture, which v2.0 did not address. The asset ledger architecture, taxonomy enforcement, and anonymization pipeline are unchanged in their fundamentals; what is new is the threat-model awareness that the dataset's commercial value attracts adversarial use, and the architectural and operational protections to defend against it.

---

## 1. The Premise (Restated and Honest)

A data moat in the RVS context means: *a proprietary dataset of structured RV service events that, over time, becomes valuable enough that competitors cannot replicate it by writing similar software.*

Two things must be true for this thesis to hold:

1. **The data must be structured well enough to support analysis.** Free-text complaints/causes/corrections (the DMS pattern) are not a moat — they're text. A moat requires controlled vocabularies, consistent taxonomy, and enforced data quality.
2. **The dataset must reach scale where statistical claims are meaningful.** A few thousand events with sparse coverage cannot support OEM-grade analytics. The path to scale runs through the **Solo, Professional, Premium, and Enterprise Scale tiers**, each contributing structured events at different rates. Solo tier customers contribute the most volume (largest customer base); Premium and Enterprise Scale customers contribute the highest-quality and multi-location data.

These two requirements drive most of the engineering and operational decisions in this document. The thesis fails if either fails.

**Honest acknowledgment:** The data moat is a 24–36 month thesis, not a 6 month thesis. Year 1 will not produce a commercially compelling dataset. Year 2 begins to. Year 3+ is when the thesis is either validated by OEM revenue or invalidated by lack of demand. The strategic discipline is to make the architectural and operational commitments that keep this option live, even when revenue is not yet flowing from the dataset.

---

## 2. The Asset Ledger: Architecture

### 2.1 The Container

Cosmos DB container `assetLedger`, partition key `/assetId`. Document type `assetLedgerEntry`. One entry per service request, written at intake time. Append-only by convention (no API surface for delete or update beyond the structured Section 10A enrichment).

### 2.2 The Asset ID Format

```
{AssetType}:{Identifier}
```

Examples:
- `RV:1HGBH41JXMN109186`
- `Boat:HIN12345678901234`
- `Excavator:CAT320GX98765`
- `Tractor:JD8R3001234`

The format is enforced at write time. Industry expansion (marine, heavy equipment) is architecturally supported but commercially deferred until RV thesis validates.

### 2.3 The Document Schema

```jsonc
{
  "id": "guid",                            // unique entry ID
  "assetId": "RV:1HGBH41JXMN109186",       // partition key
  "serviceRequestId": "guid",
  "tenantId": "org_xxx",                   // for tenant attribution and audit only

  // Asset metadata (anonymizable — kept after tenant deletion)
  "assetType": "RV",
  "manufacturer": "Grand Design",          // controlled vocab
  "model": "Momentum 395G",                // controlled vocab where possible
  "modelYear": 2023,
  "currentMileage": 18452,                 // when known
  "ageInMonthsAtService": 28,              // computed

  // Service event metadata (anonymizable)
  "serviceDateUtc": "2026-04-15T...",
  "issueCategory": "slide-system",         // controlled vocab
  "componentType": "hydraulic-pump",       // controlled vocab
  "failureMode": "seized",                 // controlled vocab
  "repairAction": "replace-pump-assembly", // controlled vocab
  "partsUsed": [                           // controlled vocab
    { "partNumber": "12345-A", "quantity": 1 }
  ],
  "laborHours": 3.5,
  "warrantyClassification": "warranty",    // warranty | customer-pay | esc | internal

  // Geographic context (anonymizable to region)
  "geoRegion": "Mountain West",            // derived from location
  "geoState": "UT",                        // state-level, not city

  // Customer context (anonymizable to retention rules)
  "customerOwnershipMonths": 24,           // how long this customer has owned the asset
  "isFirstOwner": true,

  // Strict-identifying fields (stripped on anonymization)
  "dealerLocationId": "loc_xxx",
  "dealerCorporationName": "Blue Compass RV",
  "technicianId": "user_xxx",
  "advisorNotesSnippet": "string",         // sanitized, length-limited

  // Lineage and quality
  "taxonomyVersion": "10A-2026.04",
  "dataQualityFlags": ["all-fields-present", "ai-categorized-with-override"],
  "createdAtUtc": "2026-04-15T...",
  "updatedAtUtc": "2026-04-15T..."
}
```

### 2.4 The Append-Only Discipline

The ledger is logically append-only. The implementation:

- Initial write happens at SR submission (some Section 10A fields may be null until technician completion)
- Section 10A enrichment writes happen via a single explicit "complete entry" path; no general-purpose update API
- Corrections to ledger entries are themselves new entries with a `correctsLedgerEntryId` reference, not in-place modifications
- The taxonomy version is captured per entry and never silently re-mapped; cross-version analyses use explicit migration tooling

This is unusual for a SaaS data model. It is intentional. The dataset's value to OEMs depends on auditability, immutability, and lineage tracking.

---

## 3. The Section 10A Taxonomy

### 3.1 Why Strict Vocabulary

A free-text repair description like "replaced bad pump" contains the same information as "pump replaced — was failing" but no SQL query can match them. The DMS industry has lived with this for 30 years and it is why no DMS has built failure analytics.

Strict controlled vocabularies make every repair categorizable, queryable, and aggregable. The cost is technician input friction (dropdowns instead of text). The mitigation is good UX (search-as-you-type, recent-values-pinned, AI-suggested classifications).

### 3.2 The Vocabulary Categories

Five controlled vocabularies, each versioned in `lookupSets`:

1. **`issue-category`** — top-level customer-facing category (Slide System, Electrical, Plumbing, HVAC, Generator, Appliance, Roof/Seals, Chassis, etc.)
2. **`component-type`** — the specific component being serviced (hydraulic-pump, slide-motor, water-heater-ignitor, awning-motor, etc.)
3. **`failure-mode`** — how the component failed (seized, leaking, intermittent-failure, no-power, fluid-loss, sensor-fault, etc.)
4. **`repair-action`** — what was done (replace-component, rebuild-component, adjust, lubricate, recalibrate, no-fault-found, etc.)
5. **`part-number`** — when applicable, OEM or aftermarket part numbers from a curated reference list

Each entry has a stable code, display name, optional description, deprecation flag, and version metadata.

### 3.3 Taxonomy Governance

- **Owner:** A designated domain expert (initially the founder, eventually a dedicated product/data role) owns the taxonomy.
- **Versioning:** Major versions ship quarterly. Minor additions (new failure modes for a newly-recognized issue) ship as needed.
- **Backward compatibility:** New versions add codes; never remove or repurpose existing codes. Deprecated codes remain for historical entries but are not selectable in new entries.
- **Migration:** Cross-version analyses use an explicit mapping table that can express "code X in v1.2 became codes Y and Z in v2.0" or "code X is unchanged."

### 3.4 Enforcement at the API Layer

- Section 10A fields submitted with codes not in the active taxonomy version return HTTP 400.
- Free-text override of taxonomy fields is not permitted via API. If a technician encounters a case the taxonomy doesn't cover, the workflow is: (a) submit "other-uncategorized" with a free-text supplement; (b) the supplement is queued for taxonomy review; (c) if accepted, a new taxonomy code is added in the next minor version.
- Free-text description fields (customer issue description, technician notes) are not part of Section 10A and are not constrained — they exist as supplementary context, not analytical primary keys.

### 3.5 AI-Assisted Classification

Technicians can be slow with dropdowns. AI assistance helps:

- At intake, AI suggests `issueCategory` from the customer description (already in Phase 1, all tiers).
- At Section 10A capture, AI suggests `componentType` and `failureMode` from the technician's voice notes and photos, preselecting dropdown values.
- The technician can accept or override. Acceptance rate is tracked as a data quality metric.

AI suggestions are advisory only; the technician's chosen value is what the ledger records. The AI is a UX accelerant, not a source of truth.

---

## 4. The Anonymization Pipeline

### 4.1 Why It Exists

The asset ledger contains tenant-identifying information. Selling that data raw to OEMs is a legal and ethical non-starter. The anonymization pipeline produces a derived dataset (`industryDataset` container) that:

- Strips tenant-identifying fields (corporation name, location ID, technician ID, advisor notes)
- Aggregates geographic data to k-anonymous regions (state-level minimum, region-level for low-volume areas)
- Aggregates temporal data where individual events would be re-identifiable
- Applies differential privacy noise to specific high-sensitivity aggregates
- Maintains lineage to the source ledger for audit purposes (lineage is not exposed in commercial outputs)

### 4.2 Pipeline Architecture

```
assetLedger (raw, tenant-attributed)
    ↓
Cosmos change feed processor
    ↓
Anonymization transformation
    ↓
industryDataset (anonymized, queryable)
    ↓
Benchmarking API (tier-gated query depth)
    ↓
OEM data licensing exports (commercial track)
```

MVP implementation: nightly batch via Azure Function with change feed lease. Long-term: streaming with sub-hourly latency.

### 4.3 Variable K-Anonymity Guarantees (v3 — strengthened from v2)

Every query against `industryDataset` enforces a minimum k-anonymity threshold. If a query would return data that could identify fewer than k tenants, the query returns aggregate or sampled data — or "insufficient data" — rather than a result.

v2 specified a flat k=5 threshold. v3 uses **variable thresholds based on query sensitivity**, recognizing that OEM-relevant queries pose higher re-identification risk:

| Query type | k-anonymity threshold | Rationale |
|---|---|---|
| Generic industry queries (e.g., "industry P50 RECT") | k=5 | Baseline protection |
| Manufacturer-level queries | k=10 | Manufacturer-specific patterns are commercially sensitive |
| Manufacturer-model queries | k=15 | Narrower; harder to anonymize |
| Manufacturer-model-year queries | k=20 | OEM-relevant; protects pilot/contract pricing |
| Component-failure-mode on specific models | k=25 | Highest commercial sensitivity; this is what OEMs would pay for |

This means a Premium customer querying "failure rate of [Manufacturer] [Model] [Year] hydraulic pump assemblies" requires data from at least 25 distinct tenants to return any result. Below the threshold, the query returns "insufficient data" rather than a partial answer.

**The thresholds are non-negotiable and enforced at the query engine layer, not the application layer.** They cannot be bypassed by Premium tier customers, by Enterprise Scale customers, or by RVS staff. They CAN be raised (k=30 for an OEM-licensed dataset, for example) per OEM contract, but never lowered below the table.

This protects:
- **Dealer privacy** — no aggregate query can be used to triangulate a single dealer's data
- **OEM commercial value** — Premium tier benchmarking cannot substitute for an OEM data license, because OEM-relevant queries are deliberately gated to require coverage that only OEM contracts unlock
- **Strategic integrity** — even sophisticated Premium customers attempting systematic scraping cannot extract data of the granularity that makes OEM licensing valuable

### 4.4 What Gets Stripped

Strictly removed from `industryDataset`:

- `tenantId`
- `dealerLocationId`
- `dealerCorporationName`
- `technicianId`
- `advisorNotesSnippet`
- Customer email, phone, name, address
- Specific timestamps (replaced with date-only or week-of-year)
- City-level geography (replaced with state or region)

Retained (anonymizable):

- Asset metadata (manufacturer, model, year, mileage, age)
- Section 10A structured fields (category, component, failure mode, action, parts, labor)
- Warranty classification
- State-level geography
- Customer ownership duration (in months) and first-owner flag

### 4.5 Differential Privacy for Sensitive Aggregates

For aggregates that could be re-identified through repeated querying (e.g., "average labor hours for [very specific failure] at [small geographic region]"), differential privacy noise is added to the response.

This is a Phase 2+ engineering investment. MVP-Enterprise can launch with k-anonymity only; differential privacy is added before OEM data licensing commercializes.

---

## 5. Data Governance and Legal Foundations

### 5.1 The ToS Language Requirements

All RVS customer agreements (Solo, Professional, Premium, Enterprise Scale) must explicitly grant RVS:

1. **License to aggregate the customer's service event data into anonymized industry datasets**
2. **License to commercialize anonymized industry data, including via licensing to third parties (OEMs, parts suppliers, insurance underwriters, ESC providers, etc.)**
3. **License to retain anonymized service event data after termination of the customer's contract**

In addition (new in v3):

4. **Customer warranties**: Customer warrants they are a legitimate RV service business; warrants they will not redistribute, resell, or republish benchmarking data; warrants they will not attempt to reverse-engineer, scrape, or systematically extract data beyond bona fide operational use of the platform.

5. **Liquidated damages clause for benchmarking data misuse**: For material breach of the data-use restrictions (redistribution, scraping, reverse-engineering, sale to third parties, or unauthorized aggregation), customer agrees to liquidated damages in an amount counsel-determined to be reasonable and enforceable. This clause is critical because actual damages from data theft are difficult to quantify; liquidated damages create a deterrent and a clear remedy. Counsel review of the specific damages amount, jurisdiction selection, and arbitration terms is a Phase 0 prerequisite.

6. **RVS termination rights**: RVS reserves the right to immediately terminate any customer contract for breach of the data-use restrictions, with no refund obligation, and to retain all evidence of the breach for potential legal action.
4. **Right to use anonymized data for product development, including AI training**

The grant is to anonymized, aggregated data only. The customer retains ownership of their tenant-attributed data and can export, delete, or take it elsewhere. **The dataset, once anonymized, is RVS's strategic asset and cannot be unwound.**

This is the single most important legal commitment. Get it reviewed by counsel before signing the first design partner. Re-papering customers later is expensive and signals desperation.

### 5.2 Customer Disclosures

Beyond the ToS, customers must be clearly informed that:

- Their service event data feeds an anonymized industry dataset
- They benefit from this through industry benchmarking access (Enterprise tier)
- Their dealer-identifying data is removed from the dataset
- The dataset may be commercialized

Hiding this in fine print is bad practice and a reputational risk. Disclose it prominently. The customers we want are ones who see the value (industry benchmarking, OEM optimization of products they sell) and accept the trade-off knowingly.

### 5.3 No Opt-Out (Documented Position)

Free and Enterprise customers do not have an opt-out from anonymized aggregation. This is documented as a deliberate strategic decision:

- An opt-out makes the dataset thesis fragile (one major customer opting out can damage coverage in a critical category)
- Opt-out customers benefit from the platform without contributing — a free-rider problem
- The anonymization is rigorous enough that no individual customer's data is identifiable in the dataset

If a prospective customer absolutely requires opt-out, they cannot use RVS. This is a feature, not a bug. **Document this position; review with counsel; do not waver under sales pressure.**

### 5.4 Per-Jurisdiction Considerations

- **US:** Default operating jurisdiction. ToS and DPA structured for US legal framework.
- **California (CCPA/CPRA):** Customers in California require additional disclosures and rights (right to know, right to delete). The "data deletion does not delete anonymized ledger entries" position must be defensibly documented.
- **Canada (PIPEDA):** Defer until first Canadian customer. Likely workable but requires legal review.
- **EU (GDPR):** Defer indefinitely; do not pursue EU customers until OEM thesis is validated and we can afford the legal infrastructure.

---

## 6. Anti-Corpus-Theft Architecture (new in v3)

### 6.1 The Threat Model

The dataset's commercial value attracts adversarial use. Specifically, four threat scenarios drive this architecture:

**Threat A — Competitor reconnaissance.** ServiceNomad, a DMS partner, or a future entrant signs up for Solo or trial, calls the benchmarking API, and pulls aggregate industry data they then use to inform their own product strategy or build a competing dataset.

**Threat B — OEM end-runs.** An OEM (or a consultant working for an OEM) signs up to scrape benchmarking data for their own warranty analytics, eliminating any need to license from RVS. This is the worst case — it directly cannibalizes the OEM revenue thesis.

**Threat C — Aggregator competitor.** A third party (J.D. Power, IHS Markit, a startup) signs up multiple Solo accounts under fictitious dealer identities, scrapes benchmarking aggregates across many accounts to triangulate underlying data, and resells to OEMs. The k-anonymity guarantees protect against re-identification of *individual dealer data*, but they do not by themselves protect against systematic scraping of *aggregate insights* for resale.

**Threat D — Casual data leakage.** A legitimate Solo customer, in good faith, posts screenshots of benchmarking data on social media or a trade publication. Aggregated data from RVS now circulates publicly, eroding the value of the licensed dataset.

The architecture in this section addresses each threat with a layered defense.

### 6.2 Layer 1 — Verification Gate on Benchmarking Access

Signup itself is intentionally low-friction (credit card + email verification + slug). Gating signup destroys the GTM motion. Instead, gate **benchmarking access** specifically.

**Verification flow:**

```
Solo / Pro / Premium signup:
  Credit card captured (Stripe)
  Email verification (Auth0)
  Dealer claim form: DOT number, business EIN, dealer license #, 
                     business address
  
  → Account active for intake portal, dashboard, technician app
  → Benchmarking access pending verification
  
Verification gate (Phase 1 manual, Phase 2 partially automated):
  Manual review of dealer claim
  Cross-reference against state dealer license registries (where APIs available)
  Cross-reference against DOT/SAFER for businesses claiming to operate vehicles
  Geographic consistency check (claimed business address vs IP geolocation 
    patterns over first week of usage)
  
  → Benchmarking access granted after verification (24-72 hours typical)
  → No benchmarking access if verification fails or is incomplete
  → Tenant can still use all other platform features during verification wait
```

**Why this works:** Increases the cost of attack from "credit card and 5 minutes" to "establishing a fictitious dealer entity that survives basic verification including state license registry cross-check." A determined attacker can still defeat this layer by establishing a real shell entity, but the cost is non-trivial and creates a paper trail useful for later legal action.

**Operational cost:** Manual verification work for every signup. At Solo scale (target hundreds of customers), this is 30-60 minutes per verification on average — roughly one full-time-equivalent's worth of work at 1,000 customer scale. This is the success engineer's role from Sprint 18+. Until then, the founder owns the verification queue.

**Customer experience cost:** 24-72 hours friction on benchmarking access for legitimate customers. Disclosed at signup. Most customers don't notice because benchmarking isn't their day-1 reason for signing up; they're focused on getting the intake portal live.

### 6.3 Layer 2 — Tiered Query Depth

Even with verified dealers, systematic scraping is possible. Tier-based query depth restricts what each tier can extract:

```
Solo benchmarking access:
  - Pre-built dashboard views only (no custom queries)
  - Single-dimension comparisons ("my RECT vs industry P50")
  - No drill-down beyond category level
  - No date range narrower than 90 days (prevents temporal triangulation)
  - No geographic filtering finer than US-region level
  - No model-year-specific data
  - 20 dashboard refreshes per month

Professional benchmarking access:
  - Custom queries via predefined templates
  - Model-year-specific data
  - State-level geography
  - 30-day date ranges
  - 200 queries/month

Premium benchmarking access:
  - Full custom query API
  - Manufacturer/model-specific data
  - Drill-down to component-failure level
  - 14-day date ranges
  - 2,000 queries/month

OEM tier (sales-led licensing):
  - Full firehose access
  - Custom analytical tooling
  - Negotiated query volumes per contract
```

**The strategic principle:** even a verified Solo "dealer" who's actually a competitor scraper can only extract very high-level industry aggregates that are essentially useless for OEM-grade analytics. The valuable, OEM-relevant queries (manufacturer-specific failure patterns, model-year-specific trends, component-level data) are gated behind paid tiers AND the variable k-anonymity in §4.3, where customer relationships are deeper, verification is stronger, and contractual remedies are real.

The Solo tier's "basic benchmarking" is deliberately too coarse to substitute for paid tier benchmarking, which is itself deliberately too coarse to substitute for an OEM data license.

### 6.4 Layer 3 — Query Rate Limits and Audit Logging

Beyond depth restrictions, every benchmarking query is rate-limited and audit-logged.

**Rate limits (per the table in §6.3):**
- Solo: 20 dashboard refreshes/month
- Professional: 200 queries/month
- Premium: 2,000 queries/month
- Enterprise Scale: negotiated, with quarterly review

**Audit log fields per benchmarking query:**
- `tenantId` — who queried
- `userId` — which user inside the tenant
- `queryParameters` — full parameter set
- `kAnonymityThresholdApplied` — which k threshold was used
- `resultRowCount` — what came back (or whether suppressed)
- `timestamp` — when
- `ipAddress` — where from
- `userAgent` — how
- `correlationId` — for incident reconstruction

The audit log doesn't just enable detection — it creates *evidence* if a customer is later found to be reselling the data. That evidence supports termination, ToS-breach claims, and (in extreme cases) liquidated damages enforcement.

### 6.5 Layer 4 — Anomaly Detection on Query Patterns

Automated pattern analysis runs against the benchmarking audit log. Triggers for human review:

- **Volume spikes** — query rate suddenly 10× the baseline for that tenant
- **Parametric scanning** — sequential queries varying one parameter (year=2020, year=2021, year=2022...) suggest systematic extraction
- **Profile mismatch** — queries about manufacturers/models/regions that don't match the tenant's actual operational profile (e.g., a Utah dealer querying Florida-only data extensively)
- **Re-identification probes** — queries that combine many narrow filters in sequence, suggesting attempts to triangulate specific dealer data
- **Account-cluster patterns** — multiple newly-verified accounts with similar signup patterns, similar IP ranges, similar query patterns (suggests aggregator competitor signing up multiple front entities)
- **Zero-or-near-zero SR submissions but heavy benchmarking usage** — real dealers submit service requests; scrapers don't bother

Flagged accounts surface in admin review queue. Manual investigation determines whether to:
- No action (false positive)
- Issue ToS reminder
- Throttle benchmarking access
- Suspend benchmarking access pending investigation
- Terminate per ToS breach
- Escalate to legal for liquidated damages enforcement

### 6.6 Layer 5 — Variable K-Anonymity (cross-reference to §4.3)

Variable k-anonymity is documented in §4.3. It is the technical guarantee that no benchmarking query, regardless of tier, can extract data of OEM-grade granularity unless it is supportable by 25+ distinct tenants for the most sensitive query types.

This is the hardest defensive layer to defeat because it operates at the data layer, not the application layer. Even if all other layers are bypassed, variable k-anonymity caps the maximum value extractable per query.

### 6.7 Layer 6 — Contractual Teeth (cross-reference to §5.1)

Documented in §5.1. The ToS warranties, liquidated damages clauses, and immediate termination rights create economic and legal cost for misuse. Combined with the audit log evidence trail (§6.4), this transforms the economics of attack:

- Casual scraping: nearly eliminated (verification gate + tiered depth)
- Determined scrapers: still possible but require sustained effort, leave evidence, and risk material liquidated damages
- OEM end-run: mostly prevented (verification gate + tiered depth + variable k-anonymity all align against this)
- Aggregator competitor: requires cultivating multiple verified dealer identities, which is detectable via §6.5 anomaly detection
- Casual data leakage: unaddressed by these layers; addressed by clear ToS language + customer education at onboarding

### 6.8 Layered Defense Summary

| Threat | Verification gate | Tiered depth | Rate limits | Anomaly detection | K-anonymity | Contractual teeth |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| Casual scraping | ✓ | ✓ | ✓ | — | — | — |
| Determined competitor | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| OEM end-run | ✓ | ✓ | — | — | ✓ | ✓ |
| Aggregator competitor | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Casual data leakage | — | — | — | — | — | ✓ |

No single layer is sufficient; the combination is. The architecture is designed so that defeating any one layer still leaves the attacker constrained by the others. Defeating all layers simultaneously is expensive enough that legitimate licensing becomes the cheaper option — which is the strategic goal.

### 6.9 Phasing of Anti-Theft Architecture

**Phase 1 (Solo + Professional ship):**
- Verification gate workflow (manual review queue)
- Audit log infrastructure (event capture even if benchmarking is not yet queryable)
- ToS language with liquidated damages (counsel-reviewed before any design partner signs)
- No benchmarking ships in Phase 1 — all benchmarking deferred to Phase 2 with the anonymization pipeline

**Phase 2 (Premium tier ships):**
- Anonymization pipeline operational
- Tiered query depth implemented
- Variable k-anonymity at the query engine layer
- Rate limits enforced
- Basic anomaly detection (volume spikes, profile mismatch)

**Phase 3+ (OEM pilots):**
- Advanced anomaly detection (re-identification probes, account clustering)
- Differential privacy for highest-sensitivity aggregates (already noted in §4.5)
- SOC 2 Type II attestation reinforces the integrity claim with OEMs
- Liquidated damages clauses tested against potential breach scenarios with counsel before first OEM contract

### 6.10 What This Document Position Means in Practice

The anti-theft architecture is not a separate product feature. It is woven through the platform: every benchmarking endpoint goes through verification + tier check + rate limit + audit log + k-anonymity enforcement. The customer never sees most of these layers; legitimate customers experience the platform as fast and frictionless. Adversarial users hit walls at every layer.

This is the right architecture for a dataset that is the company's strategic asset.

---

## 7. The Coverage Path to OEM Viability

### 7.1 What "OEM-Viable Coverage" Means

For an OEM to license RVS data meaningfully, the dataset must support claims like:

- *"Across 8,000 [Manufacturer] [Model] [Year] units in the dataset, the slide motor failure rate is 3.2% in months 12–24 of ownership."*
- *"Failure mode X is concentrated in units built between weeks Y–Z, with 2.3x higher incidence than the model average."*
- *"Repair action A is 18% faster on average than alternative repair action B for the same failure mode, controlling for unit age and mileage."*

These claims require:

- Sample size (N) per query slice that meets statistical significance thresholds
- Geographic and temporal diversity (so claims aren't artifacts of one region or one season)
- Coverage of the OEM's installed base at a meaningful percentage
- Time-series depth (so seasonal and aging effects can be observed)

### 7.2 The Coverage Math

Reference targets, calibrated to a hypothetical first OEM target (Brinkley, Alliance, or similar mid-tier OEM with ~50K–150K units in service):

| Milestone | Target |
|---|---|
| Phase 1 (Solo + Pro ship, month 6-7) | 1K events, ≥3 OEMs covered, ≥0.5% installed-base coverage of any single OEM |
| Phase 1+ (Solo + Pro ramp, month 12) | 10K events, ≥5 OEMs covered, ≥1% installed-base coverage of any single OEM |
| Phase 2 (Premium tier launch, month 12-15) | 25K events, ≥8 OEMs covered, ≥2% installed-base coverage of target OEM |
| Phase 3 (Enterprise Scale + first OEM pilot, month 18) | 50K events, ≥10 OEMs covered, ≥5% installed-base coverage of target OEM |
| Phase 3+ (OEM production data feed, month 30-36) | 250K+ events, ≥10% coverage of one or more OEMs |

These are aspirational but anchor the discipline. If month-12 numbers are 1K events, the OEM thesis is in trouble and we adjust strategy. If month-12 numbers are 30K events, we're ahead and can pursue OEM conversations earlier.

### 7.3 Coverage Levers

- **Solo tier adoption** — primary volume lever. More dealers using Solo → more events. Volume-driven dataset growth.
- **Professional tier adoption** — quality lever. Multi-location dealer groups contribute structured data across many locations of one corporation, providing geographic and operational diversity within consistent labeling.
- **Premium and Enterprise Scale adoption** — disproportionate contributor. One 50-location Enterprise Scale customer at 100 SR/month/location = 60K events/year from a single contract.
- **Geographic concentration** — Mountain West first means dataset is regionally skewed early; expanding outward improves diversity.
- **OEM-aligned dealer recruitment** — if Brinkley is the first target OEM, weight dealer recruiting toward dealers selling Brinkley.
- **Mobile tech / operator-couple ingestion** — *not pursued in Phase 1*, but architecturally supported. If Solo tier adoption doesn't fill the ledger fast enough, this is the fallback lever.

### 7.4 Quality Levers

Quantity is necessary but not sufficient. Quality levers:

- **Strict taxonomy enforcement** (already specified)
- **Section 10A completion rate** — track and improve the percentage of SRs with all Section 10A fields populated
- **Time from SR completion to ledger enrichment** — short is good; reflects technician engagement with the workflow
- **Taxonomy adherence rate** — what percentage of submissions use existing codes vs. "other-uncategorized"
- **AI-vs-human classification agreement rate** — disagreements indicate either bad AI or sloppy technician inputs; both need investigation

Data quality metrics should be reported monthly to the team alongside revenue and customer counts. **The dataset is the product.**

---

## 8. The Three Properties of the Moat (Restated)

From v1.0 of this doc, the three properties of a strong data moat are:

1. **Accumulates passively** — customers create the data by using the product. ✓ True for RVS.
2. **Compounds over time** — each new event makes the aggregate more valuable. ✓ True for RVS once minimum coverage thresholds are reached.
3. **Cannot be purchased or replicated** — only exists inside the platform that processed the work. ✓ True for RVS, contingent on ToS protections and lack of similar offerings from competitors.

The third property is conditional on competitive landscape. ServiceNomad collects similar data structurally; they have not yet committed to it as a moat. If they do (which is possible), RVS's first-mover advantage in *commitment to the data thesis* matters more than its first-mover advantage in *building the database*. This is why the architectural and operational commitments in this document are urgent — we need to be the credible "data company in RV service" before someone else claims that mantle.

---

## 9. Operational Discipline (Monthly Review)

Recommended monthly internal review of the dataset, separate from the product/revenue review:

```
Asset Ledger Health — Monthly Review
─────────────────────────────────────

Volume
- Total events lifetime:                      [N]
- Events added this month:                    [N]
- Events with full Section 10A:               [N] ([%])
- Distinct VINs covered:                      [N]
- Distinct OEM/model coverage:                [N OEMs, N model-years]

Quality
- Taxonomy adherence rate:                    [%]
- AI-classification override rate:            [%]
- Avg days from SR completion to 10A:         [days]
- "Other-uncategorized" submissions:          [N] ([%])
- Top 5 candidates for new taxonomy codes:    [list]

Coverage
- Top OEM coverage rate:                      [%] of estimated installed base
- Geographic spread:                          [N states, N regions]
- Time-series depth (oldest event):           [date]

Anonymization Pipeline
- Latency (P95 from ledger write to industryDataset write): [hh:mm]
- Pipeline failures this month:               [N]
- K-anonymity threshold violations:           [N] (must be 0)

Strategic Indicators
- New OEMs covered this month:                [N]
- Loss of any tenant (data-attribution risk): [Y/N]
- ToS / DPA changes pending:                  [Y/N]
```

If any quality or anonymization metric trends wrong for two consecutive months, treat it as a P1 issue. The dataset is the strategic asset. Letting it degrade silently is the most serious failure mode.

---

## 10. What This Replaces

This document supersedes:
- `RVS_data_moat.md` v1.0 (conceptual, marketing-flavored)
- `RVS_data_moat.md` v2.0 (specified architecture but assumed Free + Enterprise tiers and a flat k=5 anonymity threshold)

v3.0 changes from v2.0:
- Reflects four-tier pricing model (Solo / Professional / Premium / Enterprise Scale)
- Variable k-anonymity by query sensitivity (k=5 to k=25) replacing flat k=5
- ToS section adds customer warranties, liquidated damages clauses, RVS termination rights
- New §6 specifies the comprehensive anti-corpus-theft architecture (verification gates, tiered query depth, rate limits, anomaly detection, layered defense)
- Renumbered later sections accordingly

---

*End of RVS_data_moat.md v3.0.*
