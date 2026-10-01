Below is a **consolidated user interface feature summary** for RVS that reflects the operational realities of service departments and the goal of maximizing **technician outcome entry with minimal friction** while giving **service managers operational visibility and control**.

The UI should be designed as **two distinct experiences** optimized for different roles:

1. **Technician Mobile App (phone/tablet)**
2. **Service Manager Desktop Web App**

These two interfaces serve very different workflows.

---

# 1. Technician Mobile / Tablet App

Primary users: technicians working in service bays.

Primary goals:

• open the correct job instantly
• review service request details
• record repair outcomes in seconds
• capture photos if needed

The interface should be **mobile-first**, touch optimized, and usable with gloves.

---

## Job Access (Fastest Possible Entry)

Technicians must be able to open a job in **one action**.

Supported methods:

### QR / VIN Scanning

Technician scans VIN or QR code attached to:

• dashboard
• key tag
• work order sheet

Workflow:

Scan → Job opens instantly.

---

### My Jobs Queue

When technicians log in they see:

My Jobs

```
Momentum 395G
Slide grinding noise
Bay 3

Cougar 29BHS
Fridge not cooling
Bay 1
```

Technicians tap the job assigned to them.

---

### Bay-Based Access

Optional for larger shops.

Tablet in each bay automatically shows the job assigned to that bay.

Technician taps screen → job opens.

---

## Service Request Intake Review

Technicians should immediately see the **customer intake summary**.

Displayed information:

Vehicle

```
VIN
Manufacturer
Model
Year
```

Customer Issue Summary

```
Issue category
Customer description
Photos or videos
Date submitted
```

Predicted Diagnostics (optional but powerful)

```
Most likely failures
Suggested inspection points
```

Purpose:

• reduce diagnostic time
• provide context before repair begins

---

## Repair Outcome Entry (5–10 Second Workflow)

Outcome entry should appear automatically when the technician taps **Complete Job**.

Single-screen layout.

Fields:

Failure Mode

```
Hydraulic pump failure
Slide motor failure
Electrical fault
Fluid leak
```

Repair Action

```
Replace pump
Replace motor
Repair wiring
Adjust mechanism
```

Labor Time

```
Suggested: 3.2 hrs
+ / – buttons
```

Optional fields hidden unless expanded:

```
Parts used
Technician notes
Photo upload
```

Submit button:

```
Complete Job
```

Most common workflow:

Confirm prediction → Submit.

Total interaction: **3–5 seconds**.

---

## Additional Technician Features

### Photo Capture

Technicians can quickly add photos:

• damaged components
• completed repair
• warranty documentation

---

### Voice Notes

Technicians can dictate optional notes instead of typing.

---

### Offline Mode

Service bays may have poor connectivity.

Outcome entries should store locally and sync when connection returns.

---

## API Backend Readiness

> Cross-reference: See **RVS_Core_Architecture_Version3.md** Section 17 for full gap analysis and resolutions.

The following backend capabilities support the features described above:

| Feature | API Endpoint(s) | Auth | Status |
|---|---|---|---|
| **QR / VIN Scanning** | `POST .../search` with `assetId` filter | Bearer + `service-requests:search` | ✅ Ready |
| **My Jobs Queue** | `POST .../search` with `assignedTechnicianId` filter | Bearer + `service-requests:search` | ✅ Ready |
| **Bay-Based Access** | `POST .../search` with `assignedBayId` filter | Bearer + `service-requests:search` | ✅ Ready |
| **Vehicle Info / Issue Summary** | `GET .../service-requests/{srId}` | Bearer + `service-requests:read` | ✅ Ready |
| **Predicted Diagnostics** | `GET .../service-requests/{srId}` (diagnosticResponses + technicianSummary) | Bearer + `service-requests:read` | ✅ Ready |
| **Failure Mode / Repair Action Selection** | `GET api/lookups/{category}` for `failureModes`, `repairActions` | Bearer + `lookups:read` | ✅ Ready (requires seed data) |
| **Labor Time Entry** | `PUT .../service-requests/{srId}` (ServiceEvent.LaborHours) | Bearer + `service-requests:update-service-event` | ✅ Ready |
| **Parts / Notes / Complete Job** | `PUT .../service-requests/{srId}` (ServiceEvent fields + status) | Bearer + `service-requests:update-service-event` | ✅ Ready |
| **Photo Capture (Technician)** | `POST .../attachments` (authenticated) | Bearer + `attachments:upload` | ✅ Ready |
| **Voice Notes** | `POST .../attachments` (`.m4a`, `.wav` accepted) | Bearer + `attachments:upload` | ✅ Ready |
| **Offline Mode** | Client-side queue → sequential `PUT` replay on reconnect | — | 🟡 Client-side only (MVP) |
| **Suggested Labor Time** | Static lookup values (MVP) → `GET api/predictions/labor` (Phase 5–6) | — | 🔵 Future |


