# RVS — Archive Index

**Version:** 1.0 · September 4, 2026

Eleven core documents were superseded on September 4, 2026 when the product scope was reduced to intake, packet delivery, and a thin manager app. **Nothing was deleted.**

The archive lives in [`ARCHIVE/`](ARCHIVE/), split into `ARCHIVE/ASOT/` (product and technical) and `ARCHIVE/Marketing/`. It holds 84 files in total — the eleven below plus the supporting set in the second table. Files are unmodified copies and carry no archived banner, so read the date in each header and treat it as a snapshot of what was believed then, not a current statement of fact.

This index exists so you can find things without rereading ~9,000 lines, and so retrieval is a decision rather than a drift.

---

## The eleven

| Document | What it holds | Pull it back when |
|---|---|---|
| `ARCHIVE/ASOT/RVS_Technical_PRD.md` | The full technical contract: Cosmos data model and partition keys, middleware order, per-endpoint API shapes, security requirements, AI service abstractions, the DMS integration analysis (§10.7), and the GAP-nn register. **The densest and most reusable document in the archive.** | You need a data model or endpoint decision the new `RVS_Spec.md` doesn't cover. Check here before inventing one. |
| `ARCHIVE/ASOT/RVS_PRD.md` | Solo and Professional tier requirements, personas, the record of what was cut in earlier versions and why. | You're adding a feature back and want to know if it was already specced. |
| `ARCHIVE/ASOT/RVS_Premium_PRD.md` | Enterprise requirements: SAML/SCIM, audit log, warranty leakage analytics, OEM revenue share, custom SLA, solutions-engineer commitment. | A dealer group asks for SSO, an audit log, or contractual SLAs — i.e., a real enterprise procurement conversation, not a pilot. |
| `ARCHIVE/Marketing/RVS_data_moat.md` | Ledger architecture, the five-vocabulary taxonomy design, anonymization pipeline, k-anonymity, ToS license language. | **Two parts are still live and were copied forward into `RVS_Spec.md` X-2 and X-3.** Pull the rest when there's enough data volume to make aggregation meaningful — realistically, dozens of paying locations. |
| `ARCHIVE/Marketing/RVS_Competitive_Strategy.md` | Positioning against Kenect, ServiceNomad, QuoteIQ, IDS, Lightspeed. The four-tier pricing model. The Yes/No scope filter. Sales objection handling. | A competitive conversation. **Not pricing** — Q6 closed on September 6, 2026 with a single per-location price and no tiers; the four-tier model here priced a product that no longer exists, and reading it is how tiers get reinvented. The Yes/No filter was the most useful thing here — the mechanism that kept scope from drifting. It has since been **replaced**, not ported: see "The scope filter" in `RVS_Plan.md`. Its four questions rested on the dataset thesis and the four-tier model, both archived. Read the original if you want the reasoning behind having a filter at all. |
| `ARCHIVE/ASOT/RVS_Implementation_Plan_v2.md` | 25-sprint map, runway math, hire plan, risk register. | You need the financial model or the risk register. The sprint map is obsolete. |
| `ARCHIVE/Marketing/RVS_vs_DMS_value_prop.md` | The coexistence argument, and an honest accounting of what IDS and Lightspeed shipped in 2025. | A dealer asks "doesn't my DMS already do this?" §3.1 and §3.4 are the arguments that still hold. |
| `ARCHIVE/Marketing/RVS_OEM_GoToMarket.md` | OEM licensing strategy, target accounts, deal structures. | Far future. Only meaningful with a dataset behind it. |
| `ARCHIVE/ASOT/RVS_Context.md` | Platform overview and four-tier model as of the old scope. | Mostly superseded by `RVS_Overview.md`. Historical. |
| `ARCHIVE/Marketing/RVS_MultiIndustry_Expansion.md` | Marine, heavy equipment, agricultural expansion. | Was already deferred indefinitely before this reset. |
| `ARCHIVE/Marketing/RVS_v34_Pricing_Summary.pdf` | Pricing summary matching the old four-tier model. | Superseded. The tiers priced a product that no longer exists. |

Also archived by implication: the decision record and document index produced in the prior session, and the FR-nnn / GAP-nn / OQ-nn identifier series. New work uses the `A-n / B-n / C-n / X-n` and `Q-n` series in `RVS_Spec.md` and `RVS_Plan.md`.

---

## The supporting set

The other 73 files. Most were superseded by the five technical documents in `ASOT/`, which describe what is actually built rather than what was planned.

