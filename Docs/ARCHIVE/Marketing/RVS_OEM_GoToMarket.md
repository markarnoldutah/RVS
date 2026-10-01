# RVS OEM Go-to-Market Strategy

**Version:** 1.0
**Date:** April 30, 2026
**Status:** Internal strategic document — not for external sharing
**Related:** [`RVS_data_moat.md`](RVS_data_moat.md), [`RVS_Competitive_Strategy.md`](RVS_Competitive_Strategy.md)

> **Document discipline note:** This is a planning document, not a sales document. It contains internal strategy, target accounts, deal structures, and acquisition thinking that must not be shared externally. If pieces of this document are needed for external use (pitch decks, contracts), excerpt and re-write — do not share the source.

---

## 1. The Strategic Position of the OEM Track

### 1.1 What OEM Revenue Is For

The OEM data licensing track is not the company's primary revenue line in years 1–2. It is:

1. **Validation that the dataset has commercial value** — one OEM contract proves the moat is real
2. **The strategic asset that fundamentally changes RVS's company category** — software → data infrastructure
3. **The most likely path to a high-multiple acquisition** — data companies trade at higher multiples than SaaS companies
4. **The differentiator that makes Enterprise pricing defensible** — Enterprise customers pay for benchmarking access that exists because the OEM track is being built

### 1.2 The Honest Risk

The OEM thesis may not validate. Concretely:

- OEMs may decide they don't want third-party data (some OEMs are aggressively building internal warranty data programs)
- The procurement cycle may be too long for RVS's runway
- The dataset may not reach OEM-grade coverage in the timeframe required
- Competitors (a DMS partnership, a J.D. Power expansion into RV) may capture the OEM data slot first

The alternate monetization paths if the OEM thesis fails are documented in §7. Keep them live as backup positioning, but do not lead with them.

---

## 2. Who the OEM Buyers Are

### 2.1 Buyer Constituencies (Reprise from Conversation)

OEMs are not monolithic. Three distinct constituencies, in order of likely first-buyer status:

**Warranty Operations / Quality Assurance**
- Has measurable budget (warranty reserve is a balance sheet line)
- ROI is dollar-quantifiable (recovered claims pay for data 10x over)
- Procurement template exists (already buys data from third parties)
- Decision cycle: 6–12 months from first contact
- Authority to sign: Director-level, sometimes VP

**Engineering / Product Quality**
- Wants the data; doesn't sign POs
- Influences purchase if Warranty Ops is convinced
- Decision cycle: not directly applicable; influences via Warranty Ops

**Sales / Brand**
- Cares about dealer performance and brand protection
- Wants benchmarking data on dealer networks
- Politically sensitive (don't want to look like they're spying)
- Decision cycle: 9–18 months
- Likely second-wave revenue, after Warranty

### 2.2 Target OEM Tiering

Tier A — first targets (next 12–18 months):

- **Brinkley RV** — newer brand, growth-stage, aggressively positioned around quality, smaller installed base means coverage is easier to reach
- **Alliance RV** — independent, quality-focused, similar profile to Brinkley
- **Grand Design** — Winnebago Industries subsidiary, but with relative autonomy; large enough to matter, mid-tier brand visibility

Tier B — secondary targets (months 18–30):

- **Forest River** (especially specific divisions: Cherokee, Coachmen, Salem)
- **Thor Industries** (specific brands: Airstream, Jayco — though Lightspeed has Jayco partnership)
- **Winnebago Industries** main brand — Kenect partnership concern; entry will be harder

Tier C — long-term (months 30+):

- Component manufacturers (Lippert, Dometic, Norcold, Furrion, etc.) — they care about *their* part failure data, not whole-unit data
- Insurance carriers underwriting RV policies (Progressive, Foremost, National General)
- Extended Service Contract (ESC) providers

### 2.3 Why Not Lead with Tier 1 OEMs

Thor and Winnebago are tempting first targets because of size. They are wrong first targets because:

