# RVS — Plan

**Version:** 1.0 · September 4, 2026

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
| **7** | Stripe billing + trial | 2 sprints |
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
| Per-location configurable status vocabularies (Q5) | **Fail** | 3 and 4. A known complexity sink; prefer a fixed set |

**A prospect asking for something is not an answer to any of the four.** Neither is a competitor having it. Retrieving archived capability is governed separately, in `RVS_Archive_Index.md`, and lands in the decision log below either way.

---

## Open questions

| # | Question | Owner | Needed by |
|---|---|---|---|
| ~~**Q1**~~ | ~~PDF rendering library and license.~~ **Resolved (issue #426): QuestPDF.** Native .NET renderer, no headless browser. Community License v3.0 is free under USD 1M revenue (RVS qualifies); paid tier is perpetual USD 1,999 / 4,999 if the threshold is crossed. See decision log and Spec B-7. | Engineering | ~~Before build item 1~~ Done |
| **Q2** | Build one-click status links (C-7) now or after the manager app? Recommendation: now — it's cheap once the token machinery exists and it's the thing that makes the "no dashboard" claim actually true. | Product | Before build item 4 |
| **Q3** | Does the mobile tech's "buy in" mean a subscription at ~$39/mo, or equity/partnership? Materially different. | GTM | Next conversation with him |
| **Q4** | At the dealer group: who owns the location service pages — marketing, IT, or an agency? Determines whether the pilot is a 20-minute change or a procurement cycle. | GTM | Before pitching |
| **Q5** | Status vocabulary — is the C-3 set right for a mobile tech *and* a dealership, or does it need to be per-location configurable? Prefer fixed; configurable status sets are a known complexity sink. | Product | Before build item 4 |
| **Q6** | Pricing at this reduced scope. The old four-tier model priced a product that no longer exists. | GTM | Before build item 7 |
| **Q7** | Token model. X-5 requires per-request, hashed, TTL-bounded tokens. What is built is a per-*customer* magic link — 90-day, unhashed, on `GlobalCustomerAcct`, and a prior ASOT decision argued explicitly for unhashed. The Spec supersedes that, but this is a code and data migration, and C-7's one-click links reuse the same machinery. | Engineering | Before build item 3 |
| **Q8** | Voice transcription (Whisper) and VIN photo extraction (gpt-4o vision) are fully built into intake steps 3 and 5 but appear nowhere in the Spec. In scope or archived? The Whisper account is unconditional infrastructure spend either way. | Product | Before the next infra deploy |

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
| Sep 5 2026 | **PDF rendering: QuestPDF** (issue #426, closes Q1) | Headless Chromium (PuppeteerSharp/Playwright) fails B-7 — a browser process on Linux App Service B1 that can't be reliably health-checked or recycled, plus a ~300 MB engine the base image can't support. QuestPDF renders in-process with a bundled native lib, no network call per render, sub-100 ms per page. Community License v3.0 is free under USD 1M annual revenue (RVS qualifies now; commercial use is permitted); above that, 90 days to buy a perpetual Professional (USD 1,999) or Enterprise (USD 4,999) licence — bounded and deferrable. Publicly traded / public-sector exclusions don't apply. Fallback: PDFsharp/MigraDoc (MIT). |

---

## Things worth staying honest about

**The name overpromises.** "RV Service Intelligence" describes the archived product. What's being built is structured intake with good delivery. Not urgent, but don't let the name drive the roadmap back toward analytics.

**At this scope, the product is a form and an email.** That is a real product with real value — but it is not hard to copy. The durable version of it is the quality of the diagnostic questions, the fit of the categories to actual RV failures, and how good the PDF looks on a shop counter. That's craft, not architecture. Spend the effort there.

**Retrieval discipline.** The archive exists to be used, but pulling something back is a decision that gets logged in this file with a reason. If features return one at a time because a prospect mentioned them, the document set will be heavy again in six months and so will the product.
