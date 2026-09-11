# RVS — Manual Test Plan

**Version:** 1.0 · September 11, 2026
**Purpose:** A discreet, checkable punch list for manually validating the two user-facing halves of the product before a pilot: the intake app (RV owner) and the packet (service manager). This is not an automated suite — see `Tests/RVS.Domain.Tests` and `Tests/RVS.API.Tests` for that. This is what a human runs through with a phone, a browser, and a real inbox.

Requirement references (`A-n`, `B-n`, `C-n`, `X-n`) point back to `RVS_Spec.md` — that document is the source of truth for *what* each behavior should do; this document is *how to check it happened*.

Each test is a checkbox. Check it off only after observing the described outcome directly — not from reading the code.

---

## How to run this plan

- Use a real phone (iOS and Android, at least one each) for the intake sections — the desktop browser is a secondary pass, not a substitute.
- Use a real inbox for the packet-delivery sections — a mail client that renders HTML (e.g., Gmail, Outlook) and, separately, a plain-text-only view if available.
- Print at least one PDF on an actual shop-style printer in greyscale — a PDF viewer preview is not a substitute for B-3.
- Run the full "Intake — happy path" section before any edge-case section; edge cases assume a working baseline.
- File a GitHub issue for every failed checkbox, tagged with the requirement it violates, before checking it off as "fixed."

---

## Part 1 — Intake app, from the RV owner's perspective

### 1.1 Access and entry (A-1)

- [ ] Open the intake link (`rvintake.com/{locationSlug}`) with no prior login or account — form loads directly.
- [ ] Confirm no password, magic link, or account-creation step appears anywhere in the flow.
- [ ] Submit the form rapidly many times in a row (or reload/resubmit) from one IP — confirm the per-IP rate limit eventually blocks further submissions with a clear message, not a silent failure or a 500.
- [ ] Reload the page mid-form — confirm the app fails gracefully (either restores progress or restarts cleanly), never leaving a blank or broken screen.

### 1.2 Contact and vehicle details (A-2, A-3, A-7)

- [ ] Complete the contact step with name, phone, email, and preferred contact method (`Phone` / `Text` / `Email`) — all four are required before advancing.
- [ ] Enter a valid, real VIN — confirm make, model year, and type are decoded and shown (A-3).
- [ ] Enter an invalid/garbled VIN (too short, wrong characters, a truck VIN if the tool has one) — confirm the form does **not** block submission; it falls back to asking for make/model/year manually.
- [ ] Disconnect from the network (airplane mode) immediately after entering a VIN — confirm VIN decode fails gracefully and the rest of the form remains usable.
- [ ] Submit once as a **new** customer, then start a second intake from the same location using the **same email** — confirm name, phone, and previously used VINs are prefilled (A-7).
- [ ] Start a second intake using a **different** email than any prior submission — confirm no prefill occurs and fields start empty.

### 1.3 Problem description and photos/video (A-2, A-6)

- [ ] Enter a free-text description of a problem — confirm no character limit is hit prematurely for a realistic paragraph (3–5 sentences).
- [ ] Attach one photo in each supported format: `.jpg`, `.png` — confirm all preview and upload successfully.
- [ ] Attach a short `.mp4` video and a short `.m4a` or `.wav` audio clip — confirm both are accepted.
- [ ] Attempt to attach an unsupported file type (e.g., `.heic` directly, `.pdf`, `.docx`) — confirm the app rejects it with a clear message rather than silently dropping it or crashing.
- [ ] Attempt to attach an 11th photo — confirm the app enforces the 10-attachment cap (A-6) with a visible message.
- [ ] Attempt to attach a file over 25 MB — confirm the app rejects it before or during upload with a clear message, not a hung spinner.
- [ ] From an iPhone that captures photos as HEIC, attach a photo taken directly in the Camera app (not pre-converted) — confirm it either uploads successfully or is converted/handled without silently disappearing from the eventual packet (see Part 2, 2.4).
- [ ] Turn off Wi-Fi/cell mid-upload of a large photo — confirm the app surfaces an upload error rather than appearing to succeed while the photo is actually missing.

