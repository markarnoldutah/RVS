Below is a **unified architecture overview for RVS** showing how the three primary user interfaces interact with the core system and dealership infrastructure. This diagram focuses on **operational data flow**, not low-level implementation details, so it can be used in product documentation, investor decks, or enterprise sales conversations.

This architecture aligns with the RVS identity model and backend services described in the system architecture and identity design documents. 

---

# RVS Unified System Architecture

```
                     RV SERVICE FLOW (RVS)
                Unified Service Intake Platform
────────────────────────────────────────────────────────────────

                        CUSTOMERS
                           │
                           │
                           ▼
              ┌─────────────────────────────┐
              │  Customer Intake Web App    │
              │  (Mobile-first web form)    │
              │                             │
              │ • Submit service request    │
              │ • Upload photos/videos      │
              │ • VIN scan                  │
              │ • Guided diagnostics        │
              │ • Magic-link status page    │
              └──────────────┬──────────────┘
                             │
                             │ Service Request API
                             ▼
                   ┌─────────────────────┐
                   │       RVS API       │
                   │                     │
                   │ • Service requests  │
                   │ • Attachments       │
                   │ • Service events    │
                   │ • Analytics         │
                   │ • Tenant isolation  │
                   └─────────┬───────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        ▼                    ▼                    ▼

 ┌───────────────┐   ┌─────────────────┐   ┌─────────────────┐
 │ Cosmos DB     │   │ Blob Storage    │   │ Lookup Services │
 │ (Primary DB)  │   │ (Photos/videos) │   │ Issue categories│
 │               │   │                 │   │ Failure modes   │
 │ Service reqs  │   │ Attachments    │   │ Repair actions  │
 │ Service events│   │                │   │ Component types │
 │ Customers     │   │                │   │                 │
 │ Locations     │   │                │   │                 │
 └───────────────┘   └─────────────────┘   └─────────────────┘


────────────────────────────────────────────────────────────────
                     DEALERSHIP OPERATIONS
────────────────────────────────────────────────────────────────


        SERVICE MANAGERS / ADVISORS
                        │
                        ▼
       ┌─────────────────────────────────┐
       │ Service Manager Web Dashboard   │
       │ (Desktop web app)               │
       │                                 │
       │ • Intake queue                  │
       │ • Service scheduling            │
       │ • Technician assignment         │
       │ • Service board                 │
       │ • Outcome compliance monitoring │
       │ • Analytics dashboard           │
       │ • Batch outcome entry           │
       └──────────────┬──────────────────┘
                      │
                      │
                      ▼


                 TECHNICIANS
                      │
                      ▼
       ┌─────────────────────────────────┐
       │ Technician Mobile App           │
       │ (Phone / Tablet)                │
       │                                 │
       │ • Open job (scan VIN / QR)      │
       │ • Review intake details         │
       │ • View photos/videos            │
       │ • Record repair outcome         │
       │ • Upload repair photos          │
       └─────────────────────────────────┘


────────────────────────────────────────────────────────────────
                        SECURITY LAYER
────────────────────────────────────────────────────────────────


                     ┌─────────────────┐
                     │   Auth0 OIDC    │
                     │                 │
                     │ Dealer Staff    │
                     │ Authentication  │
                     │ RBAC roles      │
                     │ Location scope  │
                     └─────────────────┘

Customers interact anonymously using intake forms and magic-link status tokens. :contentReference[oaicite:1]{index=1}


────────────────────────────────────────────────────────────────
                     OPTIONAL INTEGRATIONS
────────────────────────────────────────────────────────────────


                DEALERSHIP SYSTEMS

        ┌─────────────────────────────────┐
        │ Dealer Management System (DMS)  │
        │                                 │
        │ Examples:                       │
        │ • IDS Astra                     │
        │ • Lightspeed                    │
        │                                 │
        │ Integration Options             │
        │ • Pull repair outcomes          │
        │ • Push service request data     │
        │ • Sync work orders              │
        └─────────────────────────────────┘


────────────────────────────────────────────────────────────────
                       ANALYTICS LAYER
────────────────────────────────────────────────────────────────


        Aggregated Service Event Data Enables:

        • Common failure detection
        • Average repair times
        • Parts replacement trends
        • Manufacturer reliability insights
        • Dealership operational analytics
```

---

# How the Pieces Work Together

### 1. Customer Intake

Customers submit service requests through the **intake web app**.

Captured data includes:

• VIN and vehicle information
• issue category
• structured symptom data
• photos and videos

This creates a **Service Request record** in the RVS database.

---

### 2. Service Manager Workflow

Service managers use the **desktop dashboard** to:

• review intake requests
• assign technicians or bays
• schedule service
• manage the service queue

Requests are converted into **active service jobs**.

---

### 3. Technician Workflow

Technicians open jobs using the **mobile app** by:

• scanning VIN
• selecting from their job queue

They can then:

• review intake information
• view customer photos
• diagnose the problem

After the repair they record:

• failure mode
• repair action
• labor hours
• optional parts and notes

This creates a **Service Event record**.

---

### 4. Data Accumulation

Every completed job contributes structured data:

```
Vehicle
Issue category
Failure mode
Repair action
Labor hours
Parts used
```

This builds the **RVS service intelligence dataset**.

---

### 5. Long-Term Value

Over time the platform becomes a **knowledge base of real-world service repairs**, enabling:

• dealership operational analytics
• faster technician diagnosis
• manufacturer reliability insights
• predictive repair guidance

This data layer becomes the **core defensibility moat** of the system.

---

# The Simplest Way to Describe RVS

In one sentence:

**RVS connects customers, service managers, and technicians through a unified service intake and repair intelligence platform.**

---

If you'd like, I can also produce a **clean visual diagram (investor/deck quality architecture graphic)** that makes this architecture instantly understandable in one slide. It tends to dramatically improve enterprise sales conversations with dealership executives.
