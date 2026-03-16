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
2. Problem description
3. Photos/videos
4. Contact information
5. Submit

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

## Step 3: Guided Problem Description

Instead of asking customers to write long descriptions, the system asks **simple structured questions**.

Example for slide systems:

```
What happens when you try to extend the slide?

☐ Grinding noise
☐ Slide moves unevenly
☐ Slide does not move
☐ Error message on panel
```

Optional free-text description:

```
Tell us anything else that may help our technician.
```

Structured responses dramatically improve technician diagnostic speed.

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

# 7. Smart Diagnostic Assistance (Future Enhancement)

As RVS collects more service data, the intake app can provide **automated troubleshooting suggestions**.

Example:

Customer selects:

```
Slide grinding noise
```

System response:

```
This issue is commonly caused by a hydraulic pump problem.

Please upload a photo of the hydraulic pump area if possible.
```

This helps capture more useful diagnostic data.

---

# 8. Dealer Configuration Options

Dealerships should be able to configure intake behavior.

Configurable elements:

• issue categories
• diagnostic questions
• intake page branding
• required fields
• photo/video limits

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
