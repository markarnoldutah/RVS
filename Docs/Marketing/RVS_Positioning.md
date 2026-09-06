# RVS — Positioning

**Version:** 1.0 · September 4, 2026
**Scope:** What we say RVS is, and who we say it against. Aligned to `../RVS_Overview.md`.

The prior positioning set — four-tier pricing, OEM data licensing, benchmarking, the cross-dealer moat, the technician app — described a product that is now archived. The competitor facts in those documents were good and are retained here. The strategy built on top of them is not.

---

## The line

> **A structured intake form for RV service work that emails a clean write-up to whoever runs the service department.**

Say it plainly. The product is a form and an email. That is a real product with real value, and overstating it is the fastest way to lose a service manager's attention.

The demo is the pitch: a customer fills out a form on a phone, and a minute later a readable one-page write-up with photos and a printable PDF is in the shop's inbox. If that lands, nothing else needs saying.

---

## Who it's for

Anyone who receives RV service requests badly today — as voicemails, as texts with no VIN, or through a website form that promises a callback and captures nothing.

Two live prospects shape everything:

**An independent mobile technician.** One person, a truck, no DMS. His problem is driving to a job with the wrong parts because the phone description was wrong. Lead with wasted trips: he eats $100–200 in time and fuel every time. Structured intake with photos and a decoded VIN before he leaves means the right part is on the truck. He never logs into anything.

**A large dealer group.** Their location pages end in a callback form that captures nothing actionable, and their advisors then play phone tag to learn what the customer already knew. Lead with their own web form. Replace it with structured triage arriving in the existing service inbox. No new logins, no DMS project, no IT involvement.

Both said some version of **"not another dashboard."** The whole product is built around taking that seriously. Do not undermine it in the pitch.

---

## What we do not claim

- **Not a DMS.** No work orders, parts, labor, invoicing, accounting.
- **Not a scheduling system.** No calendar, no bays, no capacity planning.
- **Not a messaging platform.** A request goes to the shop, a status link goes to the customer. We do not host conversations. Two-way SMS is out.
- **Not analytics.** No dashboards, no benchmarking, no cross-dealer comparison.
- **Not integrated with any DMS.** Deliberately. A paste block does the job today without a partner program.

---

## The competitive map

Three layers, three jobs. If someone asks you to draw it, draw this.

```
CUSTOMER SIDE
  Kenect, Podium      → conversations, reviews, payments
  ServiceNomad        → shop operating system, voice AI, scheduling
  QuoteIQ, RV Svc Ste → SMB CRM and invoicing
  RVS                 → structured intake → a write-up in the inbox
  ──────── feeds a paste-ready block into ────────
  IDS Astra, Lightspeed → DMS: accounting, parts, RO, warranty
BACK OFFICE
```

### IDS Astra G2 and Lightspeed

Thirty- to forty-year incumbents; roughly 1,200 and 4,500 dealer locations respectively. Lightspeed is in Salt Lake City. Both shipped free OEM VIN decoding in 2025; Lightspeed also has Service Tech Video and a Service Scheduler marketed explicitly at reducing RECT.

**Never say "DMS replacement."** You are done in the room if you do. The DMS is genuinely good at accounting, parts, repair orders, warranty and F&I, and the buyer has spent years training staff on it. Insulting it insults their judgment.

What the DMS still cannot do is capture the customer's photos, video and description *before* the unit arrives — it opens a repair order after. That is the entire wedge, and it is enough.

Note what changed: **VIN decode is parity, not a differentiator.** Do not lead with it.

### Kenect

Roughly 10,000 dealerships across several verticals; texting and reputation management with AI added after the Auto Labs acquisition. Pleasant Grove, Utah.

Coexist. "Kenect handles the conversation. RVS puts a written work-up in the service manager's inbox." A dealer can run both. Every messaging feature we might build is one Kenect already does better.

### ServiceNomad

RV-specific service operating system, Austin, consultative sales with an audit-first motion. They run the shop end to end. We do one thing before the shop starts. A single shop that wants a full operating system should probably buy theirs.

### QuoteIQ, RV Service Suite

SMB CRM and invoicing, roughly $30–100/mo. Different category — that is the band where a solo operator actually buys, which is useful pricing context, but they are not solving intake.

---

## The honest risks

**The product is not hard to copy.** A form and an email is a weekend for a competent team. The durable version is craft, not architecture: the quality of the diagnostic questions, how well the categories fit real RV failures, and how good the PDF looks on a shop counter. Spend the effort there.

**One interested technician is not market validation.** He is a design partner and a reference — a source of truth about workflow and someone who will take a call from a prospect. He is not evidence the segment buys.

**Distribution beats features.** The thing that should worry us is IDS, Lightspeed or Kenect shipping a mediocre intake form to an installed base. We have no moat against that at this scope, and pretending otherwise in internal documents is how bad decisions get made.

**The name overpromises.** "RV Service Intelligence" describes the archived product. Don't let it drag the roadmap back toward analytics.

---

## Forbidden phrases

Not defensible. Remove on sight.

- "No competitor has tackled service intake at this level."
- "The RV Service Operating System."
- "Replacement for your DMS / CRM / messaging tool."
- "First mover" anything.
- Any claim about benchmarking, industry datasets, cross-dealer insight, or OEM data — **that capability is archived and does not exist.**
- Any specific pricing. See below.

---

## Pricing

**Open.** The old four-tier model priced a product that no longer exists, and nothing has replaced it — this is Q6 in `../RVS_Plan.md`, owned by GTM, needed before billing is built.

Two anchors worth holding while it's decided: the SMB tools a solo operator already buys sit at roughly $30–100/mo, and a dealer group paying $450–3,000/mo for its DMS will not rationally pay more than that for a layer in front of it.

Do not quote a number to a prospect until this is settled. If pressed, say the pilot is free and pricing follows the pilot.

---

## The one thing that cannot be dropped

**The anonymization license clause.** Whatever a customer signs must grant a perpetual, irrevocable license to use their service data in anonymized, aggregate form. It costs nothing to include now and cannot be added later without renegotiating with every existing customer from a weaker position.

This is not a marketing point — never pitch it, never mention the dataset. It is a paperwork requirement (Spec X-3) that GTM owns because GTM owns the first contract.
