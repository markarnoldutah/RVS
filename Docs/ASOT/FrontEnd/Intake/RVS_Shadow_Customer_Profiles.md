For a solo developer aiming for **$50k ARR in 4–6 months**, implementing full customer accounts (logins/passwords) in Phase 1 is a **distraction**, but capturing and storing **customer profiles** as "shadow records" is a major **advantage**.

Here is how to balance your 30-day MVP timeline with the long-term platform vision.

---

### 1. Why Full Accounts are a Distraction (Short-Term)

* **Intake Friction:** The primary goal of RVS is to solve the "service department overload" by making intake easy. Requiring a customer to create an account and verify an email before describing their leaky slide-out creates a barrier that defeats the "mobile-friendly service request" goal.
* **Scope Creep:** You have a 30-day build window. Managing password resets, profile editing UIs, and Auth0 "End User" vs. "Dealer Staff" roles adds technical overhead that doesn't directly reduce **Repair Event Cycle Time (RECT)**.
* **PII Liability:** Explicitly managing "Accounts" increases your responsibility for user data privacy and security management before you’ve even signed your first five design partners.

### 2. Why Profile Capture is an Advantage (Long-Term)

* **The "Thor" Vision:** To become the "operational brain" for the industry, you need a historical record of every unit. Linking customer info to a **VIN** allows you to show a "service history" across different dealerships in the future.
* **Reducing RECT:** If a customer returns for a second issue, having their "make/model/year" and "contact information" pre-populated (via a secure "magic link" or VIN lookup) improves service advisor efficiency.
* **Expansion Path:** Customer profiles are required for **Phase 5 (Repair stage tracking)** and **Phase 6 (Service analytics)**, where customers will want to see the status of their specific rig.

---

### 3. The Recommended "Architect's Compromise"

Instead of a "Sign Up" button, use a **Frictionless Profile** strategy for your MVP:

* **Frictionless Intake:** The customer completes the service request form as a "guest".
* **Shadow Profiles:** Your **ASP.NET Core API** checks **CosmosDB** for an existing email or VIN. If found, it links the new request to the existing "Customer Profile" document.
* **The "Magic Link":** In the "confirmation email," include a unique, secure link (e.g., `rvs.app/status/{guid}`). This allows the customer to view their request and profile without ever creating a password.
* **Auth0 Strategy:** Keep **Auth0** strictly for **Dealer Features** (Service Advisors and Technicians) during the first 6 months to minimize complexity.

### Summary Table

| Feature | Phase 1 (MVP) | Phase 2+ (Expansion) |
| --- | --- | --- |
| **User Accounts** | **No** (Avoid Friction) | **Yes** (For status tracking) |
| **Profile Capture** | **Yes** (Name, Email, VIN) | **Yes** (Detailed History) |
| **Auth0 Role** | Dealer Staff only | Dealer Staff + Customers |
| **Sales Impact** | Fast GTM | High Stickiness |

**Would you like me to show you the CosmosDB schema for a "Customer Profile" that allows for this "Shadow Record" logic while staying partitioned by `DealerId`?**