| Where | What it holds | Superseded by |
|---|---|---|
| `ARCHIVE/ASOT/Architecture/RVS_Consolidated_Architecture.md` | Long-form backend architecture — domain model, orchestration, RU analysis, change feed | `ASOT/RVS_Architecture.md` |
| `ARCHIVE/ASOT/Architecture/RVS_Azure_Infrastructure_Architecture.md`, `ARCHIVE/ASOT/Infra/Revised_Arch.md`, `ARCHIVE/ASOT/Infra/Bicep.Planning/INFRA.dev-environment.md` | Infrastructure planning and dev-environment setup | `ASOT/RVS_Infrastructure.md`, which is derived from the Bicep itself |
| `ARCHIVE/ASOT/Architecture/RVS_SMS_Notification_Architecture.md` | Two-way SMS design | Archived capability. Outbound code still exists |
| `ARCHIVE/ASOT/Architecture/RVS_Billing_Metering_Architecture.md` | Usage metering for the tiered model | Deferred to build item 7. The metering design may outlive the tiers |
| `ARCHIVE/ASOT/Auth0/RVS_Auth0_Identity_Version2.md` | Full identity model including technician roles | `ASOT/RVS_Identity.md` |
| `ARCHIVE/ASOT/RVS_Cosmosdb_data_model.md`, `ARCHIVE/ASOT/RVS_Cosmosdb_requirements.md` | Planned container and document design | `ASOT/RVS_DataModel.md` |
| `ARCHIVE/ASOT/FrontEnd/Intake/*`, `ARCHIVE/ASOT/FrontEnd/Manager/*`, `ARCHIVE/ASOT/FrontEnd/RVS_FrontEnd_Solution.md` | Per-app feature specs, Blazor lifecycle notes, the VIN lookup decision tree, shadow customer profiles | `ASOT/RVS_FrontEnd.md`. The Manager spec describes the board, analytics and batch-outcome screens that are now descope targets |
| `ARCHIVE/ASOT/FrontEnd/Techs/*` | The technician mobile app — features, killer-feature analysis, sideloading | Archived. **This is the most likely "where did that go?" lookup** |
| `ARCHIVE/ASOT/AI/*` | AI architecture blueprint plus issue-level plans (#227, #232) and the Wave 1 backlog | The blueprint's interface and orchestration patterns shaped the code that exists; its MAUI half is archived |
| `ARCHIVE/ASOT/RVS_implementation_plan.md` | The v1 sprint plan | `RVS_Plan.md` |
| `ARCHIVE/Marketing/RVS_vs_DMS_OnePager.md`, `ARCHIVE/Marketing/RVS_vs_Kenect_OnePager.md` | Competitor one-pagers | Already declared superseded by `ARCHIVE/Marketing/RVS_Competitive_Strategy.md` before this reset |
| `ARCHIVE/Marketing/RVS_Design_Partners.md`, `ARCHIVE/Marketing/RVS_Handling Objections.md`, `ARCHIVE/Marketing/RVS_Initial Marketing Plan.md` | Local prospect research, objection handling, ICP and design-partner motion | `Marketing/RVS_GoToMarket.md` and `Marketing/RVS_Objections.md` |
| `ARCHIVE/Marketing/RVS_Why_Enter_Outcomes.md` | The leadership case for technician outcome entry | Archived with the technician workflow |
| `ARCHIVE/Marketing/RVS_30day MVP.md` | A 12-feature 30-day MVP plan | Mostly built already; build order now lives in `RVS_Plan.md` |
| `ARCHIVE/Marketing/RVS_Arch_Overview.md`, `ARCHIVE/Marketing/RVS_Product_Architecture.md` | Architecture summaries that lived in the marketing folder | `ASOT/RVS_Architecture.md` |
| `ARCHIVE/ASOT/pricing`, `ARCHIVE/ASOT/scratch.md`, `ARCHIVE/ASOT/AI/AI_Manager_Capabilities.md`, `ARCHIVE/ASOT/Architecture/RVS_Stamp_Scaleout.md` | Empty or scratch files | — |

Two files exist in both the archive and the live tree because they are still current: `ASOT/Auth0/Add metadata to accessToken.js` and `ASOT/Auth0/Auth0-Portal-Configuration-Checklist.md`. Edit the live copies under `ASOT/`. The same is true of `Marketing/Dealerships/LazyDays_Repair.md`, which is field research rather than strategy.

### Two cautions

**`ARCHIVE/ASOT/Infra/Bicep.IaC/` is a full 32-file snapshot of the infrastructure code.** The live, maintained copy is `ASOT/Infra/Bicep.IaC/`. Editing or deploying the archived one will silently do the wrong thing.

**`ARCHIVE/ASOT/RVS_MagicLink_Storage_Guidance.md` is not dormant — it is in live conflict with the Spec.** It decided that magic-link tokens are stored **unhashed**, and argued the case explicitly. Spec X-5 now requires tokens to be stored hashed. The Spec supersedes it, but the code still implements the archived decision, and C-7's one-click status links are planned on the same machinery. This is open question Q7 in `RVS_Plan.md`. Read the archived document for the threat-model reasoning before overturning it.

---

## What carried forward

Only four things survived the reset into the active set:

1. **The intake flow** — anonymous form, VIN decode, AI questions, attachments, returning-customer prefill.
2. **The ledger write** — kept because it's already built and free to keep, not because anything reads it.
3. **The anonymization license language** — kept because it's the one thing that gets more expensive to add every time a customer signs without it.
4. **The tenancy model** — claims service and access-gate middleware, unchanged.

Everything else in the archive is dormant — with the two exceptions flagged above: the Bicep snapshot, which is a live footgun rather than a document, and the magic-link storage decision, which the code still implements and the Spec now contradicts.

---

## Retrieval rules

**Retrieval is a decision, logged in `RVS_Plan.md` with a date and a reason.** Not a thing that happens because a prospect mentioned a feature.

Before pulling anything back, three questions:

1. Is a customer paying for this, or asking for it as a condition of paying? Interest is not a trigger.
2. Does it fit the current shape — a form that produces an artifact — or does it turn RVS back into an operations platform?
3. If it comes back, what comes out? The document set got heavy one reasonable addition at a time.

**The likeliest retrieval, and the one to be most skeptical of, is DMS integration.** It will come up in every dealer conversation. The paste block was built specifically so the answer can be "here's what we do instead" rather than "it's on the roadmap." Don't reopen it because someone asked; reopen it when someone won't sign without it and the deal is worth two quarters of partner certification.

---

*Prior document set archived September 4, 2026. Superseded by `RVS_Overview.md`, `RVS_Spec.md`, `RVS_Plan.md` and this file, plus the five technical documents in `ASOT/`.*
