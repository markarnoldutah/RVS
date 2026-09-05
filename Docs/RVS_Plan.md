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

## Open questions

| # | Question | Owner | Needed by |
|---|---|---|---|
| **Q1** | PDF rendering library and license. Headless Chromium on App Service is operationally painful; a native .NET renderer avoids that but has license terms to check against commercial use. | Engineering | Before build item 1 |
| **Q2** | Build one-click status links (C-7) now or after the manager app? Recommendation: now — it's cheap once the token machinery exists and it's the thing that makes the "no dashboard" claim actually true. | Product | Before build item 4 |
| **Q3** | Does the mobile tech's "buy in" mean a subscription at ~$39/mo, or equity/partnership? Materially different. | GTM | Next conversation with him |
| **Q4** | At the dealer group: who owns the location service pages — marketing, IT, or an agency? Determines whether the pilot is a 20-minute change or a procurement cycle. | GTM | Before pitching |
| **Q5** | Status vocabulary — is the C-3 set right for a mobile tech *and* a dealership, or does it need to be per-location configurable? Prefer fixed; configurable status sets are a known complexity sink. | Product | Before build item 4 |
| **Q6** | Pricing at this reduced scope. The old four-tier model priced a product that no longer exists. | GTM | Before build item 7 |

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

---

## Things worth staying honest about

**The name overpromises.** "RV Service Intelligence" describes the archived product. What's being built is structured intake with good delivery. Not urgent, but don't let the name drive the roadmap back toward analytics.

**At this scope, the product is a form and an email.** That is a real product with real value — but it is not hard to copy. The durable version of it is the quality of the diagnostic questions, the fit of the categories to actual RV failures, and how good the PDF looks on a shop counter. That's craft, not architecture. Spend the effort there.

**Retrieval discipline.** The archive exists to be used, but pulling something back is a decision that gets logged in this file with a reason. If features return one at a time because a prospect mentioned them, the document set will be heavy again in six months and so will the product.
