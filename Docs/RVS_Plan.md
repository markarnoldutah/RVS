# RVS — Plan

**Version:** 1.1 · September 6, 2026

---

## Build order

| # | Work | Rough size |
|---|---|---|
| **1** | **Packet: composition, HTML + print stylesheet, PDF** | 1–2 sprints |
| **2** | **Email delivery: ACS, recipients config, photo attachments, paste block, retry/bounce** | 1 sprint |
| **3** | Customer status page + status token | ~1 sprint |
| **4** | Manager app: list, detail, status, disposition, resend, settings | 1–2 sprints |
| **5** | One-click status links in the packet email (C-7) | Days, if the token machinery from 3 is reused |
| **6** | Issue-category vocabulary + per-category fallback diagnostic questions | Domain time, parallel, not engineering-blocked |
| **7** | Stripe billing + trial | 2 sprints — **do not start until three shops are running.** The first five customers get a hand-sent invoice and a Stripe payment link. Two sprints off the critical path, and you learn what to build |
| **8** | Descope: delete archived-scope code — analytics, Kanban board, batch-outcome, SMS, technician/scheduling fields, `build-mobile.yml` | ~1 sprint, can run in parallel |

Items 1–2 are the demo. You can pitch both prospects the moment those work end to end, before the manager app exists — that's the point of the design.

Item 6 runs alongside everything and is the only thing on this list that isn't code. Don't let it slip to the end; the fallback question bank is what makes the packet read as expert rather than generic, and it's the cheapest quality lever in the product.

## Ship criteria

- A customer completes intake on a phone and the service manager has a readable email with photos and a printable PDF within a minute.
- The PDF prints legibly on a shop printer in greyscale.
- The paste block goes into a real DMS complaint field without reformatting. **Verify against an actual DMS, not a mock.**
- The customer can check status from the link without logging in.
- A service manager can run a full week on email alone, never opening the manager app.

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
| One-click status links in the packet email (C-7) | **Pass** | Strong 1 and 2 — it is the feature that makes "no dashboard" literally true |
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
| **Q2** | Build one-click status links (C-7) now or after the manager app? Recommendation: now — it's cheap once the token machinery exists and it's the thing that makes the "no dashboard" claim actually true. | Product | Before build item 4 |
| ~~**Q3**~~ | ~~Does the mobile tech's "buy in" mean a subscription at ~$39/mo, or equity/partnership?~~ **Resolved: subscription, never equity.** Six months free, then $39/mo locked for life as a founding-partner rate. Upside, if he wants it, is a named referral fee — not shares. See decision log and `Marketing/RVS_GoToMarket.md`. | GTM | ~~Next conversation with him~~ Done |
| **Q4** | At the dealer group: who owns the location service pages — marketing, IT, or an agency? **Downgraded from a gate to a second-conversation item.** The pilot is built to require nothing the service manager cannot authorize alone — counter QR code, advisor email signatures, the existing callback autoresponder — so the web-page change is the expansion ask, not the entry ask. Still worth knowing; no longer blocks a pitch. | GTM | ~~Before pitching~~ Before the expansion ask |
| ~~**Q5**~~ | ~~Status vocabulary — is the C-3 set right for a mobile tech *and* a dealership, or does it need to be per-location configurable?~~ **Resolved (issue #428): one fixed set, adopted from the code** — `New / InProgress / WaitingOnParts / WaitingOnCustomer / Completed / Cancelled`. Per-location configurable vocabularies rejected. See decision log and Spec C-8. | Product | ~~Before build item 4~~ Done |
| ~~**Q6**~~ | ~~Pricing at this reduced scope.~~ **Resolved: one product, per location, no tiers.** $39/mo mobile; $79/location, banding to $69 at 3–9 and $59 at 10+; two months free on annual prepay; 300 requests/location/month fair use. Pilot is 60 days free at a named price, not open-ended free. Per-packet pricing, free-intake/paid-packet, and charging the RV owner are all rejected. See decision log and `Marketing/RVS_Positioning.md`. | GTM | ~~Before build item 7~~ Done |
| ~~**Q7**~~ | ~~Token model.~~ **Resolved (issue #427): X-5 as written, with split scope.** SHA-256-hashed storage (raw token never persisted). Status token stays **per-customer** (TTL ≤ 30 days, sliding renewal); C-7 action links are **per-request and per-action** (single-purpose, TTL ≤ 14 days or single-use); one shared token helper. Prior unhashed ARCHIVE decision overturned — its own stated trigger, a write-capable token, is met by C-7. Migration backfills hashes pre-GA then drops the plaintext field. See decision log and Spec X-5; implementation in #440 / #441. | Engineering | ~~Before build item 3~~ Done |
| ~~**Q8**~~ | ~~Voice transcription (Whisper) and VIN photo extraction (gpt-4o vision) are fully built into intake steps 3 and 5 but appear nowhere in the Spec.~~ **Resolved (issue #429): all four in scope.** Voice input, VIN-from-photo, issue insights, and the capability pre-check are added to the Spec as A-9–A-12. Nothing archived; no descope sub-issue on #423. Whisper and gpt-4o both stay — the `deployWhisper` / gpt-4o flag work (#467) still proceeds so the spend is per-environment and reversible, defaulting on. See decision log and Spec A-9–A-12. | Product | ~~Before the next infra deploy~~ Done |

---

## Pitches

**Mobile tech — lead with wasted trips, not with data.** He drives out, the failure is a different component than the phone call suggested, and he eats the trip: call it $100–200 in time and fuel. Structured intake with photos and a decoded VIN before he leaves means the right part is on the truck. He never logs into anything; the write-up hits his email.

**Dealer group — lead with their own web form.** Their location pages collect a name and a callback request. Replace that with structured triage arriving in the existing service inbox as a one-page write-up. No new logins, no DMS project, no IT involvement. Ask for a two-store pilot. **Ask a service manager, not a CIO.**

Honest note on both: one interested mobile technician is a design partner and a reference, not market validation. Treat him as a source of truth about workflow and as someone who'll take a call from a prospect — not as evidence the segment buys.

---

## Decision log

| Date | Decision | Why |
|---|---|---|
| Sep 4 2026 | **Archived the prior 11-document set; rebuilt as four documents** | The document set had outgrown the product. Version drift across files, colliding identifier series, and specs for capability years from being built. |
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
| Sep 6 2026 | **Recorded, not solved: the scope filter has no category for "protects revenue"** | A product designed so nobody logs in has no usage signal and nothing to renew against; in month four an invoice reaches someone with no recent memory of the value. The obvious fix — a monthly recap email — fails filter #1 outright, since it makes no individual packet better. Not building it. The invoice line item does the same job for zero product work. Logged so that the filter gets an honest amendment when something harder needs this category, rather than making a commercial decision by accident. | |

---

## Things worth staying honest about

**The name overpromises.** "RV Service Intelligence" describes the archived product. What's being built is structured intake with good delivery. Not urgent, but don't let the name drive the roadmap back toward analytics.

**At this scope, the product is a form and an email.** That is a real product with real value — but it is not hard to copy. The durable version of it is the quality of the diagnostic questions, the fit of the categories to actual RV failures, and how good the PDF looks on a shop counter. That's craft, not architecture. Spend the effort there.

**Retrieval discipline.** The archive exists to be used, but pulling something back is a decision that gets logged in this file with a reason. If features return one at a time because a prospect mentioned them, the document set will be heavy again in six months and so will the product.
