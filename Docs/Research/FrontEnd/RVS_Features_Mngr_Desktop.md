---

# 2. Service Manager Desktop Web App

Primary users:

• service managers
• service advisors
• operations leadership

Primary goals:

• manage intake requests
• assign work
• monitor service progress
• ensure outcome capture
• analyze service operations

This interface should be optimized for **large screens and operational oversight**.

---

## Intake Queue

Service requests submitted by customers appear in an intake queue.

Example:

```
New Service Requests

Momentum 395G
Slide grinding noise
Submitted today

Jayco Eagle
AC not cooling
Submitted yesterday
```

Each request includes:

• vehicle info
• issue category
• customer description
• photos / videos

Managers can review and triage.

---

## Intake Triage and Scheduling

Manager actions:

• confirm issue category
• request additional information
• schedule service appointment
• convert request to service job

This step ensures technicians receive **clear and structured work orders**.

---

## Work Assignment

Service managers assign jobs to technicians or bays.

Example interface:

```
Assign To

Technician
Bay
Priority
Scheduled date
```

Assignment automatically updates technician **My Jobs queue**.
Optional:  Use AI to infer skillset needed for job; recommend technicians based on skills matrix.  

---

## Service Board

Visual board showing all active work.
Status should align with status list visible in customer status view. 

Example columns:

```
New Requests
Scheduled
In Diagnosis
Waiting Parts
In Repair
Completed (Outcome Needed)
Closed
```

Managers can drag jobs between stages.

Purpose:

• operational visibility
• quick status tracking

---

## Outcome Compliance Monitoring

Managers need to ensure repair outcomes are captured.

Dashboard example:

```
Jobs Completed Without Outcomes

SR-1082
SR-1094
SR-1102
```

Manager can quickly open these jobs and record outcomes if technicians did not.

---

## Batch Outcome Entry

For efficiency, managers can update multiple jobs at once.

Example:

```
Select jobs → Apply repair outcome
```

Useful when:

• technicians forget entries
• repeated repairs occur
• service backlog is cleared

---

## Operational Analytics

When outcomes are captured consistently, the system provides insights such as:

Common Failures

```
Slide hydraulic pump
Lippert leveling sensor
Dometic control board
```

Average Repair Times

```
Hydraulic pump replacement
3.2 hours
```

Parts Replacement Trends

```
Hydraulic Pump Model X
42 replacements
```

These insights help leadership improve:

• scheduling
• parts inventory
• technician training

---

# Core Design Principles

Across both interfaces, the system should follow three principles.

### 1. Minimal Technician Friction

Technicians should never need to:

• search extensively
• type long notes
• navigate complex menus

---

### 2. Structured Data Capture

Repair outcomes must be structured so the system can analyze:

• failure modes
• repair actions
• labor times
• parts usage

---

### 3. Workflow Integration

RVS should align with existing service workflows:

• intake → triage → repair → outcome → close

Not replace dealership DMS systems but complement them.

---

# Resulting System Behavior

When designed correctly:

Technicians interact with the system briefly during repairs.

Service managers manage operations and ensure data completeness.

Outcome data accumulates automatically as part of normal service activity, enabling powerful operational insights and long-term service intelligence for dealerships.