- Decision cycles at 30,000-employee OEMs are 18–24 months minimum
- Competitive vendors (J.D. Power, internal data programs, partnerships with DMS) are deeply entrenched
- A "no" from Thor poisons subsequent conversations
- Smaller, hungrier OEMs are willing to pilot when bigger OEMs require production-grade data

**Lead with Brinkley or Alliance.** Win one. Use it as proof. Then approach Tier B with a credible reference.

---

## 3. The Sales Process for OEMs

### 3.1 Pre-Contact Phase (Months 0–12)

Before contacting any OEM commercially, the foundation must be in place:

1. **ToS and DPA language reviewed by counsel** — the legal grant to commercialize anonymized data must be airtight. ✓ This is gating; do not skip.
2. **Anonymization pipeline operational** — k-anonymity guarantees enforced, audit-traceable.
3. **Dataset coverage at minimum threshold for the target OEM** — see `RVS_data_moat.md` §6.
4. **Sample data documentation** — a non-confidential overview of what the dataset contains, what queries it supports, what coverage looks like.
5. **At least one Enterprise customer signed and referenceable** — proves RVS is a real company with paying customers, not a pre-product pitch.

### 3.2 First Contact (Month 12+)

The first contact with an OEM is **not** a sales call. It is intelligence gathering and relationship building.

- Coffee, dinner, conference meetings
- "We're building a structured service intelligence dataset across RV dealers; would love to hear what data you wish you had"
- Listen, don't pitch
- Goal: understand their warranty cost pain, their internal data programs, their procurement gates, their budget cycles
- Output: notes on what would constitute a compelling pilot for them specifically

### 3.3 Pilot Proposal (Month 14–18)

If the relationship warms, propose a scoped pilot:

- Specific OEM brand and model line
- 12-month engagement
- Data scope: failure pattern analysis for 1 component category (e.g., slide systems, refrigerators, generators)
- Deliverables: monthly report + quarterly insight session + ad-hoc query access
- Price: $50K–$150K depending on scope and OEM size
- Mutual evaluation criteria: are the insights actionable? do the numbers correlate with internal warranty data?

The pilot is intentionally cheap (loss-leader-ish) because it's about validation. The follow-on production contract is where revenue happens.

### 3.4 Production Contract (Month 18–30)

Successful pilot → production data feed contract:

- Multi-year (2–3 year minimum)
- $200K–$750K/year depending on data scope
- Quarterly business review, dedicated success/data analyst contact
- Right to expand scope as new component categories or use cases emerge
- **Non-exclusive** — RVS retains the right to license to other OEMs on similar terms

### 3.5 What to Avoid

These are deal-killers for long-term value:

- **Exclusive licensing.** Once we license exclusively to one OEM, the data's value to all other OEMs drops to ~zero. Refuse exclusivity, period.
- **OEM minority equity investment.** Tilts us toward acquisition by them at a discount; reduces leverage with their competitors.
- **"Strategic partnership" with right-of-first-refusal on acquisition.** Caps exit value.
- **Free data in exchange for endorsement or co-marketing.** They get the value without paying; sets precedent for next OEM.
- **Building OEM-specific custom features.** The dataset is the product; custom analytics for OEM X are bespoke service work that doesn't scale.

---

## 4. Pricing the OEM Track

### 4.1 Reference Pricing

Calibrated to adjacent industries (J.D. Power automotive data licensing, CarMD, IHS Markit/S&P Mobility):

| Tier | Price | What It Includes |
|---|---|---|
| Pilot | $50K–$150K / 12 months | Scoped to 1 product line + 1 component category; monthly reports + ad-hoc queries |
| Production data feed (single brand) | $200K–$500K / year | Full dataset access for 1 OEM brand; quarterly business reviews; query API access |
| Production data feed (multi-brand) | $500K–$1M / year | OEMs with multiple subsidiary brands (Forest River, Thor) |
| Strategic data partnership | $1M–$3M / year | Includes co-developed dashboards, regular insight workshops, custom analytical projects |

These are anchors. Real prices come from real negotiations. Smaller OEMs pay lower; larger OEMs and broader scope pay higher.

