# RVS — Money

**Version:** 1.0 · September 7, 2026
**Scope:** What this costs to run, what it plausibly earns, and when it pays anybody. Prices are decided in `Marketing/RVS_Positioning.md`; this document is what follows from them.

The archived financial model (`ARCHIVE/ASOT/RVS_Implementation_Plan_v2.md` §7) priced a four-tier product with OEM licensing behind it. That product is archived and so is its model. This is a replacement, built from the SKUs actually declared in `ASOT/Infra/Bicep.IaC/` and the AI calls actually specced in `RVS_Spec.md` A-9–A-12.

**Read this with the skepticism it deserves.** Azure unit prices are estimates, not a live pricing pull, and the revenue scenarios are judgement, not forecast. The cost conclusions survive being wrong by ±30% because the cost side barely matters. The revenue conclusions do not — they are the assumptions in §4, and they are what to argue with.

---

## 1. Unit economics

Per service request, from intake through delivered packet:

| Component | Cost |
|---|---|
| Azure OpenAI gpt-4o — five calls (A-4 follow-ups, A-5 category, A-11 insights, packet summary, A-10 VIN vision) | $0.023 |
| Whisper transcription (A-9), ~1 minute of dictation | $0.006 |
| ACS email + attachments, capped at ~9.5 MB per send (B-4) | $0.002 |
| Blob storage and egress, ~15 MB | $0.002 |
| Cosmos RU — intake write, ledger append (X-2), reads | <$0.001 |
| **Total** | **~$0.03** |

The two size figures differ on purpose. Blob keeps every original upload — `A-6` allows ten
25 MB files — while one email is budgeted at 9.5 MB under ACS's 10 MB request ceiling, base64
included (`Spec B-4`). Storage cost tracks what the customer uploaded; email cost tracks what
actually ships. Neither number moves the total, which rounds to $0.03 either way.

A location submitting 100 requests a month costs **$3.21** to serve against $79 of revenue. Gross margin 96%.

| Requests/location/month | Cost | Gross margin on $79 |
|---|---|---|
| 100 | $3.21 | 96% |
| 300 *(current fair-use cap)* | $9.64 | 88% |
| 600 | $19.28 | 76% |
| 1,000 | $32.13 | 59% |
| 2,460 | $79.00 | **0% — break-even** |

**The 300-request fair-use cap has roughly 8× headroom before it protects any margin.** It is an abuse guard, not an economic one, and it should never be the reason a dealer group hesitates. Raising it to 1,000 costs nothing. Treat that as a sales lever held in reserve rather than a number to defend.

---

## 2. Cost structure

**Fixed, from the SKUs in `ASOT/RVS_Infrastructure.md`:**

| Resource | Monthly |
|---|---|
| Static Web Apps, Standard × 4 (Intake + Manager, staging + prod) | $36 |
| App Service B1 × 2 (staging + prod) | $26 |
| Log Analytics + Application Insights, two environments | $25 |
| Cosmos DB serverless, low volume | $15 |
| Blob Storage, Standard LRS Hot | $10 |
| Key Vault × 2, DNS zones × 2, ACS base | $5 |
| **Azure total** | **~$117** |

Azure OpenAI S0 carries no standing charge — both accounts are pay-per-token, so all AI cost is variable and sits in §1.

**Non-Azure, roughly $250/month** once there are real customers: domains, tooling, Auth0 headroom above the Free tier, and E&O / cyber insurance. QuestPDF is $0 under USD 1M revenue (Spec B-7) and stays $0 through every scenario below.

