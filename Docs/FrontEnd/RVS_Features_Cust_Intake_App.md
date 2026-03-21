Below is a **consolidated user interface feature summary for the customer-facing intake application** for RVS. This is the third major UI surface alongside the technician mobile app and the service manager desktop dashboard previously described. 

The customer intake interface must prioritize **simplicity, clarity, and high-quality problem capture**, because the quality of intake data directly affects:

• technician diagnostic speed
• repair cycle time
• customer satisfaction
• long-term service data quality

Customers interact **anonymously** in MVP using a submission form and magic-link status access rather than creating accounts. 

---

# 3. Customer Intake Application

Primary users:

• RV owners requesting service
• first-time customers
• returning customers checking status

Primary goals:

• capture accurate service requests
• gather diagnostic photos/videos
• reduce service advisor phone calls
• provide a modern service experience

The intake app should be **mobile-first** because most customers will submit requests from their phones.

---

# 1. Dealer-Specific Intake Page

Each dealership location has its own intake URL.

Example:

```
rvserviceflow.com/intake/blue-compass-salt-lake
```

Page header includes:

• dealership logo
• dealership name
• location contact info
• optional service instructions

Purpose:

• reinforce dealership branding
• ensure requests route to the correct location

---

# 2. Guided Service Request Form

The intake experience should feel like a **guided wizard**, not a long form.

The process should take **2–3 minutes**.

Steps:

1. Vehicle information
2. Issue category selection
3. AI-guided diagnostic questions
4. Photos/videos
5. Contact information
6. Submit

---

## Step 1: Vehicle Identification

Customers provide vehicle details.

Fields:

```
VIN
Manufacturer
Model
Year
```

VIN scanning should be supported via phone camera.

Benefits:

• reduces typing
• improves data accuracy
• helps identify manufacturer-specific issues

---

## Step 2: Issue Category Selection

Customers select the **general problem type**.

Example categories:

```
Slide System
Leveling System
Electrical
Plumbing
Appliances
Air Conditioning
Roof / Exterior
Other
```

Selecting a category allows the system to:

• guide the customer to relevant questions
• pre-structure diagnostic data
• assist technician troubleshooting

---

## Step 3: AI-Guided Diagnostic Questions

After the customer selects a category and optionally types an initial description, the system calls **Azure OpenAI** to generate **2–4 targeted follow-up questions** specific to the selected category, description, and vehicle.

This replaces static per-category question templates with dynamic, context-aware questions.

Example for slide systems:

```
Based on your description, here are a few questions to help our technician:

What happens when you try to extend the slide?

☐ Grinding noise
☐ Slide moves unevenly
☐ Slide does not move at all
☐ Error message on panel

Does the slide operate on hydraulic or electric mechanisms?

☐ Hydraulic
☐ Electric / Motor
☐ Not sure

Have you noticed any fluid leaks near the slide mechanism?

☐ Yes
☐ No
☐ Not sure
```

The system may also display a **smart suggestion**:

```
This is commonly caused by a hydraulic pump issue.
Please upload a photo of the hydraulic pump area if possible.
```

Optional free-text description:

```
Tell us anything else that may help our technician.
```

Benefits:

• questions adapt to the specific category, description, and vehicle
• no manual template curation required
• handles "Other" category gracefully
• structured responses dramatically improve technician diagnostic speed
• if AI is unavailable, the system falls back to basic questions per category

API call: `POST api/intake/{locationSlug}/diagnostic-questions`

---

## Step 4: Photo and Video Upload

Customers are encouraged to upload visual evidence.

Upload options:

```
Add photo
Add video
Add additional images
```

Common examples:

• leaking plumbing
• damaged slide components
• control panel error codes
• roof damage

Benefits:

• technicians can pre-diagnose problems
• reduces diagnostic time during appointment

---

## Step 5: Contact Information

Customer provides:

```
Name
Phone number
Email address
Preferred contact method
```

Optional fields:

```
Preferred appointment timeframe
Urgency level
```

---

# 3. Submission Confirmation

After submitting, customers see a confirmation screen.

Example:

```
Your service request has been submitted.

A service advisor will review your request and contact you to schedule service.
```

Information displayed:

• request ID
• dealership contact info
• expected response timeframe

---

# 4. Magic-Link Status Page

Customers receive a **magic-link email** that allows them to view their request status without logging in. 

Example email:

```
View your service request status:
https://rvserviceflow.com/status/ABCD1234
```

The status page shows:

```
Request received
Under review
Scheduled
In repair
Completed
```

Customers may also see:

• technician notes
• photos of completed repairs
• estimated completion

This improves transparency and reduces inbound status calls.

---

# 5. Return Customer Experience

If a customer submits another request later, the system can automatically recognize them using:

• email address
• phone number

Returning customers benefit from:

• prefilled contact info
• stored vehicle details
• faster submissions

---

# 6. Mobile-First Design

The intake app must be optimized for:

• smartphones
• touch input
• camera uploads

Design principles:

• large buttons
• minimal typing
• progressive steps
• fast uploads

Typical submission time should be **under three minutes**.

---

# 7. Smart Diagnostic Assistance (MVP — Powered by Azure OpenAI)

The intake app provides **AI-powered diagnostic assistance** at intake time via Azure OpenAI (GPT-4o-mini).

Two capabilities are included in MVP:

**1. Dynamic diagnostic questions** — Generated based on the selected category, initial description, and vehicle info. The AI adapts questions to the specific context rather than using static templates.

**2. Smart suggestions** — When the AI identifies a likely root cause or useful photo opportunity, it surfaces a suggestion to the customer.

Example:

Customer selects:

```
Category: Slide System
Description: Slide grinding noise
Vehicle: Grand Design Momentum 395G
```

System response:

```
This issue is commonly caused by a hydraulic pump problem
on Grand Design slide-outs.

Please upload a photo of the hydraulic pump area if possible.
```

This helps capture more useful diagnostic data before the RV arrives.

**Cost:** ~$0.0002 per intake with GPT-4o-mini (~$0.20/month at 1,000 intakes).

**Fallback:** If Azure OpenAI is unavailable, the system falls back to basic rule-based questions per category. Intake is never blocked by an external service dependency.

---

# 8. Dealer Configuration Options

Dealerships should be able to configure intake behavior.

Configurable elements:

• issue categories
• AI context for diagnostic questions (optional dealer-specific prompt context, e.g. "We specialize in Grand Design and Keystone brands")
• intake page branding
• required fields
• photo/video limits
• maximum attachment count
• service instructions displayed on intake page

This allows dealerships to tailor intake to their service operations.

---

# Core Design Principles for Customer Intake

### 1. Extremely Simple Experience

Customers should complete intake quickly without technical knowledge.

---

### 2. Structured Data Capture

Guided questions produce structured data that technicians can use immediately.

---

### 3. Visual Evidence

Photos and videos significantly improve diagnostic efficiency.

---

# Resulting Operational Benefits

When the intake app works well:

Customers submit high-quality service requests.

Service advisors spend less time on intake calls.

Technicians receive structured problem descriptions and images before the RV arrives.

Service departments diagnose and repair issues faster.

---

If helpful, I can also produce a **single unified architecture diagram showing how the customer intake app, technician app, and service manager dashboard interact within the full RVS system**, which makes the product vision much easier to communicate to dealerships and investors.
