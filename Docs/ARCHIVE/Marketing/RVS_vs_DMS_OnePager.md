# RVS vs. DMS (IDS Astra G2 & Lightspeed) — One-Pager

**For: Internal study & sales conversations** | **Audience: You, then dealer GMs / owners**

---

## The 10-Second Answer

> **"Your DMS is the system of record for the *transaction*. RVS is the system of record for the *repair* — the structured failure data, the technician's outcome, and the cross-dealer pattern your DMS was never built to capture."**

Memorize this. It's your opening line whenever a dealer says *"isn't this just what my DMS does?"*

---

## Critical Frame: This Is Not Like Kenect

| | Kenect | DMS (IDS / Lightspeed) |
|---|---|---|
| Relationship | Peer in the "in front of DMS" layer | **System of record below you** |
| Strategy | Coexist by carving distinct jobs | Coexist by **feeding them better data** |
| Threat type | Could ship intake form on top of distribution | Already partnered with OEMs you want |
| Sales line | "Keep Kenect, we're complementary" | **"We make your DMS more valuable, not redundant"** |

Wrong frame here = lost deal. The DMS is incumbent infrastructure. You **never** suggest replacing it. You feed it.

---

## Who They Are (Know the Incumbents)

### IDS Astra G2 (Integrated Dealer Systems)
- **40 years** in RV/marine; **1,200+ dealership locations**, ~10,000 software users
- Wake Forest, NC; deep RVDA presence
- 2025 moves: **IDS Pay** (embedded payments), **VINRV partnership** (free AI VIN decoding for all customers as of Apr 2025)
- "All roads lead to accounting" is their explicit pitch
- Has a Technology Partner Program — **this is your friendly path in**

### Lightspeed DMS
- **~40 years**, **4,500+ dealers**, "#1 DMS in Recreation"
- Salt Lake City, UT (your backyard)
- 2025 moves: VIN decoding integrations with **Winnebago, Grand Design, Brinkley, Jayco, KZ, Alliance**, Service Tech Video, AI-powered insights, multi-location enhancements
- Has **"Service Scheduler"** marketed explicitly to **reduce RECT** ← *same acronym you use*
- RVDA + RVIA member

**Punchline:** Both are 30–40-year incumbents. Together they own the majority of the RV dealer DMS market. Your buyer has one of them. Don't pretend otherwise.

---

## What the DMS Actually Does (Be Honest)

The DMS is genuinely good at:
- Accounting, GL, AR/AP, payroll
- Parts inventory, pricing, POs, ROs
- Sales workflow, F&I, CRM
- Warranty claims, manufacturer integrations
- Multi-location consolidation, reporting
- Embedded payments (now)
- Basic VIN decode (now, free)
- Mobile tech tools tethered to repair orders

**Don't try to compete with any of this.** You will lose. They have 30+ years of dealer-specific accounting logic that's invisible from the outside but critical to operations.

---

## Where the DMS Is Genuinely Weak (Your Wedge)

These are facts, not spin:

1. **DMS captures billing data, not failure data.** Free-text "Complaint/Cause/Correction" fields. Across 100 repairs you get *bad pump / pump failure / hydraulic pump seized / pump bad* — uncategorized, unanalyzable.

2. **No structured failure-mode taxonomy.** Categorization is by labor code or department (Service / Warranty / Internal / Customer Pay), not by component or failure type.

3. **Intake context is missing.** DMS only sees the final repair. The customer's photos, video of the noise, voice description, urgency — none of it lives in the DMS.

4. **Cross-dealer analysis is structurally impossible.** Each dealer's DMS is an isolated database. Lightspeed has 4,500 dealers' data — but they don't pool it for failure analytics, and dealers wouldn't allow it without explicit consent and structure.

5. **Reports are accounting reports.** Labor revenue, parts revenue, tech hours, open ROs. Not "which slide motors fail in Y3 of ownership" or "which 2024 Momentum units have hydraulic issues."

6. **Technicians enter minimal notes.** "Replaced pump." Sufficient for billing, useless for analytics. The interface incentive isn't there.

7. **Data is hard to extract.** Proprietary databases, limited exports, inconsistent fields. Your SFTP CSV export feature is real value here.

8. **No customer-facing intake portal.** DMS opens a repair order *after* the customer arrives. Your portal opens it *before*.

---

## What Changed in 2025 (Don't Get Caught Off Guard)

Your existing `RVS_vs_DMS_value_prop.md` was written before some of this. **Update your mental model:**

