# RVS — Positioning

**Version:** 1.1 · September 6, 2026
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

There is a cheap fix that needs no renaming: **sell the artifact, not the company.** Name the packet and make it the noun in every sentence — the thing a shop buys is a one-page service request write-up, not a platform. Lead with `rvintake.com` publicly and leave `rvserviceflow.com` as the corporate domain. *(Open: confirm `rvintake.com` is actually held. Spec A-1 and X-1 both assume it and the only zone file in the repo is for `rvserviceflow.com`. Worth settling before it is printed on a counter QR code.)*

---

## Forbidden phrases

Not defensible. Remove on sight.

- "No competitor has tackled service intake at this level."
- "The RV Service Operating System."
- "Replacement for your DMS / CRM / messaging tool."
- "First mover" anything.
- Any claim about benchmarking, industry datasets, cross-dealer insight, or OEM data — **that capability is archived and does not exist.**
- Any pricing other than the numbers below. Improvised discounts and invented tiers are the failure mode here, not quoting a price.

---

## Pricing

**Decided — Q6 closed.** One product, one price, per location. No tiers.

| | Price | Who |
|---|---|---|
| **Mobile** | **$39/mo** | One person, no fixed shop |
| **Shop** | **$79 per location/mo** | 1–2 locations |
| | **$69 per location/mo** | 3–9 locations |
| | **$59 per location/mo** | 10+ locations |
| **Annual prepay** | Two months free | Any |

Everything is included at every price. Mobile is not a reduced tier — it is the same product at a different unit, because a solo operator has no "location" to count. It is defined by eligibility, never by a smaller feature set. If a two-truck shop takes the $39, let them; you want the packets.

Quote these numbers. The free period is a discount, not a mystery — see "Terms, and the first sixty days" in `RVS_GoToMarket.md`.

**Why there are no tiers.** Tiers need feature gates, and nothing here is gateable without breaking the scope filter in `../RVS_Plan.md`. Gate the PDF or the photos and the packet gets worse — filter #1, disqualifying. Gate the manager app and you are charging for the thing you tell people they will never open. The archived four-tier model worked because it gated benchmarking, SSO and a technician app. None of those exist.

Per location is the right unit because it is already the product's own boundary: `rvintake.com/{locationSlug}`, the B-6 configuration, the recipient list. A dealer group grows into more money without a second pricing conversation.

**Why $79 and not $199.** The binding constraint is not value, it is who can say yes. The whole motion is *ask a service manager, never a CIO*, and a service manager's discretionary spend is a few hundred dollars a month. Two stores at $79 is $158 — a corporate card, not a procurement cycle. At $199 per location a five-store pilot is $1,000/mo, which routes to exactly the person `RVS_GoToMarket.md` says to avoid, at exactly the cost in months it warns about. **This price buys sales velocity. It is not a valuation of the product.** Do not optimize it upward before three shops are running on it.

The value arithmetic is comfortable regardless. Forty intake calls a day at three minutes each is about two hours of advisor time, on the order of $1,000/month per store; $79 is roughly 8% of the labor it dents. It also sits sanely under the $450–3,000/mo that same dealer pays for the DMS this feeds.

**Against the functionality, $79 is low, and that is deliberate.** Spec A-9 through A-12 are real capability — dictation, VIN-from-photo, AI follow-up questions, issue insights — on top of NHTSA decode, direct-to-blob attachments, a rendered PDF, the paste block, the status page and one-click status links, with per-submission Azure OpenAI cost behind them. The buyer does not price that machinery; they price the artifact in their inbox against answering the phone. The gap between what the product does and what it sells for today is the first thing to revisit once there is usage to point at.

**Models rejected, so they stop coming back:**

- **Per-packet or usage pricing.** It suppresses the exact behavior the product depends on — a location metered per submission stops promoting the link — and early volume is what produces both proof and the X-2 ledger. It also makes the invoice variable, which invites scrutiny a flat $79 never gets. This is the natural second model for a high-volume group, not the first.
- **Free intake, paid packet.** The packet is the product. There is nothing left on the other side of that line.
- **Charging the RV owner.** No.

Fair-use cap is 300 requests per location per month. It exists to stop an absurd outlier, not to meter. Do not feature it.

**Don't build billing yet.** Build item 7 is two sprints. The first five customers get a hand-sent invoice and a Stripe payment link. Put the month's request count on the invoice line item — *"September — 14 service requests"* — which is the cheapest available answer to the renewal risk below.

### The renewal risk nobody had written down

A product designed so that nobody logs in has no usage signal and nothing to renew against. In month four an invoice arrives to someone with no recent memory of the value. That is not an argument for a dashboard. It is evidence that the scope filter has no category for *protects revenue*: the obvious fix, a monthly recap email, fails filter #1 outright because it makes no individual packet better.

Recorded rather than solved. The invoice line item does the same job for zero product work. If something harder ever needs this category, the filter gets an honest amendment then — it does not get to make a commercial decision by accident in the meantime.

---

## The one thing that cannot be dropped

**The anonymization license clause.** Whatever a customer signs must grant a perpetual, irrevocable license to use their service data in anonymized, aggregate form. It costs nothing to include now and cannot be added later without renegotiating with every existing customer from a weaker position.

This is not a marketing point — never pitch it, never mention the dataset. It is a paperwork requirement (Spec X-3) that GTM owns because GTM owns the first contract.
