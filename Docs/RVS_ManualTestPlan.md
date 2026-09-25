# RVS — Manual Test Plan

**Version:** 2.0 · September 24, 2026 (1.0 was September 11, 2026)
**Purpose:** A checkable punch list for manually validating what a pilot customer touches before the pilot starts: the intake app (RV owner), the packet (service manager), the manager app, texting, and the internal provisioning tool. This is not an automated suite. That lives in `Tests/RVS.Domain.Tests`, `Tests/RVS.API.Tests` and `Tests/RVS.UI.Shared.Tests`. This is what a person runs through with a phone, a browser, a printer and a real inbox.

Requirement references (`A-n`, `B-n`, `C-n`, `X-n`, `P-n`) point back to `RVS_Spec.md`. That document is the source of truth for *what* each behavior should do. This document is *how to check that it happened*.

Each test is a checkbox. Check it off only after you see the described outcome yourself, not after reading the code.

---

## What changed since 1.0

Version 1.0 was written against the build of September 10. Since then:

- **Rewritten because the behavior changed:** A-7 returning-customer prefill is **deferred** (#673), so a second intake with the same email now expects *no* prefill. The packet email no longer embeds photos. They travel as attachments, and the body lists them by name (#580). C-7 is no longer anonymous single-use links. It is authenticated deep links into the manager app (#498). Accepted attachment types follow the build, and the gap with Spec A-6 is listed in "Known gaps".
- **New sections:** A-2 contact preference, opt-outs and confirmation routing (#662, #673); A-14 advisor intake invites, customer side and manager side (#664, #666, #693); A-15 site identity and policies (#680, #695); the remembered status link (#716); the packet's preliminary assessment, Issue/Complaint split and local-time Received line (#507, #601, #506); manager app sign-in, session, board, disposition, status notes, location packet settings, attachments and disabled-tenant handling (#445, #447, #616, #620, #625, #699); texting keywords (#665); the platform admin tool (P-1 to P-12, #563, #647); the Denim & Rust theme (#702, #727).
- **Moved to "Known gaps":** tests for features the Spec requires but the build does not have yet. They stay visible so nobody checks them off by accident.

---

## How to run this plan

- **Environment.** Run against staging (`staging.rvintake.com`, `manager-staging.rvintake.com`, `go-staging.rvintake.com`) unless a test says otherwise. Hostnames below are written for production. Substitute the staging names.
- **Record the environment's texting state before starting.** Texting is off until the toll-free number is verified (`AzureCommunicationServices:Sms:Enabled`, go-live item G-4). With it off, every test marked **[SMS]** is skipped and noted as skipped, and the email-fallback tests run instead.
- **Accounts you need:**
  - a manager-app user whose role has `intake-invites:send` (e.g. `dealer:owner`)
  - a second user in the same tenant whose role does not have it (e.g. `dealer:readonly`)
  - a user in a **second tenant**, for the tenancy check
  - the platform-admin account (MFA on, `sub` on `Admin:AllowedUserIds`)
- **Devices.** Use a real phone for the intake sections, at least one iOS and one Android. A desktop browser is a second pass, not a substitute.
- **Inbox.** Use a real inbox for packet delivery, in a mail client that renders HTML (Gmail, Outlook), plus a plain-text view if one is available.
- **Printer.** Print at least one PDF on a real shop-style printer in greyscale. A PDF viewer preview is not a substitute for B-3.
- **Order.** Run the intake happy path (1.2 to 1.8) before any edge-case section. The edge cases assume a working baseline.
- **Failures.** File a GitHub issue for every failed checkbox, tagged with the requirement it violates, before checking it off as "fixed."

---

## Part 1 — Intake app, from the RV owner's perspective

### 1.1 Access and entry (A-1)

- [ ] Open the site root (`rvintake.com`). Confirm it shows a hero band and two equal tiles, one to start a request (`/intake`) and one to check a request's status (`/status`).
- [ ] Open an intake link (`rvintake.com/{locationSlug}`) with no prior login or account. The landing step loads directly and shows the location name, plus the dealership name when the two differ.
- [ ] Confirm the landing step previews what the form will ask for and shows a single full-width start button with a time estimate.
- [ ] Confirm no password, magic link or account-creation step appears anywhere in the flow.
- [ ] Submit the form rapidly many times in a row from one IP (or reload and resubmit). The per-IP rate limit eventually blocks further submissions with a clear message, not a silent failure or a 500.
- [ ] Reload the page mid-form. The app either restores progress or restarts cleanly, and never leaves a blank or broken screen.
- [ ] Press **Continue** at the bottom of a long step. The next step opens scrolled to the top. On steps 2–5, the cursor lands in the first empty text field and skips any field already filled.
- [ ] Type in a field on the same step. The page never jumps or re-scrolls while you type.

### 1.1b Channel-tagged links (A-13)

- [ ] Open the short link (`go.rvintake.com/{locationSlug}`). It redirects to the intake form and the address bar ends with `?src=print`.
- [ ] Open it with `?src=qr`. The redirect carries `src=qr` through.
- [ ] Open it with a channel nobody has defined (`?src=nfc`). It still redirects, tagged `nfc`, and does not error.
- [ ] Open it with rubbish (`?src=<script>`). It still redirects, tagged `other`.
- [ ] Open it with a slug that does not exist. It still redirects, and the **intake app** shows its own "location not found" page. A 404 from the redirect itself is a failure.
- [ ] Download the QR code from the manager app's Locations page and scan it. It goes through the short link tagged `src=qr`, not straight to the intake host.
- [ ] Submit one request each from `qr`, `textrepl` and a bare link, then read `GET api/locations/{id}/intake-sources`. Each submission is counted under the channel you used, and the bare one under `print`. (API only. There is no dealer-facing screen for this report yet.)
- [ ] Paste the short link into iMessage but **do not send it**, then check the report. The preview fetch appears in the raw hit count and **not** in the reported hit count. No dealer-facing number presents raw hits as "opens."

### 1.2 Contact details and notification preferences (A-2)

- [ ] Step 2 shows the two notification opt-outs (*Do not send text messages*, *Do not send email*) **above** the preferred contact method.
- [ ] Where texting is offered, the copy states how often RVS texts, that message and data rates may apply, and *Reply STOP to opt out, HELP for help*. It also links to `/sms-terms`.
- [ ] Name, phone, email and preferred contact method (`Phone` / `Text` / `Email`) are all required before you can continue. Phone is required even when the preference is Email.
- [ ] Enter a malformed email (`bob@`, `bob.example.com`) and a short phone number. Each field shows its own error and you cannot continue.
- [ ] Tick *Do not send text messages*. The **Text** option is disabled. If Text was selected, the selection is cleared.
- [ ] Tick *Do not send email*. The **Email** option is disabled. If Email was selected, the selection is cleared.
- [ ] With both opt-outs ticked, **Phone** is still available and the step can be completed.
- [ ] (Optional, API) Send a hand-built `POST api/intake/{slug}/service-requests` that pairs `Text` with an SMS opt-out ticked in the same request. You get a **422**, not a 500. Do the same with `Email` and the email opt-out ticked.

### 1.2b Vehicle details (A-3, A-10)

- [ ] Enter a valid, real VIN. Make, model year and type are decoded and shown (A-3).
- [ ] Enter an invalid or garbled VIN (too short, wrong characters). The form does **not** block submission. It falls back to asking for make, model and year manually.
- [ ] Switch to airplane mode immediately after entering a VIN. The decode fails gracefully and the rest of the form stays usable.
- [ ] Photograph a clear, well-lit VIN plate with the VIN-photo control. A VIN is extracted and fills the field. On a very confident read, the decode also fires by itself.
- [ ] Photograph a VIN plate at a bad angle or with glare. A low-confidence result is discarded rather than filling the field with garbage, and the field stays editable either way.
- [ ] After a VIN fills from a photo, edit it by hand. Your edit is kept and not overwritten.
- [ ] Try the VIN-photo capture with no network. The form falls back to manual VIN entry and still lets you submit.

### 1.2c Returning customer (A-7, deferred)

A-7 prefill is deferred (Spec A-7, #673). These tests check that it stays off.

- [ ] Submit once as a new customer. Then start a second intake at the same location with the **same email**. Name, phone and VINs are **not** prefilled, and every field starts empty.
- [ ] On the second intake, leave both opt-outs unticked after having ticked *Do not send text messages* on the first. In Cosmos `customer-profiles`, the SMS opt-out is **still set**, because intake sets an opt-out but never clears one.
- [ ] On that second intake, choose **Text** as the preferred contact. The submission is accepted (no 422), and the confirmation goes by **email**.

### 1.3 Problem description and attachments (A-5, A-6)

- [ ] Type a realistic 3–5 sentence description. No character limit is hit early.
- [ ] Open the issue-category list. All 13 categories are present, sorted alphabetically.
- [ ] Attach a `.jpg` and a `.png`. Both preview and upload.
- [ ] Attach a short `.mp4`, and a `.mov` recorded on an iPhone. Both are accepted, and the step shows how long a clip fits under the size limit.
- [ ] Attach a file of a type the app does not take (`.docx`, `.zip`). The app rejects it with a clear message rather than dropping it silently or crashing.
- [ ] Attach a `.m4a` or `.wav` audio clip and record what happens. Spec A-6 lists both, but the build does not accept them. See "Known gaps."
- [ ] Try to attach an 11th file. The 10-attachment cap is enforced with a visible message.
- [ ] Try to attach a file over 25 MB. The app rejects it before or during upload with a clear message, not a spinner that never stops.
- [ ] On an iPhone that saves photos as HEIC, attach a photo straight from the Camera app, not converted first. It uploads, and it appears in the packet as a normal JPEG (see 2.4).
- [ ] Turn off Wi-Fi and cellular in the middle of uploading a large photo. The app shows an upload error rather than appearing to succeed while the photo is missing.

### 1.4 Voice input (A-9)

- [ ] Tap the microphone on the description field. A pulsing dot and an mm:ss timer show while it records.
- [ ] Keep recording past two minutes. A countdown shows, and recording stops by itself at the 2-minute cap.
- [ ] Speak a problem description and stop. The transcript appears in the field after a short delay.
- [ ] Typing stays available and works at all times, whether or not you ever touch the microphone.
- [ ] Deny microphone permission when the browser asks. You can still type, and no blocking error appears.
- [ ] Speak a VIN aloud with the voice control near a VIN field. Spoken punctuation and spacing ("one two three, dash, A") are cleaned up sensibly, or at least the field does not break.
- [ ] Cause a transcription failure (airplane mode while recording, or gibberish). The field is left unchanged and no blocking error appears.
- [ ] Dictate a rambling description, then continue. Note your exact words: the packet's **Reported issue** must show them verbatim, not the cleaned-up version (see 2.2).

### 1.5 AI assistance (A-4, A-5, A-11, A-12)

- [ ] After you enter a description, 2–4 follow-up questions appear, relevant to the problem (a slide-out problem gets slide questions).
- [ ] Answer the follow-up questions. The answers show up in the packet's diagnostic Q&A (Part 2).
- [ ] Cause an AI failure for the follow-up questions (airplane mode at the right moment, if you can reproduce it). The fixed questions for that category appear instead of an empty or broken section.
- [ ] An AI-suggested category appears and can be changed. Pick a category different from the suggestion. **Your** choice is what reaches the packet.
- [ ] The "Suggested" urgency and usage chips (A-11) are clearly labeled as suggestions, and ignoring or dismissing them does not block submission.
- [ ] Describe a job clearly outside the location's enabled capabilities. A non-blocking capability alert appears (A-12), and you can still submit.

### 1.6 Submission and confirmation (A-8, A-2 routing)

- [ ] Submit a complete, valid intake. Success comes back quickly (spec target is P95 under 2 s), without waiting on the packet or the email.
- [ ] On the review step, tap **Submit** and then **Back** immediately. Exactly one service request is created.
- [ ] The confirmation screen shows a status link and says a confirmation is coming by text or email.
- [ ] Check that the confirmation goes out on exactly one channel, following Spec A-2's routing table:
  - [ ] Preferred **Email** → one confirmation email, no text.
  - [ ] Preferred **Phone** → one confirmation email, no text.
  - [ ] Preferred **Text** with texting **off** in the environment → one confirmation email, no text.
  - [ ] **[SMS]** Preferred **Text** with texting on and no SMS opt-out → one text, no email.
  - [ ] **[SMS]** *Do not send email* ticked, preference Phone → one text, no email.
  - [ ] Both opt-outs ticked → nothing is sent, and the confirmation screen still reads cleanly.
- [ ] The confirmation email names the dealership, contains a working status link and shows the location's phone number when one is on file. The sender shows as **RV Intake**.
- [ ] Submit, then kill the browser tab before the confirmation screen appears. Open the status link from the confirmation email. The request was still recorded.

### 1.7 Customer status page (X-1, C-9)

- [ ] Open the status link. No login is needed, and the page shows the unit, the submission date, the current status and the location's phone number.
- [ ] On a phone, tap the location's phone number. It opens the dialer (`tel:` link).
- [ ] The page offers no way to reply, message or upload anything (X-1). It is display-only.
- [ ] Walk the request through all six statuses from the manager app (New, In Progress, Waiting on Parts, Waiting on Customer, Completed, Cancelled). Reload after each one. Each status shows with its own colored badge, and none falls back to a plain grey default.
- [ ] Add a customer status note in the manager app (see 3.4) and reload. The note appears. Clear it and reload. The page still reads cleanly, with no empty note box.
- [ ] Close the request without work in the manager app (3.4). The customer sees **Cancelled** only, never the internal reason code.

### 1.7b Remembered status link (X-1, #716)

- [ ] After submitting on a phone, go to `rvintake.com` and tap the status tile. The request's status page opens straight away, without asking for the confirmation number.
- [ ] Open `rvintake.com/status?manual=true`. The form for looking up a different number appears.
- [ ] Load a different request's status link on the same device, then open `/status`. The most recently loaded link opens.
- [ ] Use an expired or unknown token (edit a character in the URL), then open `/status`. The bad token is forgotten, and the lookup form appears instead of an error loop.
- [ ] In a private or incognito window, `/status` shows the lookup form and does not break.
- [ ] The privacy policy has an "On your device" section that describes this.

### 1.8 Advisor invite, customer side (A-14)

Create the invites from the manager app (3.6) first.

- [ ] Open a texted invite link (`go.rvintake.com/{slug}?src=advisor&inv=…`). The form opens with the caller's first name and phone prefilled. Other fields are empty.
- [ ] Open an emailed invite link. First name and email are prefilled.
- [ ] Paste the invite link into iMessage without sending it, then open it for real. It still prefills, because opening or previewing an invite does not use it up.
- [ ] Submit through the invite. The request appears in the manager app tagged `src=advisor`, with the advisor on it.
- [ ] Open the same invite link again after submitting. It lands on a working, **blank** intake form, not an error.
- [ ] Open an invite with a mangled token (change one character). You get a blank, working form.
- [ ] Open a valid invite on a different location's slug. You get a blank, working form.
- [ ] (If you can age one) Open an invite more than 72 hours old. You get a blank, working form.

### 1.9 Site identity and policies (A-15)

- [ ] Every intake page, the site root included, shows the operator's legal name, **Arnold Digital Solutions**, in the footer, with links to `/privacy`, `/terms` and `/sms-terms`.
- [ ] All three policy pages load with no login, link to each other and end with the contact address `support@arnolddigitalsolutions.com`.
- [ ] The privacy policy states retention as the Spec does: contact details and requests are kept while the dealership uses RV Intake or until the customer asks for deletion, and the VIN-keyed service history is kept indefinitely.

### 1.10 Look, feel and accessibility (THEME-1)

- [ ] The intake app uses the Denim & Rust brand: Ink (dark blue) app bar and headings, Rust primary buttons and links, cream page background, **Space Grotesk** type.
- [ ] In the browser's network panel, no request goes to Google Fonts. The font is served from the app.
- [ ] Turn on the high-contrast toggle. The app switches to black, yellow and cyan, and the setting survives a reload. Intake has no dark mode, and none is offered.
- [ ] Every error message shows an icon, so an error never relies on color alone to stand out from a Rust button.
- [ ] The favicon and home-screen icon show the "Service Tag" mark.

### 1.11 Cross-device and edge-case sweep

- [ ] Complete one full happy-path submission on iOS Safari.
- [ ] Complete one full happy-path submission on Android Chrome.
- [ ] Complete one full happy-path submission on a desktop browser.
- [ ] Submit with only the required fields (no VIN, no photos, no voice, no overrides). The packet still renders sensibly (B-2, "degrades if VIN absent").
- [ ] Submit with every optional feature used at once (VIN photo, voice input, 10 attachments, category override, follow-up answers). Nothing breaks.
- [ ] Put obviously malicious input in the description (`<script>`, HTML tags, SQL-like strings). It is accepted as plain text and shows up safely in the packet and on the status page, with no script running and no broken layout.

---

## Part 2 — Packet, from the service manager's perspective

### 2.1 Delivery timing and reliability (B-1, B-4)

- [ ] After a real intake submission, the packet email reaches the configured recipients within about a minute (spec target is P99 under 60 s).
- [ ] The subject matches `[RVS] {category} — {year} {make} {model} — {customer last name}`.
- [ ] Submit with **no VIN and no decoded vehicle**. The subject reads `Unknown vehicle` in that segment (and `Uncategorized` when there is no category), never a blank or broken segment.
- [ ] The sender shows as **RV Intake**, not `DoNotReply`.
- [ ] Set a location to 1 recipient and confirm delivery. Set it to 10 recipients (the maximum) and confirm all 10 receive the packet.
- [ ] Submit with 3 photos while they are still uploading. The packet waits for them (up to about 2 minutes) and arrives with all 3, not with none.

### 2.2 Packet contents — HTML email body (B-2, B-3, C-7)

Open the packet inline in the email body, not the PDF, and check from top to bottom:

- [ ] A small action bar above the masthead offers **In Progress**, **Waiting on Parts** and **Completed** as filled Rust buttons, with an outlined **Open Manager** button on the line below (see 3.3 for what they do).
- [ ] The letterhead reads **RV Intake**. The top right shows `Intake #` with the reference code (the first hyphen-separated segment of the request id, upper-cased) and the Received time.
- [ ] The Received time is in the location's own time zone with a short zone name on a 12-hour clock (e.g. `2026-09-24 8:30 AM MDT`). For a location with no time zone set, it shows `… UTC`.
- [ ] One bold title line reads `Last, First : Year Make Model`.
- [ ] A three-column Customer / Location / Unit band shows name, phone, email and preferred contact; the location; and year, manufacturer and `Serial# (VIN)`. With no VIN, only the Serial# line is dropped.
- [ ] Issue category is shown and matches what was chosen at intake.
- [ ] An **Issue** section (the tidied-up description) appears only when it adds something the complaint does not already say. The same words never appear under two headings.
- [ ] A **Preliminary assessment** section is tagged **AI-generated** and says it is advisory. When the model offers one, it has a probable cause, a confidence (high / medium / low), **possible fixes** (plural, most likely first, never one "recommended" fix) and likely parts as generic names.
- [ ] Likely parts never include a part number or a price.
- [ ] Submit a deliberately vague description ("it's broken"). The assessment either abstains and shows the summary only, or falls back to a low-confidence answer. It does not invent specifics.
- [ ] The customer's phone is a tappable call link and their email a mail link.
- [ ] The **Reported issue — customer's words verbatim** section shows the customer's words **verbatim**, including a dictated description exactly as spoken (from 1.4), not the tidied version.
- [ ] The diagnostic Q&A shows the actual questions and answers from intake.
- [ ] The reported issue and the diagnostic Q&A are both in a typewriter (Courier) face.
- [ ] No photo file names are listed and no images are embedded: the photos are the email's attachments. A **Photos** section appears only for a video link or the "Some images can only be shown in the manager app" note.
- [ ] One click in the **Copy & paste into your DMS** box selects the whole block, fenced by `----- RV INTAKE -----`. This works in Apple Mail and a browser; some webmail clients ignore it and need a click-drag.
- [ ] A paste block is present (2.5).
- [ ] A status link and QR code are present near the end.
- [ ] The packet **never** shows pricing, quotes, labor rates or any other customer's data.
- [ ] Open the same email in a plain-text view. It reads as the paste block followed by the manager-app links as plain lines, not as broken HTML tags.

### 2.3 PDF rendering and print quality (B-3, B-7)

- [ ] Open the attached PDF. It has the same sections in the same order as the HTML body.
- [ ] The PDF has **no** manager-app action bar. Printing the HTML email from the browser also leaves the action bar out.
- [ ] The PDF shows the photos themselves, up to 6 on page one and the rest after a page break.
- [ ] Print the PDF on a real printer in **greyscale**. Every section (header, category, assessment, complaint, Q&A, photos) is readable, with no clipped or overlapping text.
- [ ] Print at **Letter** and at **A4**. Nothing is cut off on either size.
- [ ] The PDF is small (roughly 1.5–3 MB for 6–10 photos) and opens quickly.
- [ ] Open the PDF on a phone. The text is readable without heavy zooming.

### 2.4 Photos and attachments (A-6, B-2, B-3, B-4)

- [ ] Submit a mix of `.jpg`, `.png` and an iPhone HEIC photo. In the email, the PDF comes **first** among the attachments, followed by the photos. The HEIC photo arrives as a JPEG, and every photo shows in the PDF with no blank or placeholder cell.
- [ ] Open an attached photo. It is the right way up, the long edge is about 1600 px, and its EXIF data is stripped, including GPS location.
- [ ] A PNG screenshot arrives as a PNG, not re-encoded to JPEG.
- [ ] Submit more than 6 photos. The PDF shows 6 on page one and the rest on the next page.
- [ ] Submit a video. It is listed in the packet and plays in the manager app's detail drawer.

### 2.5 Paste block for DMS (B-5)

- [ ] Copy the paste block out of the email. It is plain ASCII: no smart quotes, no em dashes, no non-breaking spaces.
- [ ] The order is category, then the customer's verbatim description, then the status link.
- [ ] Submit a description longer than the cap (default 1,000 characters). The block is cut at a word boundary, not mid-word, and never goes over the cap.
- [ ] Paste the block into a real text field (a DMS complaint field if you have one, or at least Notepad or a plain textarea). It pastes cleanly with no mangled characters.

### 2.6 Size ceiling and dropped attachments (B-4)

- [ ] Submit 10 detailed full-resolution photos. The email still sends, never a failed send, with the PDF attached and as many photos as fit. Expect roughly 8 of 10, dropped from the end of the list.
- [ ] When a photo was dropped, the body ends the Photos list with *Some images can only be shown in the manager app. Click here to view.*
- [ ] Follow that link. It opens the board with that request's detail drawer showing **every** photo, including the dropped ones.
- [ ] Submit only a few photos. No manager-app note appears.

### 2.7 Per-location configuration (B-6, C-6)

In the manager app, open **Locations** and edit a location. The drawer has a **Service Packet Email** section.

- [ ] The section shows packet email on/off, a recipient list with add and remove and an "n of 10" count, attach PDF, include photos, a time-zone picker and, under **More packet options**, the paste-block cap, the status-link time-to-live and a logo URL.
- [ ] With packet email on, saving with zero recipients is refused, and so is an 11th recipient or a duplicate address.
- [ ] Type an address without pressing Add, then Save. The address is added, not dropped.
- [ ] Change the recipient list. The next packet goes only to the new list.
- [ ] Turn **Attach PDF** off. The next email has no PDF, and the body and paste block still arrive.
- [ ] Turn **Include photos** off. The next email has no photo attachments, and the rest of the packet is intact.
- [ ] Lower the paste-block cap. The next packet's paste block respects the new cap.
- [ ] Change the time zone. The next packet's Received line uses the new zone.
- [ ] Turn packet email **off**. An intake at that location still succeeds and shows in the manager app, and no packet email is sent.
- [ ] Set the status-link time-to-live and the logo URL, save and reopen. Both values are kept. (Neither one changes the packet yet. See "Known gaps.")

### 2.8 Regeneration and failure handling (B-1)

- [ ] (API) Call `POST api/dealerships/{dealershipId}/service-requests/{srId}/packet/regenerate`. A new packet version is generated and delivered once, not twice.
- [ ] If you can reproduce it in a test environment, force packet generation to fail three times. An alert fires, and the service request itself is **not** rolled back or deleted. (The manager app does not show the failure yet. See "Known gaps.")

---

## Part 3 — Manager app

### 3.1 Sign-in and session (C-7, #498, #616, #625)

- [ ] Open `manager.rvintake.com`. Sign-in goes to the Auth0 page at `login.rvintake.com`, branded for RV Intake, not a generic Auth0 page.
- [ ] `manager.rvserviceflow.com` does not resolve.
- [ ] After the first sign-in on a device, you are asked **Keep me signed in on this device**. Answer **Yes**. Close the browser, come back the next day, and you are still signed in.
- [ ] In the profile menu, change *Keep me signed in* to **Off**. The next time the app opens, it asks you to sign in. The warning about shared computers shows only under **On**.
- [ ] Sign out. It finishes within a few seconds and never hangs.
- [ ] Install the app to the home screen (PWA). It opens on the board.
- [ ] Sign in as the platform admin and disable the tenant (5.2). The tenant's signed-in user gets a single *access restricted* screen with **Sign out** and **Try again**, not a raw 403 at the top of every page. Sign out works from that screen. Re-enable the tenant, tap **Try again**, and the app works again.

### 3.2 Board and list (C-1, C-3)

- [ ] After sign-in, the app lands on the board (`/board`) in the **Actionable today** view: open requests plus anything closed today. The view can be switched off.
- [ ] Drag a card to another column. The status changes, and the customer status page shows the new status after a reload.
- [ ] `/service-requests` lists the location's requests newest first. Filtering by status works for all six statuses, including **Waiting on Customer**.
- [ ] `/service-requests/{id}/edit` no longer exists. The typed URL does not open an edit form.

### 3.3 Status links from the packet email (C-7)

- [ ] In a packet email, tap **Open Manager**. It opens the board with that request's detail drawer open.
- [ ] Tap **In Progress**. A confirmation page appears, and **nothing changes until you tap to confirm**. After you confirm, the status is In Progress, and the activity timeline credits you.
- [ ] Repeat with **Waiting on Parts** and **Completed**.
- [ ] Open an action link in a signed-out private window. You are asked to sign in, and nothing changes until you sign in and confirm.
- [ ] Open an action link as a user of the **second tenant**. The request is not shown and nothing changes (X-4).

### 3.4 Request detail drawer (C-2, C-4, C-9)

- [ ] The drawer shows the request's details, a status select, a priority select, the customer status note, the attachments and the activity timeline.
- [ ] Change the priority. It saves right away and writes *Priority changed to …* to the activity timeline.
- [ ] Write a customer status note. It shows on the customer status page (1.7), and the timeline records it with your name.
- [ ] Save the same note again without changing it. No new timeline entry is added.
- [ ] Type a note over 280 characters. It is refused or cut off at 280.
- [ ] Type a note containing `<` or `>`. It is refused. A note with apostrophes, quotation marks and semicolons is accepted.
- [ ] Use **Close without work…**. The four reasons are Duplicate, Spam, Wrong location and Customer withdrew. Pick one. The request moves to Cancelled, and the timeline records it.
- [ ] After closing, the queue shows *Closed without work · {reason}* and the board card shows *Closed: {reason}*.
- [ ] Move that request back to In Progress. The closing reason is cleared.
- [ ] Attachment tiles show a photo thumbnail for photos, the first frame with a play button for videos, and an icon for PDFs and HEIC files.
- [ ] Click each kind of tile. Photos and files open in a **new tab** without the browser's pop-up blocker stepping in. Videos play in the drawer. The manager app never navigates away.

### 3.5 Locations (C-6)

- [ ] The Locations page lists the tenant's locations and allows creating and editing them. Packet settings are covered in 2.7.
- [ ] Download a location's QR code. It scans to the short link with `src=qr` (1.1b).

### 3.6 Send intake link, advisor side (A-14)

- [ ] **Send intake link** appears beside the location selector on Home once a location is selected, and as a row action on Locations.
- [ ] With texting **off**, the dialog goes straight to email, with a one-line note that texting is awaiting carrier approval.
- [ ] **[SMS]** With texting on, the dialog offers **Text** or **Email**. Switching channels clears the consent tick.
- [ ] **[SMS]** For text: enter a first name and a mobile number. **Send** stays disabled until the consent box is ticked and the number is valid. **What to say** shows the consent script with the store name and the typed number filled in.
- [ ] For email: enter a first name and an email address, and tick *The customer asked for the link by email, at this address*. Send stays disabled until the box is ticked and the address is valid.
- [ ] Send by email. The email arrives from **RV Intake**. It says the customer started a service request with the dealership, that the email comes from RV Intake on the dealership's behalf, that the link works once and expires in 72 hours, and it gives the location's phone number. For a location with no phone, it says *contact {dealer} directly.*
- [ ] Send to an email address the tenant's customer record has opted out of. The send is refused with a clear message.
- [ ] **[SMS]** Send to a number that texted STOP (Part 4). The send is refused with a clear message.
- [ ] **Sent this shift** lists the invites sent this shift. Their status moves from *Sending…* to *Sent* / *Delivered* (text) or *Emailed* (email), and to *Form submitted* once the customer submits (1.8).
- [ ] **Resend** puts the invite's name, channel and number or address back into the form, and requires a fresh consent tick before sending.
- [ ] **Fill it in myself** opens a prefilled intake form in a new tab. If the pop-up blocker stops it, the link is shown with a Copy button instead. This works while texting is off.
- [ ] Sign in as the user **without** `intake-invites:send` and try to send. The dialog says your role doesn't have permission.

### 3.7 Look and feel (THEME-1)

- [ ] The app bar is Ink (dark blue), **not** Rust, in every mode, and reads **RV Intake Manager** with the badge.
- [ ] The theme button cycles Light → Dark → High contrast. The choice survives a reload, and the light palette never flashes first, including on the return from Auth0 sign-in.
- [ ] The manager background is barely tinted (`#FAF8F3`), not full cream, and there is no mountain photo behind the sign-in states.
- [ ] Every error message shows an icon.

### 3.8 Tenancy (X-4)

- [ ] Signed in as the second tenant, no request, location or customer from the first tenant appears anywhere in the list, board, detail or Locations.

---

## Part 4 — Texting **[SMS]**

Skip this whole part, and record it as skipped, while texting is off in the environment.

- [ ] The confirmation text names the dealership, carries the status link and ends with the STOP/HELP line. A long dealership name is cut short, never the status link.
- [ ] From the customer's phone, reply **STOP** to the RV Intake number. The carrier replies, RVS does not send a second reply, and the customer's record now has the SMS opt-out set.
- [ ] After STOP, submit an intake from that number with preference **Text**. The confirmation goes by email.
- [ ] Reply **START** (or **UNSTOP**). The SMS opt-out is cleared, and the next Text-preference intake confirms by text.
- [ ] Reply **HELP**. Exactly one fixed reply comes back, naming RV Intake, pointing at the dealership for help and repeating STOP.
- [ ] Send any other text ("hi, is my RV ready?"). Nothing is answered or stored.
- [ ] An invite texted to a number that cannot receive it shows as *Not delivered* in **Sent this shift**.

---

## Part 5 — Platform admin tool (P-1 to P-12)

RVS staff only, at `/admin` in the manager app. Run it on a phone at 390 px width (P-8).

### 5.1 Access (P-7)

- [ ] A normal dealer user who opens `/admin` is refused (403). So is the admin account when its `sub` is not on the allowlist.
- [ ] Signing in to the admin account requires MFA.

### 5.2 Tenants (P-1, P-4, P-6, P-8)

- [ ] Create a tenant from a phone. The forms are single-column and usable at 390 px. The suggested id is `ten_{snake_name}`, and it can be edited.
- [ ] The result card lists each step as `created`, shows the intake URL and the set-password link, and each has a **Copy** button. Nothing is emailed automatically.
- [ ] Open the intake URL. The new location's intake form loads with the business and location names filled in.
- [ ] Submit the same form again. Every step reports `already existed`, and nothing is duplicated.
- [ ] Create a tenant with an existing id but a different name. It is rejected.
- [ ] Disable the tenant, giving a reason. Its users are locked out (3.1). Enable it again, and they can get back in.

### 5.3 Locations (P-5)

- [ ] Add a location named `Tucson` to tenant `Blue Compass`. It is saved as `Blue Compass - Tucson` with slug `blue-compass-tucson`.
- [ ] Add a location whose name already starts with the business name. The name is not doubled.

### 5.4 Users (P-2, P-3, P-9 to P-12)

- [ ] Add a user with each offered role (`dealer:owner`, `dealer:manager`, `dealer:advisor`, `dealer:readonly`). The set-password link works, expires after 7 days, and returns the user to the manager app.
- [ ] Use **Reset password link** for an existing user. A fresh link works.
- [ ] The Users page lists the tenant's users with name, email, roles, locations, whether they can sign in, and last sign-in. A user added seconds ago may take a moment to appear.
- [ ] Change a user from `dealer:owner` to `dealer:readonly`. Within an hour (on the user's next token renewal), the user loses owner-only actions such as sending an intake link.
- [ ] Disable a user. That user cannot sign in, and the tenant's other users are unaffected. Enable the user again.
- [ ] Delete a user after the in-page confirmation. The user is gone, and the location's packet recipients are unchanged.
- [ ] Open another tenant's user through a hand-edited URL. You get a 404.

---

## Known gaps — expected to fail or not testable yet

These are Spec requirements the current build does not meet. Run the check anyway, record it as a known failure against the linked issue, and do not check it off.

| Requirement | What the Spec says | What is built | Tracking |
|---|---|---|---|
| C-2 | The detail view renders the packet | The drawer shows request data, not the packet | #443 |
| C-5 | Resend the packet to the configured recipients or an ad-hoc address | No resend anywhere in the manager app. Regeneration is API-only | #444 |
| B-1 | Three failed attempts are shown in the manager app | An alert fires. Nothing shows in the manager app | — |
| B-4 | A hard bounce disables that recipient and notifies the owner | Disabling is built, but nothing receives a bounce yet, so it never fires | — (follow-up to closed #439; see `RVS_PacketComposition.md`) |
| B-6 | Status-link time-to-live and optional logo | Both save from the Locations drawer. Neither is used by the packet or the status token yet | #505 (logo) |
| A-6 | Attachments: jpeg, png, mp4, m4a, wav | The upload step accepts jpeg, png, gif, webp, heic/heif, mp4, mov, webm and pdf, and **not** m4a or wav | — (decide: amend the Spec or the build) |
| A-7 | Returning-customer prefill | **Deferred** by decision. 1.2c checks that it stays off | #673 |
| A-2 | Nothing clears an email opt-out | Known gap by decision. A dealer-side toggle is not built | Spec A-2 |
| A-13 | Submissions by source, shown to the dealer | The report exists only as `GET api/locations/{id}/intake-sources`. There is no dealer screen | — |

---

## Sign-off

- [ ] Every checkbox in Parts 1–5 is checked, marked skipped with a reason (e.g. **[SMS]** while texting is off), or linked to an issue for a known failure.
- [ ] Every row in "Known gaps" has been run and its result recorded.
- [ ] The two ship criteria most relevant to this plan have been re-checked end to end by the person signing off, not just left to the sections above:
  - [ ] "A customer completes intake on a phone and the service manager has a readable email with photos and a printable PDF within a minute." (`RVS_Plan.md`)
  - [ ] "The PDF prints legibly on a shop printer in greyscale." (`RVS_Plan.md`)
