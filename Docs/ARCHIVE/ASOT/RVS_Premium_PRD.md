# PRD: RV Service Flow (RVS) — Premium + Enterprise Scale Tiers

**Version:** 1.2
**Date:** April 30, 2026
**Status:** Draft — design phase, ships in Phase 2 after Phase 1 (Solo + Professional) reaches ship criteria
**Supersedes:** v1.1 (earlier same-day, v3.3 pricing alignment), v1.0

> **What changed in v1.2 (v3.4 — same-day refinement):** Enterprise Scale tier sharpened with three feature/contract additions to justify the per-location rate above Premium: (1) FR-ES-007 OEM data partnership rewritten with documented revenue-share clause (5–10% of OEM contract value when ES customer data represents ≥15% of an OEM-licensed aggregate); (2) FR-ES-011 added — dedicated solutions engineer (named RVS engineering team member, ~0.10–0.50 FTE per ES customer depending on contract size); (3) FR-ES-012 added — custom SLA negotiation (higher uptime targets, service credits, custom incident response procedures, named maintenance windows). The §2.2 ES tier description was rewritten to incorporate these. No pricing changes.

> **What changed in v1.1 (v3.3 — same-day refinement):** Pricing model updated to v3.3 — pure per-location with volume bands, no per-user pricing at any tier (FR-PR-013 added; FR-PR-012 reworded). Premium per-loc bands: $119/$109/$99 by 1-9/10-24/25-49 loc. Enterprise Scale per-loc range: $150–$300, floor $150 at 50 loc. Premium SR cap added at 1,000/loc/mo (FR-PR-018, was unlimited). Implementation fees banded by location count (FR-PR-014, FR-ES-008). Premium gets optional Priority support add-on (FR-PR-016); Enterprise Scale bundles Critical support (FR-ES-004). Pro→Premium and Premium→Enterprise Scale transition discounts standardized at 50% off first 3 months. ACV reference tables in §7 fully recalculated.

> **Note:** This PRD supersedes `RVS_Enterprise_PRD.md` v1.0 from earlier today. The pivot to a four-tier model (Solo / Professional / Premium / Enterprise Scale) split the prior "Enterprise" requirements: multi-location coordination and analytics moved to the Professional tier (`RVS_PRD.md` v3.0); IT/compliance features and DMS integration remain here in Premium; sales-led custom-contract features remain here in Enterprise Scale. Many FRs from the prior `RVS_Enterprise_PRD.md` are carried forward unchanged but renumbered with the `FR-PR-` (Premium) prefix or remain Enterprise Scale-specific.

---

## 1. Document Purpose

This PRD specifies the **Premium and Enterprise Scale tiers** of RVS. These ship in **Phase 2** (months 7–12) after Phase 1 (Solo + Professional) has reached ship criteria.

For strategic context: [`RVS_Context.md`](RVS_Context.md) and [`RVS_Competitive_Strategy.md`](RVS_Competitive_Strategy.md). For Solo and Professional tier requirements: [`RVS_PRD.md`](RVS_PRD.md). For OEM go-to-market: [`RVS_OEM_GoToMarket.md`](RVS_OEM_GoToMarket.md).

---

## 2. Product Definition

### 2.1 Premium Tier in One Paragraph

RVS Premium is the IT-grade compliance and DMS-integration tier of RVS, priced at **$119/loc/mo for 1–9 locations, decreasing to $109/loc at 10–24 locations and $99/loc at 25–49 locations** (per the v3.3 banded pricing model). Premium customers are dealer groups with IT involvement, audit and SOC 2 compliance requirements, and the budget for real bidirectional DMS integration. The tier delivers SAML SSO, SCIM provisioning, IP allowlisting, comprehensive audit log, bidirectional integration with one DMS partner (IDS or Lightspeed), 99.9% SLA, dedicated success manager, and quarterly business review. Optional Priority support add-on at $500/mo for customers needing 4-hour response. **No per-user pricing**; all users (technicians and non-technicians) are unlimited at every tier. Implementation fee one-time, banded by location count ($5K/$7.5K/$10K).

