Below is a **focused MVP feature list (~12 features)** designed specifically so you can **ship in ~30 days** using your stack:

* **C#**
* **ASP.NET Core API**
* **Blazor**
* **Azure Cosmos DB**
* **Azure Blob Storage**
* **Auth0**

The goal is **not perfection**. The goal is **a usable product you can demo to dealers quickly** and begin onboarding design partners.

---

# RV Service Request Portal

### 30-Day MVP Feature Set

## 1. Dealer Account Creation

Dealers create an account and configure their service portal.

Fields:

* Dealer name
* service email
* phone
* logo upload

Output:

A unique service URL:

```
https://app.yoursite.com/service/{dealer}
```

This is the link dealers put on their website.

---

## 2. Public Customer Service Request Form

The most important feature.

Fields:

* VIN
* Make
* Model
* Year
* Description of issue
* Photo/video upload
* Customer name
* Email
* Phone

No login required.

Mobile-first design.

---

## 3. Photo / Video Upload

Customers upload:

* photos of issue
* short video

Storage:

**Azure Blob Storage**

The files attach to the request record.

This dramatically improves diagnostic information.

---

## 4. Automatic Issue Categorization

Basic rule-based classification.

Example categories:

* Slide system
* Electrical
* Plumbing
* Refrigerator
* HVAC
* Roof leak
* Generator

Example logic:

```
if description contains "slide"
category = Slide System
```

This is simple but surprisingly effective.

---

## 5. Technician Summary Generator

Create a structured summary for advisors.

Example output:

```
Customer: John Smith
VIN: 1ABC2345
Category: Slide System
Issue: Slide will not retract fully
Attachments: 3 photos
```

This can be copied into the dealer’s DMS.

---

## 6. Dealer Request Queue

Main dashboard for the dealership.

Table view:

| Status | Category | Customer | Attachments | Submitted |
| ------ | -------- | -------- | ----------- | --------- |
| New    | Slide    | Smith    | 3           | Today     |

Statuses:

* New
* Contacted
* Scheduled
* Closed

---

## 7. Request Detail Page

When a request is opened:

Show:

* customer info
* VIN / RV info
* description
* attachments
* issue category
* status controls

Advisor can update status.

---

## 8. Customer Confirmation Email

When request is submitted:

Customer receives:

```
Your service request has been received.
A service advisor will contact you soon.
```

Use:

* Azure SendGrid
* or SMTP

---

## 9. Dealer Email Notification

Dealer receives email:

```
New service request submitted
Customer: John Smith
Category: Slide system
Attachments: 3
```

This ensures requests are not missed.

---

## 10. Service Request Link Generator

Dealer dashboard shows:

```
Your Service Request Link:
https://app.yoursite.com/service/acme-rv
```

Also generate:

* QR code

Dealers can place it:

* on website
* in email signature
* at service desk

---

## 11. Simple Analytics

Basic metrics:

* requests this week
* requests this month
* most common issue categories

Helps demonstrate value.

---

## 12. Admin Panel

Minimal internal admin tools:

* view dealers
* disable accounts
* view requests

Important for early operations.

---

# Suggested Database Model (CosmosDB)

## Dealers Container

```
Dealer
{
  id
  name
  slug
  email
  phone
  logoUrl
}
```

---

## ServiceRequests Container

```
ServiceRequest
{
  id
  dealerId
  vin
  make
  model
  year
  description
  category
  customerName
  customerEmail
  customerPhone
  attachments[]
  status
  createdAt
}
```

Partition key:

```
dealerId
```

---

# Blob Storage Structure

```
service-attachments/
   dealerId/
      requestId/
         photo1.jpg
         video1.mp4
```

---

# Blazor Pages

Public:

```
/service/{dealerSlug}
```

Dealer portal:

```
/dashboard
/dashboard/requests
/dashboard/request/{id}
/dashboard/settings
```

---

# 30-Day Development Timeline

### Week 1

Foundation

* Auth0 integration
* dealer accounts
* Cosmos DB setup
* basic Blazor layout

---

### Week 2

Customer Intake

* service request form
* file uploads
* blob storage integration

---

### Week 3

Dealer Tools

* request queue
* request detail page
* status updates

---

### Week 4

Polish + Launch

* email notifications
* QR code generator
* basic analytics
* bug fixes

Launch with design partners.

---

# What NOT To Build Yet

To ship fast, avoid:

* DMS integrations
* scheduling engine
* AI models
* technician routing
* mobile apps

Those come later.

---

# What Makes This MVP Valuable

Even this simple system:

* reduces service phone calls
* captures better diagnostic information
* organizes service waitlists

That is enough to begin selling.

---

# Expected First Version Effort

Roughly:

* **8–12 backend endpoints**
* **6–8 Blazor pages**
* **2 Cosmos containers**
* **1 blob storage container**

Very achievable for a solo developer.

---

If you'd like, I can also show you **the exact system architecture diagram and API endpoint structure** that will make this system **extremely easy to extend later into the full ServiceFlow platform.**
