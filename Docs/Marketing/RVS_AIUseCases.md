# AI in RV Intake

RV Intake uses AI in two places:

1. **In the Intake app**, to help the customer describe their RV and its problem quickly and completely, on a phone.
2. **In building the packet**, to give the service department more than the customer said: a structured first read of the problem, plus facts taken from the photos.

Every AI step has a fallback. If the model is slow or unavailable, the customer can still type, and the packet still goes out. AI never blocks a submission, and it never replaces the customer's own words: the packet always carries the complaint word for word.

State as of September 26 2026, including issue #772 (photo-grounded assessment).

---

## 1. Intake app: helping the customer

| # | Feature | Wizard step | Model / service | What it does | Benefit to the customer | If AI is unavailable |
|---|---|---|---|---|---|---|
| 1 | **Read the VIN from a photo** | 3. VIN | Azure OpenAI **gpt-4o** (vision) | Reads the 17-character VIN from a photo of the VIN plate or label. The VIN is then decoded to year, make and model by the NHTSA vPIC database (not AI). | No typing a 17-character code on a phone, and no transcription errors. | The customer types the VIN. |
| 2 | **Speak the VIN** | 3. VIN | Azure OpenAI **Whisper** (speech-to-text) | Transcribes the spoken VIN; a rule-based cleaner turns the transcript into a VIN. | Hands-free entry, for example while standing at the RV. | The customer types the VIN. |
| 3 | **Describe the problem by voice** | 5. Issue | **Whisper**, then **gpt-4o** | Whisper transcribes the recording. gpt-4o turns the transcript into a clear written description the customer can review and edit. | Talking is easier than typing a paragraph on a phone, and the result reads cleanly. | The customer types the description. |
| 4 | **Tidy a typed description** | 5. Issue | Azure OpenAI **gpt-4o** | Cleans up spelling and rambling in what the customer typed. The original is kept word for word. | The shop gets a readable description, and the customer doesn't need to write well. | A rule-based cleanup, or the text exactly as typed. |
| 5 | **Service availability check** | 5. Issue | **gpt-4o** categorization, used only when no category was chosen | Works out the kind of problem and checks it against the services this location offers. | The customer learns straight away if the location doesn't do that kind of work, before finishing the form. | A keyword-based categorizer; otherwise the check is skipped. |
| 6 | **Follow-up questions for this problem** | 6. Questions | Azure OpenAI **gpt-5** (the reasoning-model deployment feature 8 uses, at minimal effort; [#783](https://github.com/markarnoldutah/RVS/issues/783)) | Writes diagnostic questions with tap-to-answer options for the chosen category, the description and the RV. It may add one short tip, shown as a *Suggestion*. | The customer answers the questions a technician would ask on the phone, without the phone call. | A fixed question bank for each category. |
| 7 | **Pre-fill the issue category** | 5. Issue | **gpt-4o** categorization | Suggests a category from the description as the customer types, marked with an *AI suggested* chip. | One less menu to think about; the customer can still change it. | The category field stays whatever the customer last chose. |
| 8 | **Pre-fill urgency and RV usage** | 5. Issue | **gpt-4o** | Infers "How urgent" and "How is the RV used" from the description, each marked *AI suggested*. | Two fewer choices to make; both stay editable. | The fields are left for the customer to set. |

The category, urgency and RV-usage suggestions are always editable — the *AI suggested* chip disappears the moment the customer changes the value, and their choice is what reaches the packet.

---

## 2. Packet: insights for the service department

The packet is the one-page summary emailed to the service department, with a PDF attached. These insights are produced after the customer submits, in the background. The customer never waits for them.

| # | Insight | Where it appears in the packet | Model / service | What it gives the reader | Benefit to the service manager | If AI is unavailable |
|---|---|---|---|---|---|---|
| 7 | **Issue** (clean restatement) | *Issue* section, labelled AI-generated, just above the customer's own words | From features 3 and 4 (**gpt-4o**) | A clear version of the complaint. | The problem is readable in seconds, and the verbatim complaint sits directly below to check against. | The section is omitted; the verbatim complaint is always shown. |
| 8 | **Preliminary assessment** | *Preliminary assessment* section, labelled AI-generated | Azure OpenAI **gpt-5** (a dedicated reasoning-model deployment, US data zone) | Probable cause, a confidence rating (high / medium / low), up to 3 possible fixes to verify (most plausible first) and up to 5 likely parts as generic names. It uses the category, the RV, the description, the answers to the questions and the photos (feature 9). | A first read of the job before anyone touches the unit, to help plan the diagnosis, the parts and the bay time. | A per-category table of the most common causes, marked *Low* confidence. |
| 9 | **From photos** (new, #772) | Inside *Preliminary assessment*, under **From photos** | **gpt-5**, in the same call as feature 8 | From up to 5 of the customer's photos:<br>• **data plates** (component, manufacturer, model, serial);<br>• **fault codes** shown on displays;<br>• **visible observations** (water staining, torn fabric, corroded terminals).<br>Each line cites its photo, for example *(photo 2, fridge-plate.jpg)*. | The model and serial needed to order the right part, and the fault code, without asking the customer again. Each finding can be checked against its photo in seconds. | The block is omitted and the rest of the packet is unaffected. |
| 10 | **Equipment lines in the DMS paste block** | *Copy & paste into your DMS* block | Taken from feature 9 (no extra AI call) | `EQUIPMENT:` and `FAULT CODE:` lines after the complaint. | The model, serial and code go into the dealer management system (DMS) with the complaint, with no retyping. | The lines are omitted. |

### What the assessment is not

- **Advisory, not a diagnosis.** The packet says so. Fixes are *possible*, never *recommended*, because nobody has inspected the unit yet.
- **No money.** No prices, labor times, costs or warranty judgements.
- **No part numbers invented.** Likely parts are generic names. A model number in *From photos* was read off the plate in the photo; the model didn't suggest it.
- **Only what is visible.** Photo findings never claim hidden or internal damage. People, faces, licence plates and addresses are ignored. An unreadable character is left out rather than guessed.
- **It can decline.** When there is too little to go on, the model says so. The packet then shows no cause or fixes, but still shows anything read from the photos.

---

## 3. Guardrails that apply throughout

- **Labelled.** Every AI-written section of the packet carries an *AI-generated* tag.
- **The customer's words are always kept.** The verbatim complaint is never shortened or rewritten.
- **Fallbacks everywhere.** Each feature above has a non-AI path, and a failed AI call never costs the customer their submission or the shop its packet.
- **Data handling.** Photos go to the model as image data, never as links to storage. gpt-5 runs on a US data-zone deployment. The customer's text is treated as data, not as instructions to the model.
- **Cost.** About $0.014–0.021 per request with 5 photos: at most about $6.30 per location per month at the 300-request fair-use cap. These are estimates, to be confirmed in staging.

---

## 4. Known gaps

| Gap | Effect |
|---|---|
| The one-line note that can open *Preliminary assessment* (a requested service is not offered at this location) is written by fixed rules, not AI — and the same field can carry text an advisor typed by hand in the manager app. Both sit under the *AI-generated* label anyway (filed as [#781](https://github.com/markarnoldutah/RVS/issues/781)). | Non-AI content is mislabelled as AI. |
| The manager app does not show the assessment or the photo findings. | Managers see them in the email and the PDF only; the PDF is linked from the request's detail dialog. |
| Photos uploaded after the packet is first generated, videos and voice notes are not analysed. | Only images present at the first generation feed *From photos*. |