- **Free OEM VIN decoding is now table stakes.** IDS Pay launched embedded payments. Lightspeed shipped VIN decoding for the biggest RV brands. *Your VIN scan is no longer a wedge feature — it's parity.*
- **Lightspeed has Service Tech Video.** Some of the "tech-facing tooling" space is contested.
- **Lightspeed Service Scheduler explicitly targets RECT reduction.** They've co-opted your KPI language. You need to be specific about *which* RECT problem you solve (pre-arrival diagnostic gap), not just say "we reduce RECT."
- **Both DMSs have direct OEM partnerships.** Winnebago, Grand Design, Jayco, Brinkley are inside IDS or Lightspeed — but for *VIN decoding and parts pricing*, not failure analytics. **That door is still open. But it's narrower than it was 12 months ago.**

---

## The Positioning Map (Commit This to Memory)

```
┌──────────────────────────────────────────────────────────┐
│  CUSTOMER SIDE                                           │
│  ▲                                                       │
│  │  Kenect (conversations, reviews, payments)            │
│  │  RVS    (structured intake, technician app, ledger)   │
│  │  ──────────────── ⇩ ⇩ ⇩ feeds data into ⇩ ⇩ ⇩          │
│  │  DMS    (accounting, parts, RO, warranty, payments)   │
│  ▼                                                       │
│  DEALER BACK OFFICE                                      │
└──────────────────────────────────────────────────────────┘
```

If a dealer asks you to draw it, draw this. Three layers, three jobs.

---

## The Capability Matrix (Memorize Bolded Cells)

| Capability | DMS (IDS / LS) | RVS |
|---|---|---|
| Accounting / GL / AR-AP | **Core** | No |
| Parts inventory & POs | **Core** | No |
| Repair Order management | **Core** | No (we feed it) |
| Warranty claim filing | **Core** | No |
| F&I, sales workflow | **Core** | No |
| Free-text Complaint/Cause/Correction | Yes | We **structure** this |
| Embedded payments | Yes (2025) | No |
| OEM VIN decode | Yes (2025) | Yes (parity) |
| Customer mobile intake portal | **No** | **Core** |
| Pre-arrival photos/video/voice description | **No** | **Core** |
| Structured failure-mode taxonomy | **No** | **Core** |
| Section 10A capture (component/mode/action/parts/labor) | **No** | **Core** |
| **Cross-dealer asset ledger** | **Architecturally impossible** | **Core moat** |
| Offline-first technician mobile | Limited (RO-tethered) | **Core (`MAUI.Tech`)** |
| Magic-link cross-dealer customer status | **No** | **Core** |
| AI issue categorization at intake | **No** | **Core** |

---

## Positioning Rules

1. **Never say "DMS replacement."** Ever. You're done in the room if you do.
2. **Always say "intake layer + repair intelligence."** That's your category.
3. **Lead with what DMS can't do, not what DMS does badly.** The structural impossibility of cross-dealer analytics is more powerful than "your free-text field is messy."
4. **Position SFTP/CSV export as a *gift to your DMS*.** It makes their system more useful, not less.
5. **Pursue the IDS Technology Partner Program early.** Being a certified partner is worth more than five sales meetings. Lightspeed has integration partners too — find the equivalent path.
6. **Don't bash IDS or Lightspeed in a meeting.** Dealers have invested years training staff on those systems. Insulting their DMS insults their judgment.

---

## Honest Caveats (Don't Sell Past These)

- **VIN decode is no longer a wedge.** Your `RVS_PRD.md` FR-003 features it prominently — it's still valuable inside the intake flow, but don't lead the sales pitch with it.
- **"Sit in front of the DMS" is contested language.** Kenect uses it. Some DMS partners use it. Your *real* differentiation is "structured failure data the DMS architecturally cannot produce."
- **Your data moat depends on dealers letting you aggregate.** DMS data is dealer-owned and siloed *by contract*. You need explicit per-tenant consent in your ToS for cross-dealer benchmarking — and you need it from day one. (Worth verifying this is in your terms.)
- **OEM relationships are partly closed.** Lightspeed has Winnebago, Grand Design, Jayco, Brinkley locked in for VIN/parts data. They are not yet locked in for *failure analytics* — that's still your opening — but the relationships are warm to Lightspeed, cold to you.
- **Your $199–$499/mo pricing sits next to IDS/Lightspeed's much larger seat-based fees.** That's actually fine — you're a complementary line item, not a replacement budget. But it means you cannot position as "the only system you need."

---

## Sales Objection Handling