### 4.2 What Drives Price Up

- Coverage of the OEM's installed base
- Time-series depth (3+ years of data is dramatically more valuable than 1 year)
- Granularity (component-level detail vs. category-level)
- Inclusion of comparative benchmarking (how does Brinkley compare to industry?)
- Strategic exclusivity within a tier (not full exclusivity, but e.g., "only Tier A licensee for slide system data")

### 4.3 What Drives Price Down

- Limited coverage
- Short time-series
- Generic aggregates without per-model detail
- Multiple OEMs already licensed (reduces scarcity)

---

## 5. The Acquisition Thesis

### 5.1 Why Build for Acquisition (and Why It's Risky to Optimize Explicitly)

Acquisition is a likely outcome path. Building for acquisition explicitly is, however, a known way to destroy company value. The discipline is:

> **Build a company that an acquirer would want to own — and that has full standalone value if no acquisition ever happens.**

The decisions that maximize standalone value almost entirely overlap with the decisions that maximize acquisition value. The few that diverge usually favor standalone value.

### 5.2 Likely Acquirer Categories

**Type 1 — DMS (IDS, Lightspeed):** Plausible. Acquires for customer base + integration into their platform + neutralizing competition. **Lowest valuation multiple** because DMS culture doesn't price the data thesis. Estimated 4–8x ARR.

