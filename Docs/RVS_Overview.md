# RVS — Overview

**Version:** 1.0 · September 4, 2026
**Status:** Active. This document set supersedes everything in [`ARCHIVE/`](ARCHIVE/).

---

## What RVS is

A structured intake form for RV service work that emails a clean write-up to whoever runs the service department.

That's it. That's the whole product right now.

A customer (or a technician on the customer's behalf) fills out a short web form: VIN, contact, what's wrong, photos, a few AI-generated follow-up questions. On submit, the service manager gets an email containing a readable summary, the photos, and a one-page PDF they can print or paste into their DMS. The customer gets a link they can check for status.

## The three components

| | Component | State |
|---|---|---|
| **A** | **Intake app** (Blazor, anonymous, per-location URL) | Substantially built |
| **B** | **Packet + email delivery** (body text, image attachments, PDF) | To build — this is the current work |
| **C** | **Manager app** (Blazor, thin: set SR status, disposition) | Minimal build |

Spec: `RVS_Spec.md`. Build order: `RVS_Plan.md`.

## Who it's for

Anyone who receives RV service requests and currently receives them badly — as voicemails, texts with no VIN, or a website contact form that says "we'll call you back."

Two live prospects:

- **An independent mobile technician.** One person, a truck, no DMS. His problem is driving to a job with the wrong parts because the phone description was wrong.
- **A large dealer group.** Their location pages end in a callback form that captures nothing actionable. Their advisors then play phone tag to learn what the customer already knew.

## What RVS is not, for now

Both prospects said some version of *"not another dashboard."* The product is built around taking that seriously.

- **Not a DMS.** No work orders, no parts, no labor, no invoicing, no accounting.
- **Not a scheduling system.** No calendar, no bay assignment, no capacity planning.
- **Not a messaging platform.** RVS sends a service request to the shop and a status link to the customer. It does not host conversations. Two-way SMS is out.
- **Not an analytics product.** No benchmarking, no dashboards of charts, no cross-dealer comparison.
- **Not integrated with any DMS.** Deliberately. Email plus a paste-ready text block does the same job today without a partner program. See [`RVS_Archive_Index.md`](RVS_Archive_Index.md) for the analysis behind that call.

## The one thing that must not be dropped

Enterprise features, benchmarking, and the anonymized dataset are archived — they can be rebuilt later from working code.

**Customer agreement language cannot.** If the terms a customer signs today don't include a perpetual, irrevocable license to use their service data in anonymized aggregate, that right has to be re-papered with every existing customer later, one at a time, from a weaker negotiating position. That language costs nothing to include now.

Two things stay in the build for this reason and this reason only:

1. The anonymization license clause in the ToS and any design-partner agreement.
2. The append-only ledger write on intake submission. It's already built, it's invisible to the user, and it means the record exists when it's needed.

Everything downstream of the ledger — the anonymization pipeline, k-anonymity, benchmarking APIs, OEM licensing — is archived.

## Document set

Five documents. That is the whole set.

| Document | Use it when |
|---|---|
| `RVS_Overview.md` | You need to remember what this is, or explain it to someone |
| `RVS_Spec.md` | You're writing code |
| `RVS_Plan.md` | You're deciding what to do next, or need to find an open question |
| `RVS_Money.md` | You need unit economics, cost structure, a revenue scenario, or the break-even |
| `RVS_Archive_Index.md` | You need something from the old document set, or want to know why something was cut |

`RVS_Money.md` was the fifth, added September 7, 2026 after the pricing decision — the test below was applied and it failed to fit anywhere. If you find yourself adding a sixth, check whether it belongs as a section in one of these first.

---

*Prior document set (11 documents, ~9,000 lines) is archived. Nothing was deleted.*
