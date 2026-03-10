Below is the **Primary Workflow** for BF’s **Patient Check-In + Eligibility/COB** flow, written the way we’d document it for architecture + API design. It’s aligned to your BF invariants: **practice-scoped PHI**, scope from **claims + route**, and eligibility payload storage as references (blob URLs) rather than large payloads in Cosmos. 

---

## Primary Workflow: Patient Check-In for Appointment + Eligibility/COB

### 0) Preconditions (Scope + Access)

1. **User authenticates** (Auth0) and obtains JWT.
2. UI selects a **Practice** (or uses tenant default).
3. Every PHI API call includes:

   * `tenantId` derived from JWT claim (server-side)
   * `practiceId` in the **route** and validated against JWT allow-list
     (never accepted from request body). 

---

## A) Start Check-In (Create / Open Encounter Draft)

### 1) User initiates check-in

**UI:** “New Check-In” (appointment exists or walk-in)

**User enters**

* Visit Type / Encounter Type (e.g., RoutineVision, Medical, ContactLensFitting)
* Location (optional)
* Appointment date/time (or “now”)
* Optional external appointment reference

**API**

* `POST /api/practices/{practiceId}/encounters` (draft or created)

  * Server stamps `tenantId`, `practiceId`, `createdAtUtc`, `createdByUserId`
  * Returns `encounterId`

**Result**

* Encounter exists (even if patient/coverage not complete yet)
* Status is logically “Draft” (even if you don’t model status yet)

---

## B) Identify Patient (Practice-scoped Search)

### 2) User searches for patient

**UI search inputs**

* First name + last name + DOB (strong match)
* Optionally phone/email as fallback (later enhancement)

**API**

* `GET /api/practices/{practiceId}/patients/search?firstName=&lastName=&dob=`

**Repository guarantees**

* Always filters `tenantId` + `practiceId`
* Uses composite indexes for name/DOB search. 

**Result options**

* 0 matches → create new patient
* 1 match → select patient
* Multiple → show disambiguation list (same name, etc.)

---

## C) Upsert Patient (If New or Needs Updates)

### 3) Create patient (if not found)

**UI collects minimal patient demographics**

* First/last name
* DOB
* Phone/email (optional)
* Optional external patient ID / MRN

**API**

* `POST /api/practices/{practiceId}/patients`

**Result**

* `patientId` returned

### 4) Update patient (if found but info changed)

**API**

* `PUT /api/practices/{practiceId}/patients/{patientId}`

---

## D) Coverage Enrollment (Upsert)

### 5) User adds/updates coverage enrollment(s)

**UI captures enrollment fields**

* Coverage type / plan type: Vision / Medical
* Payer selection (from catalog)
* Member ID, group number
* Relationship to subscriber
* Subscriber name + DOB (if required)
* Effective/termination dates (optional)
* COB hints (optional)
* Notes / “locked” flag (optional)

**API**

* `POST /api/practices/{practiceId}/patients/{patientId}/coverages` (add)
* `PUT /api/practices/{practiceId}/patients/{patientId}/coverages/{coverageEnrollmentId}` (update)

**Persistence**

* CoverageEnrollment is an embedded object under Patient (per your PHI model). 

**Optional: validate payer behavior**

* Use TenantConfig eligibility behaviors:

  * Require subscriber? real-time eligible? supports vision/medical? 

---

## E) Finalize Encounter Patient Link + Coverage Decision

### 6) Attach patient to encounter + set coverage decision (COB)

**UI selects**

* Which coverage enrollment is **Primary** for this visit
* Secondary (if applicable)
* Reason (“RoutineVision use Vision then Medical”, etc.)
* Allow override note if staff chooses differently

**API**

* `PUT /api/practices/{practiceId}/encounters/{encounterId}`
  includes:

  * `patientId`
  * `visitType`
  * optional `coverageDecision` (embedded in Encounter)

**Persistence**

* Encounter embeds `CoverageDecision` and later `EligibilityChecks`. 

---

## F) Submit Eligibility Request (Availity)

### 7) User triggers eligibility check (or it’s required pre-encounter)

**UI**

* “Run Eligibility” button (or auto-run based on tenant config)
* May run once for primary, optionally also for secondary

**API**

* `POST /api/practices/{practiceId}/encounters/{encounterId}/eligibility-checks`
  body includes:

  * `coverageEnrollmentId`
  * `dateOfService`
  * optional “force refresh”

**Service behavior**

1. Validate:

   * encounter exists in this practice
   * coverageEnrollment exists on patient
2. Build 270 request (or Availity API request) from:

   * patient demographics
   * subscriber details
   * payer config mappings
   * encounter context (DOS, visit type)
3. Call Availity (with timeout, retry/backoff if configured)
4. Receive eligibility + benefits + COB-related info (depending on what you request/parse)

**Persistence pattern**

* Save an `EligibilityCheckEmbedded` under the Encounter with:

  * timestamps, status, payerId, snapshots, coverage lines summary
* Store raw payload(s) as **Blob** and persist only `StorageUrl` references in Encounter (matches your indexing exclusions strategy).

---

## G) Display Results + Capture Output

### 8) Show “Front Desk Summary”

**UI displays**

* Active/inactive status
* Plan name, effective dates
* Copays/allowances/frequency notes (as parsed)
* Any error messages or missing info warnings
* If dual coverage: show primary + secondary results and COB guidance

### 9) Store as PDF blob (optional)

**UI**

* “Save PDF to Encounter” (or auto-store)

**Implementation options**

* Generate PDF server-side from normalized summary + include raw 271 attachment reference
* Save PDF to blob storage
* Persist a `DocumentReference` (URL + metadata) embedded in Encounter or as a separate non-PHI doc with strict practice scoping

---

## H) Completion

### 10) Mark check-in complete (optional state)

**UI**

* “Check-in complete” (or “Eligibility verified”)

**API**

* `PUT /api/practices/{practiceId}/encounters/{encounterId}`
  sets `status = Ready/CheckedIn` (if you model it)

---

# Notes / Edge Cases (Worth documenting)

### Missing patient / walk-in

* Encounter can be created first, patient linked later.

### Re-run eligibility

* Append a new EligibilityCheckEmbedded, don’t overwrite (audit trail).

### Inactive coverage response

* Store failed check + error message; prompt user to update coverage enrollment.

### Secondary/COB logic

* For routine vision: typically vision primary then medical secondary
* For medical visit: medical primary then vision (when applicable)
  TenantConfig can supply defaults. 

---

If you want, I can translate this into:

* a **sequence diagram** (UI → API → service → repo → Availity → blob),
* a **concrete endpoint list** (exact routes, request/response DTO shapes),
* and the **Cosmos document mutation plan** (what updates where, and why).
