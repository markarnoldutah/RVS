# RVS — Go To Market

**Version:** 1.0 · September 4, 2026
**Scope:** Who to approach, in what order, and what to say. Positioning is in `RVS_Positioning.md`; objections in `RVS_Objections.md`.

---

## The motion

Design partners first, pricing second. The product is a form and an email — it demos in ninety seconds and needs no integration, no IT involvement and no procurement. That is the whole advantage. Spend it on getting into shops, not on building collateral.

Two prospects are already live and they are the plan. Everything below is about widening that to a handful of design partners, not about a funnel.

---

## Ideal early customer

Ranked by how fast they can say yes.

| Segment | Why | Decision path |
|---|---|---|
| **Mobile RV technicians** | Intake chaos is their daily pain; a wasted trip has a dollar figure attached. No DMS, no IT, no committee | Owner decides on the spot |
| **Independent repair shops** | Usually no structured intake at all — phone, paper, scattered photos | Owner, yes or no |
| **Collision / insurance shops** | Documentation-heavy work; photos before arrival materially help estimates | Owner or manager |
| **Regional dealer groups** | Real volume, but their location pages capture nothing. Ask a service manager, never a CIO | Service manager for a pilot; procurement if you get routed upward |

Avoid national chains for now. Procurement friction and IT review will cost months and teach you less than one afternoon in a shop.

---

## The two live prospects

**Mobile technician.** Lead with wasted trips. He drives out, the failure is a different component than the phone call suggested, and he eats the trip. Structured intake with photos and a decoded VIN before he leaves means the right part is on the truck. He never logs into anything — the write-up hits his email.

Open: is his "buy in" a subscription at roughly $39/mo, or equity and partnership? Those are materially different conversations. This is Q3 in `../RVS_Plan.md` and it should be settled at the next conversation with him.

**Dealer group.** Lead with their own web form. Their location pages collect a name and a callback request; replace that with structured triage arriving in the service inbox as a one-page write-up. No new logins, no DMS project. Ask for a two-store pilot.

Open: who owns the location service pages — marketing, IT, or an outside agency? That single fact decides whether the pilot is a twenty-minute change or a procurement cycle. Q4, and it should be answered *before* pitching.

---

## Local design partners

The founder is in Washington, Utah, within ~120 miles of a usable cluster of shops. That proximity allows something most founders cannot do: **watch the service desk in person.** How advisors answer the phone, how jobs reach techs, how repair notes actually get written. Those observations are worth more than any survey.

| Candidate | Type | Notes |
|---|---|---|
| Lazydays RV, Washington UT (formerly RVzz) | Independent service shop, since acquired | Closest. See the caution below |
| RV Doctor Mobile Service, St. George | Mobile owner-operator | Closest analogue to the live mobile-tech prospect |
| Adventure RV Service Center, Hurricane | Independent service shop | Service-heavy, independent ownership, fast decisions |
| Motor Sportsland RV Service, St. George | Small regional dealer group | One slightly larger service department without national-chain bureaucracy |
| Cedar City RV & Trailer Repair | Independent shop | Geographic diversity, tests remote onboarding |

**Caution on Lazydays.** Earlier notes named RVzz the single best first partner. It has since been acquired by Lazydays, and the field research in `Dealerships/LazyDays_Repair.md` documents sustained customer complaints about warranty handling and — directly relevant — about communication: no updates unless the customer initiates, service managers not returning calls, week-long response gaps.

That cuts both ways. It is exactly the problem RVS addresses, which makes it a sharp pitch. But post-acquisition it is no longer a fast independent decision, and a shop with a communication problem may have a management problem rather than a tooling problem. Treat it as a prospect, not as the anchor design partner.

---

## Outreach

Cold email and LinkedIn work in this niche — it is small, and operators are used to vendors. Keep it to four lines.

> Subject: Reducing service phone calls at RV dealerships
>
> Hi [Name],
>
> I built a simple tool that lets RV customers submit service requests online with VIN, photos and a description — and emails your service department a one-page write-up they can print or paste into their DMS.
>
> I'm looking for a couple of shops willing to try it and tell me what's wrong with it. Ten minutes?

Ask a service manager. Not a CIO, not an owner if there is a service manager. The service manager feels the problem hourly.

---

## The demo

Ninety seconds, on a phone, live. Do not present slides.

1. **Fill out the form as a customer would.** VIN, description, two photos, answer the AI follow-up questions.
2. **Open the inbox.** Show the email that just arrived — the write-up, the photos, the printable PDF.
3. **Show the paste block.** Point at it and say: *"That drops into the complaint field in your DMS without retyping anything."*

Then stop talking. The interesting question is whether they reach for the PDF or the paste block — that tells you which half of the product they actually want.

Do not demo the manager app. A manager should be able to run a full week on email alone; showing a dashboard undercuts the one claim that differentiates the product.

---

## Pilot terms

Free during the pilot. One thing must be true of the paperwork before a design partner starts: **the terms must grant a perpetual, irrevocable license to use their service data in anonymized, aggregate form** (Spec X-3). It costs nothing now and cannot be retrofitted later without renegotiating with every existing customer.

Never pitch this. It is a clause, not a feature.

---

## What proves it worked

Borrowed from `../RVS_Plan.md`, because these are sales claims as much as engineering ones:

- The paste block goes into a real DMS complaint field without reformatting — **verified against an actual DMS, not a mock.**
- The PDF prints legibly on a shop printer, in greyscale.
- A service manager runs a full week on email alone and never opens the manager app.

That last one is the real test, and it is also the best case study sentence available: *"they never logged in."*

---

## Deliberately not doing yet

No website, no content marketing, no conference presence, no case studies, no ARR target driving behaviour. At two prospects and zero shipped packets, all of that is premature. Revisit when three shops are running on it.

Pricing is unresolved — Q6. Do not quote a number. The pilot is free; pricing follows the pilot.