**Type 2 — OEM directly:** Plausible. Acquires for proprietary access to failure intelligence. **Medium multiple** with strategic alignment risk (post-acquisition pressure to favor acquirer's brand). Estimated 6–15x ARR depending on strategic fit.

**Type 3 — Industrial data company expanding into RV:** S&P Mobility, IHS Markit, J.D. Power, or PE-backed industrial data roll-up. Acquires because the dataset slots into their existing licensing playbook. **Highest multiple** because data companies trade higher than software companies. Estimated 8–20x ARR.

The exit we want is Type 3. The decisions that build toward Type 3 are:

- Treat the dataset as the product
- Build dataset durability through multi-year contracts and embedded integrations
- Avoid exclusive arrangements that anchor us to a single OEM
- Build a customer base across multiple OEMs (3+ paying OEM customers in production is a much stronger position than 1)
- Maintain anonymization rigor that survives acquirer due diligence

### 5.3 Anti-Patterns That Destroy Acquisition Value

- Hiring a banker before the asset is built
- Discussing acquisition with any single OEM (especially before having multiple OEMs paying)
- Pitching at investor conferences with "exit potential" in the deck
- Naming OEMs as customers explicitly in marketing
- Taking minority equity from a strategic acquirer
- Concentrating revenue in a single OEM customer (concentration risk caps exit multiple)

### 5.4 What "Looks Like" Maximum Acquirability

In year 3:

- $5M+ ARR, growing 60%+ YoY
- 30+ Enterprise customers across diverse dealer groups
- 3+ OEM data customers across non-overlapping brands
- A dataset documented with rigor (coverage, quality, lineage, anonymization audit trail)
- Multiple categories of acquirer have approached
- The company can credibly continue independently

That's the position from which acquisition negotiations are conducted from strength.

---

## 6. Cap Table Discipline

### 6.1 Bootstrap as Long as Possible

The OEM thesis takes 18–30 months to validate. Raising capital before validation prices the company on the SaaS thesis (lower valuation). Raising after validation prices it on the data thesis (significantly higher).

**Bootstrap if at all possible** until either:

1. First OEM pilot under contract (validates the data thesis; raise after)
2. Cash runway forces it (raise on the strongest available story)

### 6.2 What to Avoid

- **Strategic investment from any RV industry player** — distorts incentive structure and signals for-sale energy
- **Investor mandates that push toward "be more SaaS-y"** — incompatible with the data thesis
- **Convertible notes from OEM-aligned investors** — same concentration risk as direct OEM investment

### 6.3 What's OK

- Friends-and-family or angel rounds at reasonable terms (avoid SAFEs at very low caps that punish later)
- Industry-agnostic seed VCs who understand vertical SaaS / data plays
- Revenue-based financing once Enterprise revenue is real
- Strategic SBA or credit lines for working capital

---

## 7. Backup Monetization Paths (If OEM Thesis Doesn't Validate)

If month 18 arrives and no OEM is ready to pilot, the company doesn't fail — it pivots monetization. Options:

### 7.1 Parts Suppliers

Component manufacturers (Lippert, Dometic, Furrion, etc.) want failure data on their own parts. Procurement is faster than OEMs (smaller companies, simpler bureaucracy). Pricing similar but lower volume per contract.

### 7.2 Insurance Underwriters

Progressive, Foremost, and National General write significant RV policy volume. Failure pattern data informs underwriting and claims handling. They already buy data from third parties.

### 7.3 Extended Service Contract Providers

ESC companies (Good Sam, Wholesale Warranties, RV America) price contracts based on expected failure rates. Better data = better pricing models. Smaller deals than OEMs but easier procurement.

### 7.4 Aftermarket Parts E-commerce

Companies selling RV parts (Amazon, eTrailer, Camping World retail) want to know what's failing where. Could power inventory and recommendation algorithms.

### 7.5 Industry Analyst Subscriptions

A subscription product for industry analysts, financial analysts covering Winnebago/Thor stocks, and consulting firms. Lower individual price ($10K–$50K/year per subscriber), broader market.

The point of listing these: **the dataset has commercial value beyond OEMs.** If the primary OEM thesis takes longer than expected, these are real businesses to pursue. None pays as well or proves the thesis as cleanly, but each is a viable monetization path that doesn't require redoing the architectural work.

---

## 8. The Operational Cadence

### 8.1 OEM Track Activities by Quarter

| Quarter | Activity |
|---|---|
| Q1 (post-Phase 1 Solo+Pro ship, ~month 4–6) | ToS/DPA legal review; anonymization pipeline design; identify Tier A target OEMs |
| Q2 (~month 7–9) | First OEM intelligence-gathering meetings; Premium tier launch; benchmarking dashboard live |
| Q3 (~month 10–12) | Continue OEM relationship building; first OEM pilot proposal (if relationships warm) |
| Q4 (~month 13–15) | First OEM pilot under contract or definitive feedback on what's blocking |
| Year 2 | Pilot execution; expansion to Tier B OEMs; first production contract negotiation |
| Year 3 | Multiple production OEM contracts; positioning for strategic options |

### 8.2 Internal Reporting

Monthly OEM track review (separate from product/revenue review):

- Active OEM relationships and stage
- Coverage progress for Tier A targets
- Pilot proposals outstanding
- Pilot revenue YTD
- Production contract pipeline
- Anonymization pipeline health
- Any concerning signals (competitor announcements, OEM internal data programs, etc.)

---

## 9. The Things That Will Tempt Us to Compromise (And Why Not To)

These are predictable failure modes:

**"Big OEM X wants to invest in us."** Decline. Take the meeting, take the relationship-building, decline the equity. Refer back to §3.5 and §5.3.

**"OEM Y will pay double if we sign exclusive."** Decline. The non-exclusivity preserves multi-OEM revenue and acquisition optionality.

**"We need to hit revenue targets so let's sell anything to anyone."** No. The dataset is the strategic asset. Selling raw or insufficiently-anonymized data to short-circuit revenue is reputationally and legally devastating.

**"Let's just build a custom analytics product for OEM Z."** Bespoke services don't scale, and they signal we're a consultancy rather than a data company. If OEM Z wants custom, the answer is "here's our data; here's a SOW for a 90-day custom project at consultancy rates."

**"The dataset isn't ready but if we wait we'll run out of money."** Then raise capital on the SaaS thesis. Or pursue backup monetization (§7). Or reduce burn. Selling underbaked data poisons the well.

The discipline is: **the dataset's reputation is the company's reputation.** Compromising on quality, anonymization rigor, or non-exclusivity early creates problems that can't be unwound.

---

*End of RVS_OEM_GoToMarket.md.*