### 1.4 Voice input (A-9)

- [ ] On the description field, use the microphone/dictation control to speak a problem description — confirm the transcript appears in the field after a short delay.
- [ ] Confirm typing remains available and usable at all times, with or without ever touching the microphone control.
- [ ] Deny microphone permission in the browser when prompted — confirm the form still allows typed entry with no blocking error.
- [ ] Speak a VIN aloud using the voice-input control near a VIN field — confirm spoken punctuation/spacing ("one two three, dash, A") is cleaned up sensibly, or at minimum does not crash the field.
- [ ] Simulate a transcription failure (e.g., airplane mode while recording, or speaking gibberish) — confirm the field is left unchanged and no blocking error is shown.

### 1.5 VIN from photo (A-10)

- [ ] Photograph a clear, well-lit VIN plate using the "photo of VIN" control — confirm a VIN is extracted and auto-fills the field.
- [ ] Photograph a VIN plate at a bad angle or with glare — confirm low-confidence results are discarded rather than auto-filling garbage, and the field remains manually editable either way.
- [ ] After an auto-fill (from a clear photo), manually edit the VIN field — confirm the edit is respected and not overwritten.
- [ ] Attempt VIN-photo capture with no network connectivity — confirm the form degrades to manual VIN entry without blocking submission.

### 1.6 Issue category and AI assistance (A-4, A-5, A-11, A-12)

- [ ] After entering a description, confirm 2–4 AI-generated follow-up questions appear relevant to the described problem (e.g., a slide-out problem yields slide-related questions).
- [ ] Answer the follow-up questions and confirm the answers are retained through to submission (spot-check against the eventual packet in Part 2).
- [ ] Simulate an AI failure for the follow-up questions (airplane mode at the right moment, if reproducible) — confirm hardcoded per-category fallback questions appear instead of a blank or broken section.
- [ ] Confirm an AI-suggested issue category appears and can be overridden by the customer — pick a category deliberately different from the suggestion and confirm the override sticks through submission.
- [ ] Confirm the "Suggested" urgency/usage-context chips (A-11) are visibly labeled as suggestions/advisory, not authoritative, and do not block submission if ignored or dismissed.
- [ ] Enter a description for a service clearly outside a location's stated capabilities (e.g., engine transmission rebuild at a mobile RV-appliance tech) and confirm a non-blocking capability alert appears (A-12) — then confirm the customer can still submit anyway.

### 1.7 Submission and confirmation (A-8, X-1)

