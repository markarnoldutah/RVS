# Toll-free verification: application package (#659)

Two applications with the same content. Microsoft's guide says the **Associated phone numbers** list shows only the numbers in the ACS resource you are filing from. Staging and prod each have their own resource, so each number needs its own application.

| | Prod | Staging |
|---|---|---|
| ACS resource | `acs-rvs-notify-prod-wus3-s01-001` | `acs-rvs-notify-staging-wus3-s01-001` |
| Number | **+1 833 239 8230** | **+1 866 231 9618** |
| Volume | 1,000 / month | 50 / month |

**Portal path:** ACS resource → **Phone numbers** → **Submit Application** (older UI: **Regulatory Documents → Add**).

Every block marked *paste* is written for the aggregator's reviewer. Message text is copied from the code (`IntakeInviteContent`, `ServiceRequestConfirmationContent`, `InboundSmsReplyContent`), and tests pin those strings. Do not reword the samples.

---

## 0. Evidence: capture and host this first

The application is rejected outright if the opt-in URL is missing or unreachable. Capture these at desktop width, about 1280 px, with the browser chrome cropped out. Use a **private window** so device memory (#811) can't prefill anything.

| # | Shot | Where | Must show |
|---|---|---|---|
| E1 | **Intake Step 2, empty** | prod `rvintake.com/{test-location-slug}`, through to Step 2 | Mobile number field, the Notification Preferences disclosure (*"…message frequency varies. Message and data rates may apply. Reply STOP to opt out, HELP for help. Texting terms"*) and the two opt-out boxes. Preferred contact method has **no radio selected**. |
| E2 | **Intake Step 2, Text chosen** | same | Phone entered and **Text message** selected by the customer. Shows that consent is the customer's own action. |
| E3 | **Texting terms** | `https://rvintake.com/sms-terms` | The whole page. It renders in the browser (Blazor), so a no-JS fetch sees only a loading shell. The screenshot covers a reviewer whose tooling doesn't run JS. |
| E4 | **Manager: Send intake link, consent ticked** | local run, see below | First name, mobile number, the ticked box *"I read the consent script and the customer said yes to this text."*, and the **What to say** panel expanded with the script. |
| E5 | **Consent record in the database** | Cosmos Data Explorer → `rvs-db` → `intake-invites` → the doc E4 created | `advisorUserId`, `phone`, `consentCapturedAtUtc`, `sentAtUtc`, `locationId`, `channel: "sms"`. This is the *"screenshot record of opt-in via verbal in your database/CRM… a check box… and the date"* that Microsoft asks for. |
| E6 | **Sent this shift list** (optional) | same dialog after Send | The invite listed with its time. |

**E4/E5 can't be taken from prod or staging.** The consent box only renders when `Sms:Enabled` is true, and both environments are off. Unverified toll-free numbers are blocked anyway, so turning an environment on proves nothing. Run the API locally with SMS on instead:

```bash
AzureCommunicationServices__Sms__Enabled=true \
AzureCommunicationServices__Sms__FromPhoneNumber=+18662319618 \
dotnet run --project RVS.API -lp https
dotnet run --project RVS.Blazor.Manager -lp https
```

Send to **your own mobile only**. The record is written before the send is attempted, so E5 exists even though ACS refuses the send from the unverified number. Delivery will show *failed*. Crop it out of E4/E6, or take E4 before you tap Send.

**Hosting.** The images are served from the Intake app at `https://rvintake.com/compliance/<file>`, the same domain as the website named on the application. They live in `RVS.Blazor.Intake/wwwroot/compliance/` and ship with every Intake deploy. The published service worker leaves `compliance/` alone (it neither precaches it nor answers it with `index.html`), and so does the SPA fallback in `staticwebapp.config.json`. `optin-evidence.png` is all the shots in one tall image: verbal (E4, E5) on top, then web (E1, E2, E3). If the form takes a single opt-in URL, use that one. **Open every URL in a private window before submitting.** Don't move, rename or delete these files until both numbers are verified; the reviewer may come back to them weeks later.

---

## 1. Application type

**Country or region:** United States, plus Canada if offered. Recipients are US/CA mobile numbers, and `PhoneNumberNormalizer` accepts only NANP.

**Associated phone numbers:** the one number in this resource.

**Are you using more than one sending phone number?** Yes. *Paste:*

> This program uses two toll-free numbers, one per environment, for the same use case. +1 833 239 8230 is production. +1 866 231 9618 is our staging (QA) environment. It sends only to RV Intake staff and test phones, and its traffic is a small fraction of production's. Each number lives in a separate Azure Communication Services resource, so each is filed in its own application with identical program details.

---

## 2. Company details

| Field | Value |
|---|---|
| Legal company name | **Arnold Digital Solutions** (doing business as RV Intake) |
| Website | `https://rvintake.com`. The footer links privacy, terms and texting terms, and each one names Arnold Digital Solutions in its opening line (Spec A-15). |
| Address / tax ID | *yours* |
| Point of contact | *you*. **Use an inbox you read daily.** All status updates and any *More info needed* request go there and nowhere else. |

Company details are filed in RVS's own name, with the multi-dealer model stated plainly in the program description. #676 got no reply, so this is the planned fallback. If it's rejected, the reason tells us which of #676's three outcomes we're in.

---

## 3. Program details

**Use case / category:** Customer Care. If that isn't listed, use Account Notifications. **Not** Marketing or Mixed.

### Program description (*paste*)

> RV Intake (rvintake.com), operated by Arnold Digital Solutions, is software that RV dealership service departments use to collect customers' service requests. We send transactional text messages on behalf of the dealership a customer is already dealing with, from one toll-free number shared across the dealerships that use RV Intake. Every message begins with the name of the dealership it is sent for, and our texting terms state that RV Intake sends on the dealership's behalf.
>
> Recipients are only consumers who have (a) asked a dealership service advisor, on a live phone call, to text them a link to start a service request, or (b) entered their mobile number on the dealership's online service-request form and chosen "Text message" as their preferred contact method.
>
> There are two outbound message types, both transactional: (1) a one-time link to start a service request, sent only after the customer's verbal agreement on the call; and (2) a confirmation with a status link after the customer submits a request. We also reply to HELP with a fixed help message. There is no marketing, promotional, lead-generation or third-party content.
>
> Frequency: about one or two messages per service request: one invite (an advisor may resend it if it is lost, and must record consent again to do so) and one confirmation. STOP is honored through the carrier and recorded in our system, which then refuses further sends to that number. Sends are also rate-limited per advisor, per location and per dealership account. Texting terms: https://rvintake.com/sms-terms

### Opt-in type

Select **Verbal** and **Website** if the form allows several. If it allows one, choose **Verbal**. The verbal path is the main one, and the description below covers both.

### Opt-in description (*paste*)

> Two opt-in paths. In both, consent is collected at the moment the number is collected, the disclosure names the third-party sender (RV Intake), and texting is never pre-selected.
>
> 1. Verbal, on a live phone call. A customer calls their RV dealership. The service advisor reads a published consent script: "I can text you a link so you can send us the details and some photos of the problem. It's one text from [dealership name], sent through our RV Intake service, to this number: [number read back]. It's just the one message with a link — you'd only get more texts if you ask for text updates on the form. We won't use it for marketing. Standard message and data rates may apply, and you can reply STOP any time to stop texts, or HELP for help. Is it OK if I send that now?" The advisor waits for a clear yes. In the RV Intake manager app, the advisor then ticks "I read the consent script and the customer said yes to this text." The Send button stays disabled until it is ticked, and the script is displayed beside the box. On send, the system stores a permanent consent record: advisor user ID, consent timestamp, phone number, dealership location and channel. The record never expires. A number that has replied STOP is refused. Proof: screenshot of the consent checkbox with the script, and of the stored consent record.
>
> 2. Website, on the dealership's online service-request form (rvintake.com). On the contact-details step, the customer enters a mobile number. On the same screen, a disclosure states: "We'll confirm your request by text if you prefer text, otherwise by email. Texts are only about your service request; message frequency varies. Message and data rates may apply. Reply STOP to opt out, HELP for help." It links to the texting terms at https://rvintake.com/sms-terms. Texting happens only if the customer actively selects "Text message" as their preferred contact method. No option is pre-selected. A "Do not send text messages" box disables that option. Proof: screenshots of this step and of the texting terms page.

### Opt-in URL / image

`https://rvintake.com/compliance/optin-evidence.png` (from section 0). If the form has a separate terms or privacy URL field: `https://rvintake.com/sms-terms` and `https://rvintake.com/privacy`.

### Opt-out / HELP (if asked separately)

> Opt-out: reply STOP (also honored: UNSTOP/START to resubscribe), handled by the carrier. RV Intake receives the keyword, marks the number opted out and refuses later sends to it. Customers can also tick "Do not send text messages" on the form.
>
> HELP reply: "RV Intake: We send service-request links and confirmations for your RV dealership. For help with your request, contact the dealership directly. Msg & data rates may apply. Reply STOP to opt out."

---

## 4. Volume

**Expected total messages per month:** prod **1,000**, staging **50**.

Basis: `RVS_Money.md` assumes about 60 requests per location per month, and each request sends at most two texts. Pilot scale is a handful of locations, about 250–700 a month, so 1,000 leaves room without inviting scrutiny. Raise it if you expect more than about 8 locations live within six months.

---

## 5. Templates (*paste each one as a separate sample*)

Use realistic values, not `{placeholders}`. Every message is under two GSM-7 segments.

**1. Service-request link (advisor invite, after verbal consent):**

> Acme RV Service: Hi Dana, here's the link to start your service request: https://go.rvintake.com/acme-rv-service?src=advisor&inv=Q2xhdWRlLXNhbXBsZS10b2tlbi0zMmJ5dGVzLWV4YW1wbGU Msg & data rates may apply. Reply STOP to opt out, HELP for help.

**2. Service-request confirmation:**

> Acme RV Service: Thanks for your service request. Status: https://rvintake.com/status/7f3c9a2e5b1d4c88 Questions? Call (801) 555-0142. Msg & data rates may apply. Reply STOP to opt out, HELP for help.

**3. HELP auto-reply:**

> RV Intake: We send service-request links and confirmations for your RV dealership. For help with your request, contact the dealership directly. Msg & data rates may apply. Reply STOP to opt out.

Links use our own domains (`go.rvintake.com`, `rvintake.com`), never a public URL shortener.

---

## Before you click Submit

- [ ] Every evidence URL opens in a private window. Check `https://rvintake.com/compliance/optin-evidence.png`, `/sms-terms` and `/privacy`.
- [ ] E1 shows no preferred-contact radio selected.
- [ ] Contact email is one you read daily.
- [ ] Prod and staging applications are both submitted. Note both submission dates on #659.
- [ ] Don't change the sample strings, `/sms-terms` or the consent script until both numbers are verified. If you must, the tests in `IntakeInviteContentTests` / `InboundSmsReplyContentTests` will remind you.