**E&O and cyber liability, scheduled, not just budgeted (Gate G6, #534).** The LLC exists; the two policies under it do not yet. They cover different failures. **E&O (professional/technology errors & omissions)** responds to a claim that the product itself caused a loss — the packet's AI-generated preliminary assessment (Spec A-11) is advisory, not a licensed diagnosis, and "the app told the shop the wrong thing and it cost someone money" is the exposure that insures against. **Cyber liability** responds to a breach of the data itself: notification costs, forensics, credit monitoring, and regulatory response if Cosmos or Blob storage is compromised. Yes, this is data worth protecting — name, phone, email, and VIN together are enough to trigger most state breach-notification statutes on their own, and the photographs `A-6` allows (including of a customer's home, parked RVs being what they are) turn a data-broker-grade breach into a materially worse one. The reason to bind now rather than defer: that data is currently routing through Azure infrastructure Mark holds personally, under an LLC that has no coverage in place yet. **Bind both before the first live location** (before Jay Lyons, #525) — see `RVS_Plan.md` decision log, Sep 11 2026. This is a real-world action (get quotes, bind a policy) outside what a commit to this repo can do; it is recorded here so it is scheduled instead of silently assumed inside the $250/mo line.

**Stripe** takes 2.9% + $0.30 per charge, which is a real line once billing exists — about $110/month at $3,300 MRR across fifty invoices.

### Two things worth doing now

**Staging is about 40% of the Azure bill.** Four Standard SWAs and two B1 App Service plans exist because staging mirrors production. Staging does not need an SLA: F1 App Service and Free-tier SWAs would cut roughly $45/month. Small in absolute terms, large as a fraction, and free to do.

**Verify these figures against the Azure pricing calculator before anyone budgets on them.** They are good enough to reason with and not good enough to commit to.

---

## 3. Break-even

**Hard costs — $117 Azure plus ~$250 other — are covered by five shop locations.**

That is the most important number in this document. The business pays for itself almost immediately, which means the question is never survival of the product; it is only ever whether it pays a person.

| Founder draw | MRR needed | Locations at the $65 blended rate |
|---|---|---|
| $5,000/mo | $5,700 | ~88 |
| $8,000/mo | $8,800 | **~135** |
| $12,000/mo | $12,900 | ~198 |

---

## 4. Revenue scenarios

The clock starts at **first paying customer**, not today. Build items 1–2 are two to three sprints, so demo-ready around December 2026 and first revenue around February–March 2027 after the sixty-day free period in `Marketing/RVS_GoToMarket.md`.

Assumptions common to all three, and the ones to argue with:

- Founder-led sales only. No paid marketing, no hires.
- In-person within ~120 miles first, remote onboarding after.
- A founder selling this way closes **two to four locations a month at best**, and the local cluster is perhaps 15–25 reachable businesses.
- Blended revenue lands near **$65/location** in every mix, because the bands and the mobile rate roughly cancel.

### A — Base case

The local cluster converts steadily. No large group lands; the biggest win is a four-store.

| | Locations | MRR | Profit before founder pay |
|---|---|---|---|
| M6 | 6 | $394 | ~$0 |
| M12 | 18 | $1,182 | $741 |
| M18 | 32 | $2,088 | $1,589 |
| M24 | 50 | **$3,270** | $2,640 (81%) |

### B — Good case

The live dealer-group prospect converts and expands, and a second 25-location group signs in year two.

| | Locations | MRR | Profit |
|---|---|---|---|
| M12 | 28 | $1,792 | $1,350 |
| M18 | 72 | $4,608 | $3,890 |
| M24 | 110 | **$7,190** | $6,313 (88%) |

### C — Weak, but not failure

The local cluster exhausts around twelve shops, no group converts, and ~3%/month churn eats most of the new wins.

| | Locations | MRR | Profit |
|---|---|---|---|
| M12 | 9 | $551 | ~$130 |
| M24 | 14 | **$866** | $443 |

### What the three scenarios actually say

Margins run 80–88% and are close to irrelevant, because the base is small. **The base case does not reach a founder salary inside 24 months.** The good case reaches a $5,000 draw around month 19–20 and $8,000 around month 26–30. That is the financial shape of this business: it covers its own costs almost immediately and pays a founder slowly, in one scenario out of three.

This is not an argument against building it. It is an argument for knowing which lever is actually load-bearing.

---

## 5. The levers, in order of leverage

**1. One dealer group is worth more than the entire local motion.** A 25-location group at $59 is $1,475 MRR — the equal of nineteen independent shops won one at a time, in person, over a year. Scenario B beats scenario A almost entirely on this one fact. It argues for spending disproportionate effort on the live dealer-group prospect, and for treating the independent shops as workflow truth and reference material rather than as the revenue plan.

**2. Price, on new customers only, after ten to fifteen wins.** Every $10 on the shop rate is +$500/month at fifty locations, at zero cost and zero delivery change. The velocity argument for $79 in `Marketing/RVS_Positioning.md` holds while the product is unproven; it stops holding once there are referenceable customers. The grandfathering promise in `Marketing/RVS_Objections.md` is what makes the increase clean rather than a betrayal — honor it.

**3. Churn is the quiet killer at this size.** At 3%/month on fifty locations you lose one and a half a month, so roughly two new locations a month go to standing still — and founder-led selling tops out at two to four. That is the exact mechanism by which scenario A decays into scenario C. Group locations churn materially less than solo operators, which is the second reason lever 1 dominates.

Lever 1 is where the effort goes. Levers 2 and 3 are things not to get wrong.

---

## 6. The ceiling

Stated once, so it is a known fact rather than a discovery in year three.

**These market figures are estimates and are not sourced from the repo — verify against RVDA data before relying on them.** On the order of 3,000 dealer service locations in the US, a few thousand independent RV repair shops, and perhaps 5,000–10,000 mobile technicians.

At an aggressive 10% penetration and current prices, that is **under $1M ARR.** Intake plus an emailed packet at $79 is a good small business. It is not a venture-scale one, and no amount of execution inside the current scope makes it one.

Getting past that ceiling means adjacent verticals (`ARCHIVE/Marketing/RVS_MultiIndustry_Expansion.md`) or the warranty and claims ideas in `Other ideas.md`. Neither is in scope, neither should be started now, and both are governed by `RVS_Archive_Index.md` and the scope filter in `RVS_Plan.md` if they ever return.

The ceiling is not a reason to stop. It is a reason not to hire against a revenue curve that was never going to arrive.

---

## 7. Assumption register

Change a number here and the scenarios move. Nothing else in this document is load-bearing.

| Assumption | Value | Confidence |
|---|---|---|
| Cost per service request | $0.03 | Medium — derived from listed model pricing and estimated token counts, never measured |
| Requests per location per month | 60 | **Low — pure guess.** The first three shops settle this |
| Fixed Azure, both environments | $117/mo | Medium — from declared SKUs, not a billing export |
| Non-Azure fixed cost | $250/mo | Low-medium |
| Blended revenue per location | $65 | High — arithmetic on decided prices |
| Founder close rate | 2–4 locations/month | Low — no closes yet |
| Reachable local cluster | 15–25 businesses | Low |
| Monthly churn | 3% | Low — SMB SaaS convention, not observed |
| US serviceable locations | ~6,000 + 5–10K mobile techs | **Low — unsourced** |

**The two that matter most are the two least known:** requests per location, and the founder close rate. The first three paying shops resolve both. Re-run this document then, and treat the version above as what was believed before there was any evidence.