| Dealer says... | You say... |
|---|---|
| *"Doesn't my DMS already do this?"* | "Your DMS captures the repair order. RVS captures the *diagnosis* — what failed, how, and how often across thousands of similar units. Different question, different data." |
| *"Lightspeed has a Service Scheduler that reduces RECT."* | "Scheduling reduces RECT *after* the unit arrives. We reduce RECT *before* it arrives — by giving the tech the symptoms, photos, and category before the bay door opens. Different RECT lever." |
| *"IDS has VIN decoding now, free."* | "Good — we use it the same way they do. The VIN is just the start. The value is everything you capture *about that VIN over time* — and that's not in IDS." |
| *"I don't want another system to log into."* | "Your advisors don't switch systems — RVS feeds the SR straight into your DMS via SFTP or CSV. The only new tool is the customer's intake form and your tech's mobile app. Both replace processes that aren't in the DMS today." |
| *"Will you integrate with my DMS directly?"* | "Today, daily SFTP export of structured SR data into your DMS. Roadmap: API integration as IDS/Lightspeed open partner endpoints. We're targeting their Technology Partner Programs." |
| *"Why would I pay you when I already pay IDS / Lightspeed?"* | "You pay them to track *what you sold and serviced*. You'd pay us to know *what's actually failing across your fleet* — and to send the tech to the bay knowing it. They're billing software. We're repair intelligence." |
| *"My DMS captures all my service data."* | "It captures the *transaction*. Try this: pull all your slide-out repairs from the last year and tell me the most common failure mode across them. The DMS can't answer that — the data's there but it's free text. We make it queryable from day one." |

---

## What to Build / Emphasize Because of This

1. **SFTP/CSV DMS export is a *headline feature*, not a checkbox.** Your FR-013 should be Tier 1 marketing material. *"Plays nicely with your existing DMS"* is the most disarming thing you can say.
2. **Structured failure-mode taxonomy is your IP.** Document it well. The lookup sets in your `lookupSets` container are part of the moat — a curated taxonomy is hard to bootstrap.
3. **Dealer ToS must permit aggregated/anonymized cross-dealer analytics.** Without this, your data moat is legally hollow. Verify with counsel.
4. **Apply to IDS Technology Partner Program early.** Even if you can't ship a deep integration on day one, being listed is sales-team gold.
5. **Don't oversell VIN scan.** It's now parity. The wedge is *what happens after the VIN is captured* — photos, video, voice, AI-suggested category, structured wizard answers, asset ledger entry.
6. **Build a real DMS comparison page on your website** that says "RVS + IDS" and "RVS + Lightspeed" — not "RVS vs."

---

## PRD Edits to Consider

| File | Change |
|---|---|
| `RVS_vs_DMS_value_prop.md` | **Update for 2025 reality:** add note that IDS now has free VIN decode, IDS Pay, VINRV; Lightspeed has Service Scheduler explicitly targeting RECT, Service Tech Video, AI insights. The original points stand but the gap is narrowing on convenience features — widening on structured-data features. |
| `RVS_PRD.md` FR-003 (VIN scan) | Reframe internally as "parity feature," not differentiator. Sales materials lead with intake + Section 10A, not VIN scan. |
| `RVS_PRD.md` FR-013 (SFTP export) | Promote to **Critical** priority (currently High). This is a load-bearing piece of your *coexistence* story. |
| `RVS_data_moat.md` | Add a paragraph: cross-dealer analytics requires explicit per-tenant ToS consent. Verify the consent language exists. |
| `RVS_Context.md` Section 13.1 | Soften *"No competitor has tackled service intake at this level"* — replace with "DMS systems own the transaction; Kenect owns the conversation; RVS owns the structured intake-and-outcome data layer between them." |

---

## The One Thing That Should Worry You

Not IDS or Lightspeed today. **The risk is one of them deciding in 2027 to ship a "service intake module" to their existing 1,200 / 4,500 dealer customers** — even a mediocre one.

If they do, distribution wins. Their version will be worse than yours, but it'll be free or low-cost as part of an existing seat license, and it'll feed straight into the DMS the dealer already trusts.

**Your only defenses:**
- **The asset ledger filling fast** with structured cross-dealer data (architecturally hard for them to retrofit because each dealer's DMS is siloed by design)
- **OEM relationships for failure analytics** (still open — neither DMS has done this)
- **Becoming a Technology Partner inside both DMSs before they decide to compete with you** (turns the threat into a distribution channel)

Get partner status. Fill the ledger. Lock in one OEM. The race is real but you have a 12–24 month window. Don't waste it.
