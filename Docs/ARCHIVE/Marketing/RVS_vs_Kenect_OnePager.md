# RVS vs. Kenect — One-Pager

**For: Internal study & sales conversations** | **Audience: You, then dealer GMs**

---

## The 10-Second Answer

> **"Kenect handles the conversation. RVS captures what the technician needs to fix it — and turns every repair into a data point your DMS doesn't have."**

Memorize this. It's your opening line in every dealer meeting where Kenect comes up.

---

## Who Kenect Is (Know Your Opponent)

- ~10,000 dealerships across auto, RV, marine, powersports, equipment, outdoor power
- ~10 years in RV via RVDA; partnered with Winnebago, IDS, Lightspeed
- Acquired Auto Labs (Mar 2025) → Service AI, Voice AI, automated scheduling, video MPI, recall mining
- Pleasant Grove, Utah (your neighbor)
- Core DNA: **text messaging + reputation management** with AI bolted on top

---

## The Overlap Map (Commit This to Memory)

| Capability | Kenect | RVS |
|---|---|---|
| 2-way customer texting | **Core** | No (transactional only) |
| Google reviews / reputation | **Core** | No |
| Text-to-pay | **Core** | No |
| Voice AI receptionist | Yes | No |
| Appointment scheduling | Yes | Phase 4 |
| **Structured intake (VIN, photos, AI category)** | **No** | **Core** |
| **Pre-arrival diagnostic data for techs** | **No** | **Core** |
| **Section 10A repair outcomes** | **No** | **Core** |
| **Cross-dealer asset ledger / failure data** | **No** | **Core moat** |
| **Offline-first technician mobile app** | **No** | **Core** |

**Punchline:** Kenect = conversations. RVS = structured data. A dealer should run both.

---

## Why Kenect Is a Real Threat (Don't Pretend Otherwise)

1. **They own the "service efficiency" narrative** — your buyer has heard the pitch
2. **They own the budget slot** — $300–500/mo already spent, you compete with leftovers
3. **They're moving toward you faster than you toward them** — could ship a basic intake form in 6–12 months via Auto Labs team
4. **OEM relationships** — Winnebago is theirs; data-licensing path you want is partly blocked
5. **RVDA + 10-year trust** — you're the unknown

## Where They're Genuinely Weak (Exploit These)

1. **No structured service-event data** — conversations evaporate, your data accumulates
2. **Voice AI / scheduling is auto-DNA** — built for 1-hour oil changes, not slide-system diagnosis
3. **Communication layer, not workflow layer** — advisor still logs into DMS to do real work
4. **No technician-facing product** — your `MAUI.Tech` app is uncontested
5. **Horizontal & shallow** — six verticals from one product; nothing RV-specific in their model

---

## Positioning Rules

- **Don't compete with Kenect — coexist.** "Keep Kenect, we're complementary."
- **Lead with the technician app, not the customer intake form.** Intake portals are commoditized; offline-first tech app + Section 10A is not.
- **Don't claim "no competitor has tackled this."** Name Kenect, frame complementarity, build credibility.
- **Get one OEM design partner before they lock in more** (Grand Design, Forest River, or Thor — Winnebago is gone).

---

## Honest Caveats (Don't Sell Past These)

- **Data moat is real but slow.** 27K events/yr ≠ OEM-grade analytics. Need 100K+ before Winnebago/Thor pays.
- **"In front of the DMS" is crowded.** Both you and Kenect use that line. Differentiation must be *structured-data-for-technicians*, not *not-the-DMS*.
- **The race that matters:** Kenect shipping a generic intake form in 2027. Your defense is the asset ledger filling fast + OEM relationships locked in first.

---

## Messaging Strategy: Build *Less*, Integrate *Smarter*

**Path chosen: Minimum viable transactional messaging + clean Kenect integration seams.**

### KEEP
- Submission email + SMS with magic-link
- Status-change SMS (transactional only)
- ACS as default provider
- TCPA timestamped opt-in
- Magic-link status page (passive, read-only)

### CUT or DEFER from MVP
- ❌ 2-way SMS reply inbox
- ❌ "Send SMS to customer" button in dashboard (was TBD in FR-009)
- ❌ Chat dialog on status page (was TBD in FR-008)
- ❌ Advisor-composed ad-hoc messages
- ❌ Broadcast / marketing / review-request automation

**Why:** Every messaging feature you build is a feature Kenect already does better. You will lose. Save the engineering for the technician app and asset ledger.

### BUILD FOR INTEGRATION (do this now)
1. **Per-tenant `NotificationProvider` enum**: `RvsNative` | `KenectWebhook` | `Disabled`
2. **Outbound webhook** on SR submission, status change, note add — structured payload with SR ID, customer phone, magic-link, message text
3. **Customer-facing message text as first-class SR field** (template fires through whichever channel is configured)
4. **`SuppressOutboundCustomerMessages` flag** in `TenantConfig` — kill switch for native delivery
5. **Plain, dealer-branded message bodies** — no "RVS" branding, so it reads natural when re-routed through Kenect's thread

---

## PRD Edits To Make

| File | Change |
|---|---|
| FR-008 | **Remove TBD** about chat dialog |
| FR-009 | **Remove TBD** about manual SMS send; replace with "ad-hoc messaging is handled in dealer's existing tool (Kenect, Podium, etc.)" |
| FR-016 | **Tighten:** add explicit non-goals (no 2-way SMS, no broadcast, no marketing, no in-dashboard composition) |
| **NEW FR-016a** | **Outbound integration webhook** — structured event payload + `SuppressOutboundCustomerMessages` toggle |
| Pricing | Frame Pro/Enterprise as **complementary to Kenect spend**, not competitive with it |

---

## Sales Objection Handling

| Dealer says... | You say... |
|---|---|
| *"Doesn't Kenect already do this?"* | "Kenect texts your customer. We tell your technician what's actually broken before the rig rolls in. Different layer." |
| *"We already pay Kenect."* | "Good — keep them. We send our notifications through their thread via webhook. You pay both, but for different jobs." |
| *"Can you message customers from the dashboard like Kenect does?"* | "No, and on purpose. That's Kenect's job. Ours is the structured intake and the technician's outcome data — things Kenect doesn't capture." |
| *"What if I don't have Kenect?"* | "We send transactional emails and texts natively. The day you add Kenect or Podium, we route through them with one config change." |
| *"Kenect has Voice AI now too."* | "Right — for booking oil changes. RV diagnostics need photos, video, and slide-system context. Voice AI can't capture a grinding noise; our intake wizard can." |

---

## The Mental Model to Hold

```
┌─────────────────────────────────────────────┐
│  DEALER SERVICE STACK                       │
├─────────────────────────────────────────────┤
│  Kenect      → conversations, reviews, $$$  │
│  RVS         → structured intake + tech app │
│  DMS         → accounting, parts, warranty  │
└─────────────────────────────────────────────┘
       (three layers, three jobs, no overlap)
```

If a dealer ever asks you to draw it, draw this.

---

## The One Thing That Should Worry You

Not Kenect today. **Kenect + a generic intake form feature shipped in 2027** through 10,000 dealer relationships in a quarter.

**Your only defense:** asset ledger filling fast (architecturally hard for them to retro-build under thousands of customers) + OEM exclusivity-flavored relationships locked in first.

**That is the actual race.** Not feature parity. Not pricing. Data depth + OEM trust, before they notice.
