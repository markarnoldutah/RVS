# RVS — Plan

**Version:** 1.6 · September 11, 2026

---

## Build order

### The eight items (stable reference — other docs cite these numbers)

| # | Work | Rough size |
|---|---|---|
| **1** | **Packet: composition, HTML + print stylesheet, PDF** | 1–2 sprints |
| **2** | **Email delivery: ACS, recipients config, photo attachments, paste block, retry/bounce** | 1 sprint |
| **3** | Customer status page + status token | ~1 sprint |
| **4** | Manager app: list, detail, status, disposition, resend, settings | 1–2 sprints |
| **5** | Persistent manager-app session + deep links from the packet email (C-7, issue #498) | Folds into item 4 as a sub-issue of #420 — a session/PWA build, not the original days-long token-reuse estimate |
| **6** | Issue-category vocabulary + per-category fallback diagnostic questions | Domain time, parallel, not engineering-blocked |
| **7** | Stripe billing + trial | 2 sprints — **do not start until three shops are running.** The first five customers get a hand-sent invoice and a Stripe payment link. Two sprints off the critical path, and you learn what to build |
| **8** | Descope: delete archived-scope code — analytics, Kanban board, batch-outcome, SMS, technician/scheduling fields, `build-mobile.yml` | ~1 sprint, can run in parallel |

### Sequenced for the first mobile technician (operative plan)

The target is **one mobile technician live on a paid 30-day pilot in the fewest working days**. He never opens the manager app and has no DMS, so build item 4 and the DMS-verification half of item 2 come off the critical path entirely. Items 1–3 are substantially shipped already (see the merged issues on `main`); what is left is the packet reading as expert, landing by email every time, and deploying to production.

**Critical path — nothing here is parallelisable away, roughly in order:**

| Step | Work | Issues | Why it gates the first tech |
|---|---|---|---|
| 1 | Packet layout + masthead + PDF photo render | #492 (items 1–6, 8) | A packet with an "RV ServiceFlow" masthead or blank photo cells is not shippable. #492 shipped the seams and stopped HEIC from failing the whole render — see step 2. **Defer** the follow-ups it split off: #505 (per-location branding UI), #506 (packet local-time), #507 (structured-assessment spike). |
| 2 | Transcode HEIC/HEIF uploads to JPEG server-side | #508 | Confirmed root cause of "photos don't appear": iPhone uploads are HEIC, which QuestPDF's decoder and most mail clients can't render. #492's mitigation shows a placeholder instead of crashing; the photo still doesn't show until this lands. |
| 3 | Finalise the issue-category vocabulary | #452 | The packet's most differentiating section. Domain work — start day one, alongside everything. |
| 4 | Per-category fallback diagnostic questions | #453 | The cheapest quality lever in the product; makes the packet read as expert with the AI switched off. Needs #452. |
| 5 | Retire the four technician-side vocabularies | #454 | Removes the "Not yet classified" fields from the packet. Small, and it improves what lands in the inbox. |
| 6 | Hide the AI summary + diagnostics from the customer at intake | #483 | The tech gets the assessment; the customer should not be shown a machine guess about their rig. |
| 7 | Production infra: reachable Azure OpenAI, real parameter values | #463, #466 | The Bicep as written cannot reach Azure OpenAI in prod and ships placeholder values. Hard deploy blockers. |
| 8 | Whisper behind a flag; tenant access gate proven end to end | #467, #465 | #467 makes the AI spend per-environment and reversible; #465 proves the disabled-tenant path before a real tenant depends on it. |
| 9 | Packet-email reliability: bounce wiring + LogCritical alert | #497, #494 | The whole model is "runs on email alone." A silently dropped packet email is the one unrecoverable failure. |
| 10 | Customer confirmation email layout; voice-recording indicator | #496, #479 | Customer-facing polish on the intake path. Small, do last. |

Non-code blocker, unchanged: the one-page, phone-signable design-partner agreement carrying the X-3 anonymisation licence (decision log, Sep 6 2026). It gates go-live alongside the steps above.

**Runs fully in parallel — not gated by the critical path, does not gate go-live:**

- Descope (epic #423 → #455–#461): analytics, Kanban, batch-outcome, SMS, scheduling/assignment fields, `build-mobile.yml`. ~1 sprint, start any time.
- Cosmos client-config reconcile (#477).

**Deferred until the first mobile tech is live:**

| Work | Issues | Trigger to start |
|---|---|---|
| Manager app to thin scope (build item 4) | epic #420 → #443–#448, plus #498, #468, #470 | The dealer-group pitch. The mobile tech does not use it. Build item 5 (C-7) no longer stands apart from this row — #498 folded it in; see Spec C-7 and the Sep 11 2026 decision log entry. |
| Per-location packet branding UI (brand name + logo) | #505 | Needs the manager location-config screen (#470 territory). #492 already ships the default "RV Intake" masthead. |
| Stripe billing + trial (build item 7) | epic #425, #478 | Three shops running. First five customers get a hand-sent invoice + Stripe link. |
| Packet "Received" line in dealership-local time | #506 | Cosmetic; falls back to a `… UTC` string until a Location timezone field exists. |
| Structured preliminary assessment (probable cause / fix / parts) | #507 | Spike, then its own issue(s). Architecture where the #452/#453 craft work is cheaper first (scope filter #3). |
| Intake dealer-capability search when no slug is supplied | #471 | Explicitly future state. |

Work deferred on a condition *other* than "first tech is live" — including potential work that has no issue yet — is in the **Future-state register** below.

Steps 3–5 (the issue-category and question-bank work, build item 6) run alongside everything and are barely engineering — mostly domain judgement. Don't let them slip to the end; the fallback question bank is what makes the packet read as expert rather than generic, and it's the cheapest quality lever in the product.

## Ship criteria

- A customer completes intake on a phone and the service manager has a readable email with photos and a printable PDF within a minute.
- The PDF prints legibly on a shop printer in greyscale.
- The paste block goes into a real DMS complaint field without reformatting. **Verify against an actual DMS, not a mock.** — *not a gate for the first mobile technician (he has no DMS); it becomes a gate before the dealer-group pilot.*
- The customer can check status from the link without logging in.
- A service manager can run a full week acting on packet-email deep links alone — no interactive login, no proactive visit to the app as a workspace.

That last one is the real test.

---

## The scope filter

The archived strategy carried a Yes/No filter that kept scope honest for a year. Its four questions were built on the dataset thesis and the four-tier pricing model, and both are gone — so this is a replacement, not a port. It exists for the same reason the original did: the last document set got heavy one reasonable addition at a time, and so did the product.

**Every proposed feature answers all four. A "no" on 1 or 2 is disqualifying on its own.**

1. **Does it make the packet better?** The packet is the product; everything else exists to produce it. A change that improves what lands in the service manager's inbox is presumptively worth doing. A change that adds a surface somewhere else is presumptively not.

2. **Can a shop still run a full week on email alone?** If a feature only pays off when someone opens the manager app daily, it breaks the one claim that differentiates RVS. Both prospects said *"not another dashboard."* Anything that quietly makes the app mandatory fails, however useful it looks in isolation.

3. **Is it craft, or is it architecture?** The durable advantage is the quality of the diagnostic questions, the fit of the categories to real RV failures, and how the PDF looks on a shop counter. That is craft. A week on the question bank beats a week on a subsystem. If a proposal is architecture, it needs a reason the craft work can't go first.

4. **What comes out?** Nothing goes in for free. Name the thing being dropped, deferred or deleted — the descope backlog in build item 8 is the obvious donor. If the honest answer is "nothing," the answer to the feature is no.

### Worked examples

| Proposal | Verdict | Why |
|---|---|---|
| Per-category fallback diagnostic questions (item 6) | **Pass** | Strong 1 and 3. The cheapest quality lever in the product |
| Persistent-session deep links from the packet email (C-7) | **Pass** | Strong 1 and 2 — it is the feature that keeps "no dashboard" true without a daily interactive login |
| Per-location paste-block cap (B-6) | **Pass** | 1. Real DMS complaint fields truncate at different lengths |
| Resend the packet (C-5) | **Pass** | 1, and already specced |
| A dashboard, of any kind | **Fail** | 2, outright. This is the objection the whole design answers |
| Two-way SMS | **Fail** | 1 — it does not improve the packet — and 2, because the inbox becomes a place you have to visit |
| Appointment scheduling | **Fail** | 1 and 2. Also explicitly out of scope in the Spec |
| DMS API integration | **Fail** | Hardest on 4: three to six months of partner certification and nothing comes out. Retrieval trigger is in `RVS_Archive_Index.md` |
| A second AI enrichment pass over the description | **Fail** | 3. Architecture where craft is cheaper and better |
| Per-location configurable status vocabularies (Q5) | **Fail** | 3 and 4. A known complexity sink; a fixed set serves both a solo tech and a dealership. Decided — issue #428 |

**A prospect asking for something is not an answer to any of the four.** Neither is a competitor having it. Retrieving archived capability is governed separately, in `RVS_Archive_Index.md`, and lands in the decision log below either way.

---

## Open questions

| # | Question | Owner | Needed by |
|---|---|---|---|
| ~~**Q1**~~ | ~~PDF rendering library and license.~~ **Resolved (issue #426): QuestPDF.** Native .NET renderer, no headless browser. Community License v3.0 is free under USD 1M revenue (RVS qualifies); paid tier is perpetual USD 1,999 / 4,999 if the threshold is crossed. See decision log and Spec B-7. | Engineering | ~~Before build item 1~~ Done |
| ~~**Q2**~~ | ~~Build one-click status links (C-7) now or after the manager app?~~ **Resolved (issue #498): built alongside the manager app, as part of it — not the standalone anonymous-token design this question assumed.** That design (cheap, reuses the status-token machinery) turned out to collide with corporate mail-security link-prescanning and left an anonymous write surface; #498 replaces it with a persistent manager-app session + deep links, load-bearing for #420's AC rather than a cheap add-on after it. See decision log and Spec C-7. | Product | ~~Before build item 4~~ Done |
| ~~**Q3**~~ | ~~Does the mobile tech's "buy in" mean a subscription at ~$39/mo, or equity/partnership?~~ **Resolved, then closed for good (Sep 9 2026): subscription, never equity — and no founding-partner term.** He gets the same 30-day free trial as everyone else, then $39/mo locked for life. Upside, if he wants it, is still a named referral fee — not shares. See decision log and `Marketing/RVS_GoToMarket.md`. | GTM | ~~Next conversation with him~~ Done |
| **Q4** | At the dealer group: who owns the location service pages — marketing, IT, or an agency? **Downgraded from a gate to a second-conversation item.** The pilot is built to require nothing the service manager cannot authorize alone — counter QR code, advisor email signatures, the existing callback autoresponder — so the web-page change is the expansion ask, not the entry ask. Still worth knowing; no longer blocks a pitch. | GTM | ~~Before pitching~~ Before the expansion ask |
| ~~**Q5**~~ | ~~Status vocabulary — is the C-3 set right for a mobile tech *and* a dealership, or does it need to be per-location configurable?~~ **Resolved (issue #428): one fixed set, adopted from the code** — `New / InProgress / WaitingOnParts / WaitingOnCustomer / Completed / Cancelled`. Per-location configurable vocabularies rejected. See decision log and Spec C-8. | Product | ~~Before build item 4~~ Done |
| ~~**Q6**~~ | ~~Pricing at this reduced scope.~~ **Resolved: one product, per location, no tiers.** $39/mo mobile; $79/location, banding to $69 at 3–9 and $59 at 10+; two months free on annual prepay; 300 requests/location/month fair use. Pilot is 30 days free at a named price, not open-ended free (shortened from 60 days, Sep 9 2026). Per-packet pricing, free-intake/paid-packet, and charging the RV owner are all rejected. See decision log and `Marketing/RVS_Positioning.md`. | GTM | ~~Before build item 7~~ Done |
| ~~**Q7**~~ | ~~Token model.~~ **Resolved (issue #427): X-5 as written, with split scope.** SHA-256-hashed storage (raw token never persisted). Status token stays **per-customer** (TTL ≤ 30 days, sliding renewal); C-7 action links are **per-request and per-action** (single-purpose, TTL ≤ 14 days or single-use); one shared token helper. Prior unhashed ARCHIVE decision overturned — its own stated trigger, a write-capable token, is met by C-7. Migration backfills hashes pre-GA then drops the plaintext field. See decision log and Spec X-5; implementation in #440 / #441. | Engineering | ~~Before build item 3~~ Done |
| ~~**Q8**~~ | ~~Voice transcription (Whisper) and VIN photo extraction (gpt-4o vision) are fully built into intake steps 3 and 5 but appear nowhere in the Spec.~~ **Resolved (issue #429): all four in scope.** Voice input, VIN-from-photo, issue insights, and the capability pre-check are added to the Spec as A-9–A-12. Nothing archived; no descope sub-issue on #423. Whisper and gpt-4o both stay — the `deployWhisper` / gpt-4o flag work (#467) still proceeds so the spend is per-environment and reversible, defaulting on. See decision log and Spec A-9–A-12. | Product | ~~Before the next infra deploy~~ Done |
| ~~**Q9**~~ | ~~Spec X-1 says the customer status page has "no conversation, no messaging, no file exchange". Can the manager leave free-text notes that show on that page?~~ **Resolved (issue #500): yes.** The X-1 restriction was never a deliberate decision and is removed — the status page may show whatever is useful to the customer, and the manager can author notes that render there (new Spec C-9). Hard constraint retained: the page is display-only for the customer, with no path to send anything back — one-directional manager → customer. See decision log and Spec X-1 / C-9. | Product | ~~Before #500~~ Done |

---

## Future-state register

Potential work that is **deliberately not being done now**, each row carrying an explicit condition for reconsidering it. This is the home for "we thought about X — here is why we parked it, and here is what would make us look again." It is not a roadmap and not a wishlist: a row earns its place only by naming a trigger.

It complements what this document already tracks. The **"Deferred until the first mobile tech is live"** table covers work gated on that one event. This register covers work gated on a *different* condition — a metric crossing a threshold, a dependency shipping, a date, a repeated ask — and, unlike that table, it also holds items that do not yet have a GitHub issue.

**Trigger types** — every row names one; a second is optional:

| Type | Fires when |
|---|---|
| **Event** | A named thing ships or happens — "manager app (#420) ships", "first dealer-group pilot signed" |
| **Signal** | A recurring observation crosses a threshold — "≥ 2 pilots ask for it", "packet-email hard-bounce rate > 0 over any 7-day window". Tie it to an existing alert (#494) or the metrics ledger (#528); do not invent a mechanism |
| **Threshold** | A scale or performance number — "tenant access gate read p95 > ~40 ms sustained", "Cosmos RU/month > X" |
| **Dependency** | Another piece of work lands and a condition still holds — "#498 shipped and 'open app → board' still reported as friction" |
| **Date** | A calendar point — "RVDA Convention, Nov 2026", "annual infra review" |

**Rules:**

- **Adding a row** is subject to scope-filter question 4 — name what it trades against — and requires a written trigger and a "lands as". No trigger, no row.
- **When a trigger fires**, the item graduates: file the issue (or a Spec change plus an issue), then strike the row and link it, exactly as the resolved `Q-n` entries above are struck. Rows leave the register; it does not grow without bound.
- **Killing a row** is allowed too — strike it as *Rejected (decision-log YYYY-MM-DD)* when a decision is taken not to do it. The "we considered it" record stays.
- **Review cadence:** scan this table at each plan phase boundary (P0 → P1 → P2) and whenever a parent Feature (`type:feature`) closes.

| ID | Item | Deferred because | Revisit trigger | Origin | Lands as |
|---|---|---|---|---|---|
| **FS-1** | Cosmos dedicated gateway + integrated cache | MVP request volume needs no read cache; an in-memory cache (archived `O-4`) is the cheaper first move if one is ever needed | **Threshold:** tenant access gate read p95 > ~40 ms sustained, **or** a second hot Cosmos read path appears | #477 | #557 — filed, parked pending the trigger |
| **FS-2** | `rv-warranty-rules` → warranty / likely-parts hints in the assessment | The container is seeded but has no repository and is a descope candidate; the value is unproven | **Dependency:** the #507 structured-assessment spike concludes warranty data materially improves the packet **and** the container is kept | #507 | Spec requirement (A-n) + API issue |
| **FS-3** | Hardened anonymous C-7 deep-links — `GET` landing → `POST` confirm, idempotent transitions, scanner tagging, warmed action domain | Authenticated deep-links from #498 may remove the friction on their own | **Dependency:** #498 shipped **and** "open app → board" still reported as friction by ≥ 1 pilot | #498 (old Q2) | Engineering issue |
| **FS-4** | Competitor-referral / network-effects behaviour on a disabled or no-match intake slug | Out of scope, and an open legal question | **Event:** a deliberate decision to pursue network effects as a product feature | #478 | `type:decision` issue + legal check |
| **FS-5** | Manager UI to upload per-location branding (logo + display name) | The backend (#505) is not yet landed and branding-in-pilot-scope is not yet decided (#470) | **Dependency:** #505 backend merged **and** #470 decides branding is in pilot scope | #505 / #470 | Fold into #470, or a new manager issue |
| **FS-6** | Alert coverage for `EventId` 438001 / 434001 in the packet pipeline | May already be covered by #494's alert mechanism | **Event:** #494 closes without covering both event IDs | #494 | Extend #494, or a new observability issue |
| **FS-7** | In-room deliverability check ritual for pilots after the first | Only matters once a second pilot is onboarding | **Event:** second pilot onboarding begins | #532 | Pilot-recruitment issue under #523 |
| **FS-8** | Blue Compass Hurricane as a pilot or enterprise account | No X-3 signing authority at a 100-location PE-backed company, no price band that fits, and a corporate no is expensive and durable (decision log Sep 11 2026, closes #530) | **Event:** Phase P1 (Jay Lyons pilot, #525) closes, opening the 30-minute observation visit | #526 | Pilot-recruitment issue under #523, or a `type:decision` issue if authority/pricing changes |

---

## Pitches

**Mobile tech — lead with wasted trips, not with data.** He drives out, the failure is a different component than the phone call suggested, and he eats the trip: call it $100–200 in time and fuel. Structured intake with photos and a decoded VIN before he leaves means the right part is on the truck. He never logs into anything; the write-up hits his email.

**Dealer group — lead with their own web form.** Their location pages collect a name and a callback request. Replace that with structured triage arriving in the existing service inbox as a one-page write-up. No new logins, no DMS project, no IT involvement. Ask for a two-store pilot. **Ask a service manager, not a CIO.**

Honest note on both: one interested mobile technician is a design partner and a reference, not market validation. Treat him as a source of truth about workflow and as someone who'll take a call from a prospect — not as evidence the segment buys.

---

## Decision log

| Date | Decision | Why |
|---|---|---|
| Sep 4 2026 | **Archived the prior 11-document set; rebuilt as four documents** | The document set had outgrown the product. Version drift across files, colliding identifier series, and specs for capability years from being built. *(Four was the outcome of that consolidation, not a constraint. It was later mistaken for one — see Sep 7 2026.)* |
| Sep 4 2026 | **Scope reduced to intake + packet + thin manager** | Both prospects objected to "another dashboard." An artifact delivered to an inbox answers that objection for both without a DMS integration. |
| Sep 4 2026 | **No DMS integration** | The one API examined publicly exposes customer and unit data, not a work-order write path, and partner certification runs 3–6 months with a per-dealer static IP requirement. A paste block delivers most of the value now. |
| Sep 4 2026 | **Four of five controlled vocabularies archived; `issue-category` only** | The others were technician-side and were never populated at intake. Keeping them meant shipping a packet with four "Not yet classified" fields in its most differentiating section. |
| Sep 4 2026 | **Offline mobile app dropped** | The wedge is customer-side intake. A one-person operator needs to *receive* structured work, not run a field app. Revisit only on a demonstrated dead-zone requirement. |
| Sep 4 2026 | **Ledger write and anonymization license retained** | The only two pieces of the data strategy that are cheap now and expensive-to-impossible to retrofit. Everything downstream is archived. |
| Sep 4 2026 | **Replaced the Yes/No scope filter rather than porting it** | The archived filter's four questions all rested on the dataset thesis or the four-tier pricing model. Both are gone, so every question would have passed vacuously. The new filter tests the packet, the no-dashboard claim, craft-over-architecture, and what comes out. |
| Sep 5 2026 | **Status vocabulary: one fixed set, adopted from the code** (issue #428, closes Q5) | Code implements `New / InProgress / WaitingOnParts / WaitingOnCustomer / Completed / Cancelled` in `StatusTransitions`; the Spec suggested a different, un-built `New → Received → In Progress → Ready → Closed` plus `Cancelled`. The built set already works for both audiences — `WaitingOnParts` / `WaitingOnCustomer` are the real holds, and closing without work is C-4 disposition. Aligning the Spec to the code costs nothing; rebuilding the code to the Spec's guess costs a migration for no packet gain. Per-location configurable vocabularies rejected: a config surface, per-tenant migration, and location-varying customer-status copy, none of which makes the packet better. Spec C-3 / C-8 updated to match. |
| Sep 5 2026 | **Anonymous token model: X-5 as written, split scope** (issue #427, closes Q7) | Spec X-5 already mandates hashed, TTL-bounded, ≥128-bit tokens; the built per-customer magic link is unhashed and 90-day. Hashing costs ~4 lines — the lookup is already cross-partition on `/email`, so indexing a hash column is the same RU — and removes plaintext tokens from the container, backups, and diagnostic exports. C-7's one-click links perform an anonymous *write* on the same machinery, which is the exact trigger the prior ARCHIVE decision (`RVS_MagicLink_Storage_Guidance.md`) named for reconsidering unhashed storage; that decision is overturned. Scope is split rather than forced to one: the read-only status token stays per-customer (identity-scoped, matches `GlobalCustomerAcct`; TTL cut to ≤ 30 days with sliding renewal), C-7 links are per-request/per-action and single-purpose. Migration is cheap pre-GA — backfill hashes from the current plaintext, then drop the `magicLinkToken` field (#441). No Spec rewrite; X-5 clarified, ASOT gap tables updated. |
| Sep 5 2026 | **Voice and vision AI: all four capabilities in scope** (issue #429, closes Q8) | `ai/transcribe-issue` (Whisper), `ai/extract-vin` (gpt-4o vision), `ai/suggest-insights`, and `assess-capabilities` are fully built into intake steps 3 and 5 and were the last unspecced parts of a "substantially built" app. They pass the scope filter: each makes the packet better or the form easier without new architecture — dictation lowers the barrier to a good free-text description (the packet's most differentiating section), VIN-from-photo raises decode hit-rate, insights and the capability pre-check are advisory. Every one already has a rule-based or no-op fallback and none blocks submission, so the marginal cost is the two Azure OpenAI accounts, not new code. Archiving them would mean deleting working, wired-in code and a step-3/step-5 redesign for no gain. Specced as A-9–A-12; nothing archived, so no descope sub-issue on #423. The Whisper and gpt-4o accounts stay, but #467 still puts them behind a `deployWhisper` (and gpt-4o) flag, defaulted on, so the infra spend is per-environment and reversible. |
| Sep 5 2026 | **PDF rendering: QuestPDF** (issue #426, closes Q1) | Headless Chromium (PuppeteerSharp/Playwright) fails B-7 — a browser process on Linux App Service B1 that can't be reliably health-checked or recycled, plus a ~300 MB engine the base image can't support. QuestPDF renders in-process with a bundled native lib, no network call per render, sub-100 ms per page. Community License v3.0 is free under USD 1M annual revenue (RVS qualifies now; commercial use is permitted); above that, 90 days to buy a perpetual Professional (USD 1,999) or Enterprise (USD 4,999) licence — bounded and deferrable. Publicly traded / public-sector exclusions don't apply. Fallback: PDFsharp/MigraDoc (MIT).
| Sep 6 2026 | **Pricing: one product, per location, no tiers** (closes Q6) | $39/mo mobile, $79/location banding to $69 (3–9) and $59 (10+), two months free annual, 300 requests/loc/mo fair use. **No tiers**, because nothing here is gateable without breaking the scope filter — gating the PDF or photos makes the packet worse (filter #1), and gating the manager app charges for the thing we tell people they'll never open. The archived four-tier model gated benchmarking, SSO and a technician app, none of which exist. **Per location** because that is already the product's boundary (`{locationSlug}`, B-6 config, recipient list), so a group grows without a second pricing conversation. **$79 rather than $199** is a sales-velocity decision, explicitly not a valuation: the motion is *ask a service manager, never a CIO*, and two stores at $79 is $158 — discretionary spend, not procurement. Against the functionality (A-9–A-12, decode, PDF, paste block, status links, per-submission Azure OpenAI cost) $79 is low, and that gap is the first thing to revisit once three shops are running. Rejected: per-packet/usage pricing (suppresses link promotion, which is exactly the behaviour early volume and the X-2 ledger need; natural second model, not first), free-intake/paid-packet (the packet *is* the product), charging the RV owner. |
| Sep 6 2026 | **Pilot is 60 days free at a named price, not open-ended free** (Q6) | Open-ended free produces polite non-adoption and no signal: a yes costs the buyer nothing, so it tells us nothing, and a free thing has no anchor to renew against. The rule across all three Marketing docs inverts from *never quote a number* to **quote the price, discount the pilot**. Consequence: the design-partner agreement carrying the X-3 anonymization license must be one page and e-signable from a phone, because closing by live configuration means packets can flow before anything is signed. The agreement gates go-live and is now a blocking item alongside build items 1–2. |
| Sep 6 2026 | **Mobile tech: subscription, never equity** (closes Q3) | Six months free, then $39/mo locked for life. Equity trades a percentage of the company for advice worth a few thousand dollars; converts a customer into an owner, which destroys the value as evidence (*"a shop that pays"* is a proof point, *"my partner likes it"* is not); puts a name on the cap table before there is anything to raise against; and cannot be undone. Upside, if wanted, is a named referral fee — costs nothing until it works. |
| Sep 6 2026 | **Q4 downgraded from a gate to a second-conversation item** | The pilot is built to require nothing the service manager cannot authorize alone: counter QR code, advisor email signatures, and the service department's existing callback autoresponder. Replacing the location web form becomes the expansion ask, made once packets are landing. Who owns the pages is still worth knowing; it no longer holds up a pitch. |
| Sep 6 2026 | **Stripe billing deferred until three shops are running** (build item 7) | The first five customers get a hand-sent invoice and a Stripe payment link. Takes two sprints off the critical path, and the billing model gets built against observed behaviour instead of a guess. The month's request count goes on the invoice line item — that is also the cheapest available answer to the renewal risk below. |
| Sep 7 2026 | **Added `RVS_Money.md` to the canon** | Unit economics and cost structure have no other home: not product scope (`RVS_Spec.md`), not build order (this file), not a sales argument (`Marketing/`), and the archived model in `ARCHIVE/ASOT/RVS_Implementation_Plan_v2.md` §7 priced the four-tier product. Folding it here would have doubled this file with material nobody reads while deciding what to build next. Registered in `CLAUDE.md`, `RVS_Overview.md` and `.github/copilot-instructions.md` so it cannot drift unnoticed. |
| Sep 7 2026 | **Removed the document-count rule from canon** | Origin was the Sep 4 2026 entry below — *"rebuilt as four documents"* — which recorded what happened. `RVS_Overview.md` then restated it as a standing rule (*"That is the whole set"*, plus a test to apply before adding a fifth), and `CLAUDE.md` and `.github/copilot-instructions.md` mirrored the Overview. Nobody decided a count was the constraint; it hardened from a description into a rule by being copied. It was also actively wrong: two of the four copies still said *four* and omitted `RVS_Money.md`, so the rule generated exactly the drift it claimed to prevent. What the Sep 4 rebuild was actually solving was version drift across files, colliding identifier series, and specs for capability years from being built — none of which is a function of how many files there are. Replaced with a placement rule in `RVS_Overview.md`: one authoritative home per fact, a document earns its place by answering a question someone asks, unbuilt capability lives in `ARCHIVE/`, and a new document is registered in all three places on the same commit. Splitting an overgrown document is now as legitimate as merging two redundant ones. |
| Sep 7 2026 | **The financial shape is known and written down** (`RVS_Money.md`) | Serving cost is ~$0.03 per service request and fixed infrastructure is ~$117/month, so hard costs are covered by five shop locations and margins run 80–88% in every scenario. The base case still does not reach a founder salary inside 24 months; a $8,000/month draw needs ~135 locations. One 25-location dealer group is worth nineteen independent shops won one at a time, which makes the live dealer-group prospect the only genuinely load-bearing item in GTM. Ceiling at current scope and prices is under $1M ARR — recorded so no hire is ever made against a revenue curve that was not going to arrive. Two assumptions carry the whole model and neither is observed: requests per location, and founder close rate. The first three paying shops settle both; re-run the document then. |
| Sep 9 2026 | **Build order re-sequenced around the first mobile technician** (see "Sequenced for the first mobile technician") | The eight-item list is a topic order, not a delivery order, and read literally it front-loads work the first customer never touches. That customer has no DMS and never opens the manager app, so build item 4 and the "verify against a real DMS" half of item 2 leave the critical path. Items 1–3 are already substantially merged. The remaining gate is ten steps: packet layout + masthead (#492), HEIC→JPEG transcode so iPhone photos actually render (#508 — confirmed root cause of the "photos don't appear" report), the category vocabulary and fallback question bank (#452 → #453 → #454), hiding the AI assessment from the customer at intake (#483), two prod-deploy blockers (#463, #466), the Whisper flag and tenant-gate proof (#467, #465), packet-email reliability (#497, #494), and intake polish (#496, #479). Descope (#423) and the Cosmos reconcile (#477) run in parallel and gate nothing. The manager app (#420), C-7 links, billing (#425), #471, and the three #492 follow-ups split off as their own issues — per-location branding UI (#505), packet local-time (#506), structured-assessment spike (#507) — are explicitly deferred until the first tech is live. No scope added or removed — this is ordering only; the DMS paste-block verification stays a gate before the dealer-group pilot. |
| Sep 9 2026 | **Customer status page opened up; manager-authored notes added** (issue #500, closes Q9) | Spec X-1's "no conversation, no messaging, no file exchange" was never a deliberate decision — it hardened into the Spec unattributed, the same failure mode as the Sep 7 document-count rule. Removed. The status page is now free to show whatever is useful to the customer about their job, and the manager can attach free-text notes that render there (new Spec C-9). The one invariant kept: the page is display-only for the customer — no reply, no inbound message, no file upload — so there is no customer → manager channel and this does not reopen two-way messaging (still out of scope). Notes are manager-authored only, optional, length-capped (default 280 chars), sanitised on input, and never written to application logs. Spec X-1 rewritten, C-9 added, ASOT `RVS_Architecture.md` X-1 coverage note updated. Implementation in #500. |
| Sep 9 2026 | **Trial shortened to 30 days free at a named price, not 60** (supersedes Sep 6 2026, Q6) | Sixty days was long enough to blur the pilot into an open-ended free tier by another name — plenty of runway to be polite about a product nobody was pressure-testing. Thirty days is enough for a mobile tech's actual job volume to prove or kill the packet, and it shortens the gap between "yes" and the first invoice. The rule from Sep 6 is unchanged — quote the price, discount the pilot — only the length moves. Pricing itself ($39/mo mobile, $79/location banding) is untouched; every remaining "60 days" reference in this document is updated to 30. |
| Sep 9 2026 | **Q3 closed for good: no founding-partner term — the mobile tech gets the same 30 days as everyone else** | The six-month free period was a handshake carve-out for the first customer as design partner, made when the pilot length was otherwise open-ended. Now that every pilot is a named 30-day trial, a bespoke six-month term is a second pricing model to track and explain for a discount that stops earning its keep once the packet is proven. He gets the identical 30-day trial, then $39/mo locked for life as a founding-partner rate; the named-referral-fee upside from the Sep 6 decision is unchanged. |
| Sep 6 2026 | **Recorded, not solved: the scope filter has no category for "protects revenue"** | A product designed so nobody logs in has no usage signal and nothing to renew against; in month four an invoice reaches someone with no recent memory of the value. The obvious fix — a monthly recap email — fails filter #1 outright, since it makes no individual packet better. Not building it. The invoice line item does the same job for zero product work. Logged so that the filter gets an honest amendment when something harder needs this category, rather than making a commercial decision by accident. | |
| Sep 10 2026 | **Added a future-state register to this document** | Deferred sub-items were surviving only inside other issues' bodies (the `rv-warranty-rules` hint sub-task in #507, a hardened C-7 in #498, competitor-referral in #478) or inside a closing issue's checklist (the Cosmos integrated cache in #477), with no condition recorded for revisiting them. Leaving GitHub issues open for parked work makes them accrete and invites scope creep; this document already tracked one deferral class ("Deferred until the first mobile tech is live"). The register generalises it: one table, every row carrying a typed trigger and a "lands as", graduating the same way the resolved `Q-n` entries do, entry gated by scope-filter question 4. Seeded from an open-issue sweep done the same day. A matching issue-body template is in `.github/skills/github-issues/references/templates.md`. |
| Sep 11 2026 | **Commercial launch targets mobile techs and small shops only; Blue Compass Hurricane deferred** (approved by Mark, closes #530) | The local cluster around Washington/Hurricane/St. George/Cedar City — seven independent shops, approached in person, capped at five live pilots (#527) — *is* the commercial launch, not "party three." Blue Compass Hurricane is explicitly excluded from the pilot sequence: no X-3 signing authority at a 100-location PE-backed company, no price band that fits the $79/location independent pricing, and a corporate no is expensive and durable. It is downgraded from a pilot candidate to a relationship to nurture — a 30-minute observation visit, not a trial — running in parallel any time after Phase P1 (the Jay Lyons pilot, #525) closes, and gating nothing else in the sequence. See #526 and the future-state register (FS-8). |
| Sep 11 2026 | **Spec C-7 and this document's build-item-5 references reconciled to the #498 design** | #421 (the original C-7: anonymous, tokenized one-click email action links) closed `not_planned` on Sep 9, superseded by #498 (persistent ~30-day manager-app session + `/sr/{id}?action=…` deep links, no anonymous status-write endpoint) — but Spec C-7, the X-5 token row, and this document's build-order/deferred/worked-example/Q2 rows for "build item 5" still described the withdrawn design, caught while updating #420's AC to match. Spec C-7 rewritten around #498; X-5 narrowed to the one anonymous token scope that still exists (X-1's read-only status token) since C-7 no longer has an anonymous write surface for it to cover. This file: build-order row 5, the deferred-work table, the scope-filter worked example, and Q2 updated to match; build item 5 folds into item 4 (#420) rather than standing apart from it. `RVS_Architecture.md`, `RVS_Identity.md`, and `RVS_DataModel.md` still quote the pre-#498 X-5 wording (the "C-7 action links are per-request and per-action" clause) and need their own pass. |
| Sep 11 2026 | **Outbound-only SMS kept as a send channel; #458 narrowed — it is not "two-way SMS"** | The scope filter's "Two-way SMS: Fail" (`RVS_Plan.md:99`) rejects a conversational/inbox model specifically — it doesn't improve the packet, and it turns SMS into a place you have to visit. No two-way SMS code was ever built here: `RVS_Archive_Index.md` notes the archived two-way design's outbound half is what survived into this repo; there is no inbound-SMS handler anywhere in the codebase. #458 (executing the SMS descope under #423) had bundled the entire outbound stack — `AcsSmsNotificationService`, `ISmsNotificationService`, `NoOpSmsNotificationService`, the `SmsOptOut`/`EmailOptOut` opt-out fields — into the same deletion as the rejected two-way capability. That was an assumption riding along in a feature-descope issue, not a considered call, and #576 (texting the intake link to a customer's phone for the mobile intake flow) already flagged the same tension. Decision: outbound, one-directional SMS stays alongside email as a customer-notification channel. #458 narrowed to the two genuinely dead, never-called orchestrator methods (`NotificationOrchestrator.SendStatusChangeAsync`, `SendMagicLinkAsync`); the opt-out fields and the ACS SMS client are no longer in its deletion scope. Which field actually governs channel routing (`PreferredContact` vs. the opt-out pair) is unresolved and tracked separately in #577. |

---

## Things worth staying honest about

**The name overpromises.** "RV Service Intelligence" describes the archived product. What's being built is structured intake with good delivery. Not urgent, but don't let the name drive the roadmap back toward analytics.

**The money is written down now, and it is small.** `RVS_Money.md` carries the unit economics, the cost structure and three revenue scenarios. The short version: hard costs are covered by five locations, margins are 80–88%, and the base case still does not pay a salary inside two years. Read it before making any commitment that assumes revenue.

**At this scope, the product is a form and an email.** That is a real product with real value — but it is not hard to copy. The durable version of it is the quality of the diagnostic questions, the fit of the categories to actual RV failures, and how good the PDF looks on a shop counter. That's craft, not architecture. Spend the effort there.

**Retrieval discipline.** The archive exists to be used, but pulling something back is a decision that gets logged in this file with a reason. If features return one at a time because a prospect mentioned them, the document set will be heavy again in six months and so will the product.