### 2.2 Enterprise Scale Tier in One Paragraph

RVS Enterprise Scale is the sales-led, custom-contract tier for dealer groups operating 50+ locations. Pricing is **$150–$300/loc/mo (negotiated; floor $150 at 50 locations)** with $15K–$40K banded one-time implementation fee. Enterprise Scale adds multiple-DMS support, custom analytics and report builder, custom data exports, **Critical support tier bundled** (1-hour response, dedicated CSM channel, 24/7 on-call for P1 incidents — exclusive to Enterprise Scale, not available as a Premium add-on), **dedicated solutions engineer** (FR-ES-011 — named individual on the RVS engineering team responsible for DMS integration, custom analytics, and technical escalation), **custom SLA negotiation** (FR-ES-012 — higher uptime targets, service credits, custom incident response, named maintenance windows), quarterly executive business review, co-marketing opportunities, and **OEM data partnership coordination with documented revenue-share clause** (FR-ES-007 — 5–10% of OEM contract value when customer's data represents ≥15% of an OEM-licensed aggregate). **No per-user pricing**; consistent with all other tiers in the v3.3+ model.

### 2.3 The Ideal Customer Profile (ICP)

**Premium:**
- Mid-market dealer groups (5–49 locations) — most common Premium customer
- Single-location strategic accounts with IT/compliance requirements (rare, sales-led custom)
- Decision cycle 2–6 months
- Buyer is typically VP of Service, COO, or CIO
- Budget: $20K–$50K ACV at typical sizes

**Enterprise Scale:**
- Large dealer groups (50+ locations) — Camping World class, Blue Compass class
- Decision cycle 9–18 months
- Buyer is VP of Service, CIO, or CFO
- Budget: $100K–$300K+ ACV
- Higher contract value, longer sales cycle, biggest data contribution

### 2.4 Out of Scope (Explicit)

Premium and Enterprise Scale do NOT include:

- Service appointment scheduling, bay assignment, technician routing — DMS or ServiceNomad territory
- Invoicing, payments, ESC approval — DMS territory
- Voice AI / inbound call handling — ServiceNomad territory
- Two-way SMS, broadcast, marketing — Kenect / ServiceNomad territory
- Parts inventory or ordering — DMS territory
- HR / payroll integration
- Marine, heavy equipment, agricultural verticals — deferred until OEM thesis validated

The Yes/No filter from `RVS_Competitive_Strategy.md` §7 governs all scope decisions.

---

## 3. Goals

### 3.1 Strategic Goals

- Land **3–5 paying Premium customers** within 6 months of Phase 2 ship
- Land **first paying Enterprise Scale customer** within 9 months of Phase 2 ship
- Reach **month 24 ARR of $500K–$1M** (combined Solo + Pro + Premium + Enterprise Scale)
- Build a **reference customer set of 2–3 marquee Premium / Enterprise Scale customers**
- Demonstrate **multi-location ROI** quantitatively (RECT improvement, warranty leakage recovery, technician productivity benchmarks)
- Generate **dataset growth at 5–10× the rate of Solo + Pro alone** — Premium and Enterprise Scale customers drive disproportionate ledger volume

### 3.2 Customer Goals

- **VP of Service:** single-pane-of-glass operational visibility across all locations with drill-down to individual SR
- **Regional Manager:** their region's metrics, alerts, queue without seeing irrelevant locations (provided in Pro)
- **CFO / Finance:** warranty leakage analytics (Pro), cross-location cost-per-repair benchmarking (Pro), clean SOC 2-grade audit trail (Premium)
- **CIO / IT:** SAML SSO, SCIM provisioning, IP allowlisting, real DMS integration (Premium)
- **Owner:** industry benchmarking and the strategic optionality of OEM data partnerships

---

## 4. Functional Requirements (Premium Tier)

Premium inherits all Solo + Professional FRs and adds the following.

### 4.1 Identity, Compliance, and Audit

**FR-PR-001: SAML SSO via Auth0 Organizations**
Premium tenants are provisioned as Auth0 Organizations. Each org configures its own SAML connection (Okta, Entra ID, Google Workspace, OneLogin, etc.). The `tenantId` claim is sourced from the Auth0 Organization ID. ClaimsService is unchanged.

**FR-PR-002: SCIM 2.0 provisioning**
Premium tenants can configure their identity provider to push user provisioning, deprovisioning, and role updates to Auth0 via SCIM. RVS does not implement SCIM directly; relies on Auth0's SCIM support.

Group-to-role mapping from IdP groups to RVS roles (`dealer:owner`, `dealer:regional-manager`, etc.) configured at the Auth0 Organization level.

**FR-PR-003: Comprehensive audit log**
A new Cosmos container `auditLog` (partition key `/tenantId`) records all data access and mutation events:

- Authentication events (login, logout, MFA challenge, failed attempts)
- Authorization events (permission denied)
- Data access (SR view, asset ledger query, attachment access, analytics query, benchmarking query)
- Data mutation (SR create, status change, Section 10A update, user CRUD, settings change)
- Administrative events (user role change, integration configuration, location creation/deletion)

Each entry includes: `tenantId`, `userId`, `eventType`, `resourceType`, `resourceId`, `action`, `timestamp`, `ipAddress`, `userAgent`, `correlationId`.

Retention: 7 years (configurable per tenant). Export available via authenticated API for SOC 2 or other audit needs.

**FR-PR-004: Audit log query and export**
`POST api/audit-log/search` allows `dealer:corporate-admin` to query the audit log with filters on `eventType`, `userId`, `dateFrom`, `dateTo`, `resourceType`. Page size capped at 200. Export via `POST api/audit-log/export` returns a SAS URL to a generated CSV/JSON archive (60-minute expiry).

**FR-PR-005: IP allowlisting**
Per-tenant IP allowlist configuration. Authenticated requests originating outside the allowlist return 403 with `tenant-ip-restricted`. Anonymous intake endpoints (`api/intake/*`, `api/status/*`) are excluded from allowlisting.

**FR-PR-006: Field-level access control for sensitive data**
Certain fields (customer PII, technician notes, warranty eligibility flags) can be restricted by role at the field level. The `dealer:readonly` role at Premium tier never sees customer email or phone.

**FR-PR-007: SOC 2 attestation copies**
Premium customers receive copies of RVS's SOC 2 Type I (Phase 2) and Type II (Phase 3) attestation reports for their own compliance evidence files.

### 4.2 DMS Integration

**FR-PR-008: Bidirectional DMS integration (one partner)**
Premium tier ships with bidirectional integration with **one DMS** — IDS Astra OR Lightspeed — based on which partner program admits us first. Integration includes:

- **RVS → DMS:** New SRs from RVS push to the DMS as repair orders or service tickets
- **DMS → RVS:** Status updates, parts info, technician assignments, final invoicing data flow back to RVS
- **Reconciliation:** Daily reconciliation report identifying SRs in RVS not in DMS and vice versa, with manual resolution UI

Architecture: DMS-specific integration adapter implementing `IDmsIntegrationProvider` interface, with `IdsAstraIntegrationProvider` and `LightspeedIntegrationProvider` as concrete implementations.

**FR-PR-009: Mixed-DMS support per tenant**
Some dealer groups acquired locations running different DMS systems. Integration configuration is per-location, not per-tenant. A 30-location group might have 22 locations on IDS and 8 on Lightspeed; both flow into RVS. (Premium tier supports both partners RVS has integrations with; Enterprise Scale extends this further.)

**FR-PR-010: SFTP/CSV scheduled export (legacy DMS support)**
For locations whose DMS does not yet have a partner integration, daily SFTP push of structured SR data is supported. SFTP credentials per-location, stored in Azure Key Vault, configured by corporate admin.

**FR-PR-011: DMS reconciliation dashboard**
Dedicated UI for the operations team to review reconciliation discrepancies, manually resolve them, and view integration health metrics (push success rate, sync lag, error rate by location).

### 4.3 Pricing Structure

**FR-PR-012: Volume-banded per-location pricing for Premium**
Premium tier pricing is per-location with three volume bands tracked in `TenantConfig.BillingConfig`:
- 1–9 locations: $119/loc/mo
- 10–24 locations: $109/loc/mo
- 25–49 locations: $99/loc/mo

The applicable rate is determined by `TenantConfig.BillingConfig.LocationCountForBilling` at billing period start. ALL locations are billed at the band-applicable rate (not graduated). Premium tenants exceeding 49 locations transition to Enterprise Scale; system permits operation at 50+ locations on Premium pricing for up to 30 days while sales engagement converts the contract.

**FR-PR-013: No per-user pricing**
Premium tier does NOT charge per user. All users — technicians, advisors, managers, owners, regional managers, corporate admins — are unlimited. This protects the dataset thesis (technicians are the data-capture users; gating them would damage Section 10A coverage) and simplifies procurement-team conversations. Per-user pricing was eliminated in v3.3 of the pricing model.

Revenue from high-engagement Premium customers is captured through:
- Volume-banded per-location pricing (FR-PR-012)
- Optional Priority support tier upgrade ($500/mo, FR-PR-016)
- Banded implementation fees (FR-PR-014)

**FR-PR-014: Banded implementation fee**
Premium tier implementation fee is one-time, banded by location count at contract signing:
- 1–9 locations: $5,000
- 10–24 locations: $7,500
- 25–49 locations: $10,000

The implementation fee covers: SAML/SCIM IdP setup, Auth0 Organization provisioning, DMS integration configuration per location, location template setup, user bulk provisioning and training, Section 10A taxonomy review, white-glove onboarding session, runbook handoff, customer-specific configuration documentation.

Implementation add-ons priced separately and scoped at signing:
- Additional DMS integration beyond first: +$3,000 each
- Custom data migration from prior system: +$2,000–$10,000 (scoped at signing)
- Add-ons added during implementation: published rate plus 25% expedite premium

### 4.4 SLA and Support

**FR-PR-015: 99.9% SLA**
Monthly uptime target 99.9% for Premium customers (vs. 99.5% for Solo/Pro). Documented incident response and customer notification procedures. Quarterly disaster recovery exercises.

**FR-PR-016: Standard support included; Priority support optional**
Premium tier includes Standard support (1 business day response, email/portal channels). Customers may upgrade to Priority support for $500/mo additional. Priority adds:
- 4-hour response SLA, business hours
- Phone channel
- Faster escalation to engineering for unresolved issues

Priority support is opt-in via dashboard self-service. Most modal Premium customers (5-10 locations, low operational complexity) remain on Standard. Customers with high operational urgency or large staff (organizational complexity not captured in location count) typically upgrade.

Critical support tier (1-hour response, dedicated CSM channel) is bundled with Enterprise Scale and not available as a Premium add-on.

**FR-PR-017: Dedicated success manager**
Each Premium tenant assigned a dedicated success manager. Quarterly business review. Customer-facing escalation path. Onboarding ownership through implementation fee scope.

**FR-PR-018: Premium SR cap**
Premium tier SR cap is 1,000/loc/mo. At cap, intake submissions return HTTP 402 with the same customer-friendly message as Solo and Pro. Customers exceeding 1,000/loc/mo consistently are flagged for sales engagement on Enterprise Scale conversion (where SR cap is removed).

### 4.5 Advanced Benchmarking (Tier-Gated)

**FR-PR-019: Premium-tier benchmarking query depth**
Premium tier customers gain access to deeper benchmarking queries beyond Pro's limits:
- Full custom query API (vs Pro's predefined templates)
- Manufacturer/model-specific data
- Drill-down to component-failure level
- 14-day date ranges (vs Pro's 30-day, Solo's 90-day)
- 2,000 queries/month

K-anonymity enforcement remains: variable threshold k=5 for generic queries up to k=25 for OEM-relevant manufacturer/model/year queries. Even Premium customers cannot bypass k-anonymity. Detailed in `RVS_data_moat.md` §4.3.

### 4.6 Anti-Corpus-Theft Protections (Premium-Specific)

**FR-PR-020: Enhanced audit logging for benchmarking queries**
All Premium benchmarking queries logged to `auditLog` with full query parameters, result row count, and user context. Anomaly detection runs against query patterns:
- Sudden query volume spikes
- Systematic scanning patterns (incrementing parameters)
- Queries that don't match the dealer's actual operational profile
- Cross-referencing queries that suggest re-identification attempts

Flagged accounts surface in admin review queue.

**FR-PR-021: Premium tier ToS includes liquidated damages**
Premium customer contracts include explicit liquidated damages clause for benchmarking data misuse, redistribution, or reverse-engineering. Counsel-determined damages amount.

---

## 5. Functional Requirements (Enterprise Scale Tier)

Enterprise Scale inherits all Premium FRs and adds the following. **All Enterprise Scale features are sales-led custom configurations, not self-serve.**

### 5.1 Multi-DMS Integration

**FR-ES-001: Multiple DMS integrations supported**
Enterprise Scale customers can integrate with both IDS and Lightspeed simultaneously across their location portfolio (vs Premium's one-DMS limit). Per-location DMS configuration.

### 5.2 Custom Analytics and Reporting

**FR-ES-002: Custom analytics builder**
Enterprise Scale customers can build custom analytics views beyond the pre-defined Cross-Location Analytics dashboard. Drag-and-drop report builder with custom metrics, dimensions, filters, and visualizations.

**FR-ES-003: Custom data exports**
Scheduled or on-demand exports of structured tenant data (SRs, asset history, audit log) to customer-controlled storage (S3 bucket, Azure Blob container, SFTP endpoint). Customer-defined schemas and frequencies.

### 5.3 Support and Engagement

**FR-ES-004: Critical support tier (bundled)**
Enterprise Scale customers receive **Critical support** bundled with their contract:
- 1-hour response SLA, business hours
- All channels (email, portal, phone, dedicated CSM channel)
- 24/7 on-call rotation for P1 incidents (production-impacting)
- Direct escalation to engineering leadership for unresolved issues

Critical support is NOT available as a Premium add-on; it requires the Enterprise Scale tier. The Critical support is the formalized "24/7 priority support" referenced in the Enterprise Scale tier definition (`RVS_Context.md` §2.1).

**FR-ES-005: Quarterly executive business review**
Dedicated success team conducts quarterly business reviews with customer leadership. Includes ROI analysis, dataset contribution metrics, roadmap input, and strategic discussion.

**FR-ES-006: Co-marketing opportunities**
Joint case studies, conference appearances, customer council participation. Negotiated per contract.

### 5.4 OEM Data Partnership

**FR-ES-007: OEM data partnership coordination and revenue share**
For Enterprise Scale customers whose data forms a meaningful portion of OEM-relevant aggregates, RVS coordinates the customer's role in OEM data partnerships:
- Notice of OEM contracts that materially use the customer's anonymized data (within 30 days of contract execution)
- **Documented revenue-share clause** in the Enterprise Scale Master Services Agreement: when a customer's anonymized data represents ≥15% of an OEM-licensed aggregate (measured by SR contribution to the OEM's targeted asset population), the customer is entitled to a 5–10% revenue share of that OEM contract's annual value, paid quarterly in arrears. Specific percentage negotiated per ES contract.
- Reference customer status with permitted OEMs (joint case studies, OEM advisory board seats)
- Right of first refusal on OEM-funded co-development of vertical-specific features

Customer's data remains anonymized in any OEM commercial output. Revenue share is contingent on continuous active subscription (cancellation forfeits future quarterly payments; previously-earned amounts paid out under contract terms).

### 5.5 Pricing and Implementation

**FR-ES-008: Banded implementation fee**
Enterprise Scale implementation fee is one-time, banded by location count at contract signing:
- 50–99 locations: $15,000
- 100–249 locations: $25,000
- 250+ locations: $40,000

Add-ons (additional DMS integration, custom data migration, custom analytics dashboard config) priced separately and scoped at signing per `RVS_Context.md` §2.1. Implementation timeline: 60–180 days from contract signing to full production deployment.

**FR-ES-009: Per-location subscription pricing**
Enterprise Scale subscription pricing is per-location, negotiated per contract within the published range:
- Floor: $150/loc/mo (applies at 50-loc contracts)
- Ceiling: $300/loc/mo (applies at largest contracts where additional services warrant)

Contract may include volume discount provisions for tenant growth beyond contracted location count. Per-user pricing is NOT charged at Enterprise Scale (consistent with all other tiers; v3.3 model). Critical support tier is bundled (FR-ES-004); no support add-on fee applies.

**FR-ES-010: Multi-region data residency declaration (future)**
Enterprise Scale contracts can declare data residency requirements (US, Canada, etc.). MVP supports US-only (Azure US regions). Cross-border or multi-region tenants are deferred to year 2+.

### 5.6 Dedicated Engineering and SLA Customization

**FR-ES-011: Dedicated solutions engineer**
Each Enterprise Scale tenant is assigned a **dedicated solutions engineer** for the duration of the contract. This is a named individual on the RVS engineering team (not a rotating pool resource) responsible for:

- DMS integration architecture review and validation (initial deployment + ongoing changes)
- Custom analytics dashboard configuration and report builder enablement (FR-ES-002)
- Custom data export pipeline setup and maintenance (FR-ES-003)
- Custom SLA monitoring and incident response coordination (FR-ES-012)
- Direct technical escalation channel beyond Critical support
- Quarterly architecture review and capacity planning sessions
- Co-design of OEM data integration projects when applicable

The solutions engineer commitment is approximately 0.10–0.25 FTE per ES customer depending on contract size and complexity. For ES contracts ≥150 locations, the commitment increases to 0.25–0.50 FTE. This human capital is the largest single cost driver justifying the Enterprise Scale per-location rate above Premium.

The solutions engineer is distinct from the dedicated success manager (FR-PR-017): success manager owns business relationship and adoption; solutions engineer owns technical architecture and delivery.

**FR-ES-012: Custom SLA negotiation**
Enterprise Scale contracts may negotiate SLA terms beyond the standard 99.9% uptime baseline (FR-PR-015). Available customizations:

- **Higher uptime targets** (99.95% or 99.99% available; requires multi-region active-active architecture, deferred to Phase 4+ for 99.99%)
- **Service credits** for SLA breach (standard ES contract: 5% of monthly fee per 0.1% below SLA target, capped at 50% of monthly fee; negotiable up to 100% credit cap for largest contracts)
- **Custom incident response procedures** (e.g., named technical contact for P1 incidents, on-site escalation triggers, customer-specific runbooks)
- **Custom maintenance windows** (vs. standard maintenance window; minimizes disruption to customer's operational hours)
- **Defined escalation chain** with named RVS engineering leadership contacts for unresolved P1 incidents
- **Mutual NDA-protected post-incident reviews** within 5 business days of any P1 event

Custom SLA terms are negotiated per ES contract and documented in the Master Services Agreement as a separate exhibit. Solutions engineer (FR-ES-011) is responsible for monitoring SLA conformance and reporting quarterly to the customer.

---

## 6. Non-Functional Requirements

### 6.1 Performance (Premium and Enterprise Scale)

| Metric | Target |
|---|---|
| Cross-location queue load (50 locations) | P95 < 2 seconds |
| Cross-location analytics dashboard load | P95 < 4 seconds |
| Industry benchmarking query response | P95 < 3 seconds |
| Audit log query (1 month range) | P95 < 5 seconds |
| Bulk user import (500 users) | < 60 seconds |
| Bulk location import (50 locations) | < 30 seconds |
| DMS bidirectional sync lag | < 5 minutes (median) |

### 6.2 Availability

- Premium SLA: **99.9% monthly uptime**
- Enterprise Scale SLA: **99.9% monthly uptime + 24/7 support**
- Documented incident response and customer notification procedures
- Quarterly disaster recovery exercises

### 6.3 Security and Compliance

- **SOC 2 Type I** ready by Phase 2 ship; **Type II** by Phase 3
- All access events captured in audit log
- Data encrypted at rest (Cosmos managed keys) and in transit (TLS 1.3)
- Penetration testing annually (cost: ~$15K–$30K)
- Customer-controlled data retention for audit log (1–7 years)

### 6.4 Scalability

- Single tenant must support **up to 200 locations** without architecture changes
- Cross-location analytics queries must remain single-partition (Cosmos partition is `tenantId`)
- Asset ledger queries must remain single-partition per asset

---

## 7. Pricing Reference

### 7.1 Premium Tier Monthly Pricing (subscription only; no per-user fees)

| Locations | Band | Per-loc rate | Monthly | Annual ACV (15% prepay) |
|---|---|---|---|---|
| 5 | 1–9 | $119 | $595 | $6.1K |
| 10 | 10–24 | $109 | $1,090 | $11.1K |
| 15 | 10–24 | $109 | $1,635 | $16.7K |
| 25 | 25–49 | $99 | $2,475 | $25.2K |
| 35 | 25–49 | $99 | $3,465 | $35.3K |
| 49 | 25–49 | $99 | $4,851 | $49.5K |

Premium ACV excludes Priority support add-on ($500/mo = $5.1K ACV at prepay) and excludes implementation fees (one-time, see FR-PR-014).

### 7.2 Enterprise Scale ACV (custom contract, annual prepay 15% off)

| Locations | Per-loc range | Monthly | Annual ACV (15% prepay) |
|---|---|---|---|
| 50 | $150 (floor) | $7,500 | ~$76.5K |
| 75 | $150–$200 | $11,250–$15,000 | $115K–$153K |
| 100 | $150–$250 | $15,000–$25,000 | $153K–$255K |
| 150 | $175–$275 | $26,250–$41,250 | $268K–$421K |
| 250+ | $200–$300 | $50,000+ | $510K+ |

These are planning numbers. Real prices come from real negotiations. Critical support is bundled at all Enterprise Scale price points (no separate add-on fee). Implementation fees one-time per FR-ES-008.

### 7.3 Transition Discounts

**Pro→Premium transition discount:** 50% off Premium subscription for first 3 months. Sales lever, not published. Applies to subscription only, not implementation fees. Example: 5-location customer upgrading sees Premium at $298/mo (vs. $595) for 3 months, then full price.

**Premium→Enterprise Scale transition discount:** 50% off Enterprise Scale subscription for first 3 months. Sales lever, not published. Applies to subscription only, not implementation fees. Example: 50-location customer upgrading sees Enterprise Scale at $3,750/mo (vs. $7,500) for 3 months, then negotiated rate.

Both discounts apply to ongoing subscription fees only; implementation fees are full price even during a discount period. This avoids customers gaming the discount by triggering re-implementation events.

### 7.4 Implementation Fee Reference

Per FR-PR-014 and FR-ES-008:

| Tier | Locations | One-time fee |
|---|---|---|
| Premium | 1–9 | $5,000 |
| Premium | 10–24 | $7,500 |
| Premium | 25–49 | $10,000 |
| Enterprise Scale | 50–99 | $15,000 |
| Enterprise Scale | 100–249 | $25,000 |
| Enterprise Scale | 250+ | $40,000 |

Add-on services priced separately, scoped at signing:
- Additional DMS integration beyond first: +$3,000 each
- Custom data migration from prior system: +$2,000–$10,000 (scoped)
- Custom analytics dashboard config (Enterprise Scale): +$5,000–$15,000 (scoped)
- Add-ons added during implementation: published rate plus 25% expedite premium

---

## 8. Implementation Sequencing

Detailed in [`RVS_Implementation_Plan_v2.md`](RVS_Implementation_Plan_v2.md). Summary:

| Quarter | Focus |
|---|---|
| Q1 (months 7–9) | Auth0 Organizations migration, SAML/SCIM, audit log, IP allowlisting, anonymization pipeline, tiered benchmarking, verification gate |
| Q2 (months 10–12) | First DMS partner integration (IDS or Lightspeed), reconciliation dashboard, per-user pricing infrastructure, Premium tier GA |
| Q3 (months 13–15) | Second DMS partner integration (if first validates), Enterprise Scale features (custom analytics, custom exports), 24/7 support model |
| Q4+ (months 16–18) | First Enterprise Scale customer onboarding, OEM pilot infrastructure, SOC 2 Type I |

Premium GA target: month 9. First paying Premium customer: month 9–12. First paying Enterprise Scale customer: month 12–18.

---

## 9. Success Criteria

**Premium tier ships when:**
- Cross-location analytics dashboard in production with positive feedback (carried over from Pro tier)
- SAML SSO validated with at least 2 different IdPs (Okta + Entra ID, ideally)
- Audit log captures all required event types with passing manual audit
- DMS integration with at least one partner in production at a Premium design partner
- Anonymization pipeline operational with k-anonymity enforced
- Verification gate operational with established review SLA (24–72 hours)
- Tiered benchmarking access (Solo/Pro/Premium) implemented with rate limits
- ToS, MSA, and DPA template documents reviewed by counsel
- All Phase 1 (Solo + Pro) ship criteria continue to be met
- Asset ledger has ≥10,000 events with ≥95% taxonomy adherence

**Enterprise Scale tier ready when:**
- Premium tier has 3+ paying customers
- Custom analytics builder in production
- Multi-DMS support validated at a Premium customer
- 24/7 support model operational
- SOC 2 Type I attestation obtained
- First Enterprise Scale customer signed (sales-led)

---

## 10. Open Questions

| # | Question | Owner | Due |
|---|---|---|---|
| OQE-01 | Which DMS partner — IDS or Lightspeed — first? | GTM + Engineering | Phase 2 kickoff |
| OQE-02 | SOC 2 audit timing — Type I before first Premium customer or Type II after first 3 customers? | Compliance / GTM | Before first Premium pitch |
| OQE-03 | Industry benchmarking k-anonymity threshold — initial k=5; pressure-test variable thresholds with first benchmarking queries | Engineering + Legal | Phase 2 design |
| OQE-04 | Should the analytics dashboard support custom report builder (more flexible, Premium) or fixed reports (faster to build)? | GTM + Engineering | Phase 2 sprint planning |
| OQE-05 | Premium pricing — fixed Premium tier or fully custom per contract for larger Premium customers (40+ loc)? | GTM | Before first sales conversation |
| OQE-06 | Multi-region data residency — when do we need Canada or EU regions? | GTM | When first non-US prospect signs |
| OQE-07 | Per-user pricing on Premium — monthly true-up or quarterly true-up for Stripe metered usage? | Engineering | Phase 2 kickoff |
| OQE-08 | Pro→Premium transition discount — applied via Stripe coupon (50% off subscription only, first 3 months per FR-BILL-08); test edge cases of mid-period upgrade and discount expiration | Engineering | Phase 2 kickoff |
| OQE-09 | OEM data partnership coordination for Enterprise Scale — case-by-case, or template revenue-share contract? | GTM + Legal | After first OEM pilot signed |

---

*End of RVS_Premium_PRD.md.*