- [ ] Submit a complete, valid intake — confirm the app returns success quickly (well under a few seconds; spec target is P95 under 2s) without visibly waiting on packet generation or email delivery.
- [ ] Confirm a status link (and/or confirmation email) is presented to the customer immediately after submission.
- [ ] Open the status link — confirm it requires no login, shows the unit, submission date, and current status.
- [ ] Confirm the status page shows no path to reply, message, or upload anything back (X-1) — it is read-only for the customer.
- [ ] If a manager-authored note exists on the request (see Part 2, C-9), reload the status page and confirm the note renders; if none exists, confirm the page still reads cleanly with no empty/broken note section.
- [ ] Submit intake, then immediately kill the browser tab/app before any confirmation screen renders — reopen the status link from the confirmation email (if received) and confirm the request was still recorded (i.e., the `201` truly didn't depend on anything client-side after submit).

### 1.8 Cross-device and edge-case sweep

- [ ] Complete one full happy-path submission on iOS Safari.
- [ ] Complete one full happy-path submission on Android Chrome.
- [ ] Complete one full happy-path submission on a desktop browser.
- [ ] Submit with the minimum required fields only (no VIN, no photos, no voice, no overrides) — confirm the packet still renders sensibly (see B-2, "degrades if VIN absent").
- [ ] Submit with every optional capability exercised at once (VIN photo, voice input, max photos, category override, follow-up answers) — confirm nothing breaks under the combined load.
- [ ] Attempt to enter obviously malicious input in the free-text description (`<script>`, HTML tags, SQL-like strings) — confirm it is accepted as plain text and rendered safely later in the packet/status page (no script execution, no broken layout).

---

## Part 2 — Packet, from the service manager's perspective

### 2.1 Delivery timing and reliability (B-1, B-4)

- [ ] After a real intake submission, confirm the packet email arrives at the configured recipient inbox within about a minute (spec target P99 under 60s).
- [ ] Confirm the subject line matches the pattern `[RVS] {category} — {year} {make} {model} — {customer last name}`.
- [ ] Submit a request with **no VIN and no decoded vehicle** — confirm the subject and packet degrade gracefully (no blank/garbled make-model-year segment).
- [ ] Configure a location with 1 recipient and confirm delivery; reconfigure with the maximum of 10 recipients and confirm all 10 receive the packet.
- [ ] If a bounce-testing recipient is available, use a known-bad address as one of several recipients — confirm only that one address is disabled/flagged and the rest of the configuration keeps working (per-address bounce isolation).

### 2.2 Packet contents — HTML email body (B-2, B-3)

Open the packet as rendered inline in the email body (not the PDF) and confirm, in order:

- [ ] Unit header shows year, make, model, VIN — or degrades sensibly when VIN/decode is absent.
- [ ] Customer block shows name, phone, email, and preferred contact method.
- [ ] Location, submission timestamp, and a short reference code (first hyphen segment of the request id, upper-cased) are all present.
- [ ] Issue category is shown and matches what was selected/overridden at intake.
- [ ] An AI-generated summary appears, clearly labeled as AI-generated, positioned before the customer's own verbatim text.
- [ ] The customer's original description appears **verbatim**, unmodified, in its own section.
- [ ] The diagnostic Q&A block appears with the actual questions and answers captured at intake.
- [ ] Up to 6 photo thumbnails appear on the first "page" of the HTML view; additional photos (if more than 6 were attached) appear in an appendix section.
- [ ] A paste block section is present (see 2.5 below).
- [ ] A status link and QR code are present near the end.
- [ ] Confirm the packet **never** shows pricing, quotes, labor rates, or any other customer's data.
- [ ] Open the same email in a plain-text-only mail client/view — confirm it degrades to a readable paste-block-style body rather than showing broken HTML tags.

### 2.3 PDF rendering and print quality (B-3, B-7)

- [ ] Open the attached PDF — confirm it contains the same content as the HTML body (same composition, not a divergent second template).
- [ ] Print the PDF on a real printer in **greyscale/black-and-white** — confirm every section (header, category, description, Q&A, photos) remains legible with no clipped text or overlapping elements.
- [ ] Print at both **Letter** and **A4** paper sizes — confirm the layout adapts without cutting off content on either.
- [ ] Confirm the PDF is reasonably small (should render quickly and attach without issue given the size-budget rules in 2.6).
- [ ] Open the PDF on a phone (the way a technician actually would) — confirm text is readable without excessive zooming.

### 2.4 Photos and attachments (A-6, B-2, B-3)

- [ ] Submit an intake with a mix of `.jpg`, `.png`, and an iPhone-native HEIC-sourced photo — confirm all appear as real images in both the HTML body and the PDF, not blank/placeholder cells (packet composition should not fail the whole render on one bad image).
- [ ] Submit an intake with more than 6 photos — confirm the first 6 appear on the main packet and the rest appear in the appendix, in both HTML and PDF.
- [ ] Confirm images in the HTML email are backed by time-limited SAS URLs (inspect the image `src`/link, not embedded base64).
- [ ] Wait past a SAS URL's expiration window (or use an intentionally aged link if available) and confirm the expired image link fails gracefully rather than looking like a data-integrity bug, and that the PDF/manager app still has an accessible copy.
- [ ] Submit an intake with a very large batch of photos (near the 10-photo, 25 MB-each cap) — confirm the email still sends and attachments are trimmed per the size-ceiling rule (PDF kept first, photos dropped from the end) rather than the send failing outright (B-4 size ceiling).

### 2.5 Paste block for DMS (B-5)

- [ ] Copy the paste block text out of the email — confirm it is plain ASCII (no smart quotes, no em-dashes, no non-breaking spaces).
- [ ] Confirm the paste block order is: category, then the customer's verbatim description, then the status link.
- [ ] Submit an intake with a very long description that would exceed the configured character cap (default 1,000) — confirm the paste block truncates at a word boundary, not mid-word, and does not silently exceed the cap.
- [ ] Paste the block into a real text field (a DMS complaint field if available, or at minimum a plain textarea/Notepad) — confirm it pastes cleanly with no visible mangled characters or reformatting artifacts.

### 2.6 Size-ceiling and attachment-dropping behavior (B-4)

- [ ] Submit an intake sized to just barely exceed the transport's attachment budget (many large photos) — confirm the email still sends (never a failed send), the PDF is retained if it fits, and photos are dropped from the end of the list in order until it fits.
- [ ] Confirm a dropped photo is still viewable elsewhere (manager app / HTML SAS link) even though it did not fit as an email attachment — i.e., the recipient isn't fully cut off from it.

### 2.7 Per-location configuration (B-6)

- [ ] Change a location's recipient list and confirm the next packet honors the new list (old recipients stop receiving, new ones start).
- [ ] Toggle "attach PDF" off for a location and confirm the next email omits the PDF attachment while everything else (HTML body, paste block) still works.
- [ ] Toggle "include photos" off and confirm photos are omitted from the email while the rest of the packet remains intact.
- [ ] Change the paste-block character cap to a smaller value and confirm truncation respects the new cap on the next submission.
- [ ] Disable a location entirely and confirm no packet is generated/sent for intake at that location (cross-check against the tenant access gate behavior, X-4).

### 2.8 Regeneration and failure handling (B-1)

- [ ] Trigger a manual packet regeneration (if the manager app or an admin path supports it) — confirm the regenerated packet is idempotent for the same `(serviceRequestId, packetVersion)` and doesn't duplicate delivery.
- [ ] If reproducible in a test environment, force packet generation to fail three times — confirm an alert fires and the failure is surfaced in the manager app, and confirm the underlying service request itself is **not** rolled back or deleted.

### 2.9 Status link and one-click actions (X-1, C-7 if enabled)

- [ ] Click the status link/QR from the packet — confirm it opens the same anonymous, read-only customer status page validated in Part 1, section 1.7.
- [ ] If one-click status-update action links are enabled in the deployed environment (C-7), click one (e.g., "In Progress") directly from the packet email without logging in — confirm the status updates and a small confirmation page is shown.
- [ ] Re-click an already-used single-use action link (if C-7 links are single-use) — confirm it fails gracefully (e.g., "link already used/expired") rather than silently reapplying or erroring with a stack trace.

### 2.10 Manager app cross-check (C-1, C-2, C-5)

- [ ] Log into the manager app and confirm the same service request appears in the list, newest first.
- [ ] Open the detail view and confirm it renders the same packet content validated in 2.2/2.3, plus the status control and a resend button — and nothing else beyond that (C-2 scope discipline).
- [ ] Use the resend button to resend the packet to the configured recipients — confirm a duplicate email arrives.
- [ ] Resend the packet to an ad-hoc address not in the location's configured list — confirm it is delivered there without altering the location's stored recipient configuration.

---

## Sign-off

- [ ] Every checkbox in Part 1 is checked, or has a linked issue for a known failure.
- [ ] Every checkbox in Part 2 is checked, or has a linked issue for a known failure.
- [ ] The two ship-criteria items most relevant to this plan are personally re-verified end to end, not just delegated to the sections above:
  - [ ] "A customer completes intake on a phone and the service manager has a readable email with photos and a printable PDF within a minute." (`RVS_Plan.md`)
  - [ ] "The PDF prints legibly on a shop printer in greyscale." (`RVS_Plan.md`)
