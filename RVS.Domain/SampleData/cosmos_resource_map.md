# Cosmos Resource Map & ASCII Diagram

## High-Level Resource Map

**Database:** `bfdb`

**Containers + PKs:**

1. **tenants**  
   - **PK:** `/id`  
   - **Documents:**  
     - `Type = "tenant"`  
     - `Type = "tenantConfig"`  
     - `Type = "payerConfig"`

2. **payers**  
   - **PK:** `/id`  
   - **Documents:**  
     - `Type = "payer"`

3. **practices**  
   - **PK:** `/tenantId`  
   - **Documents:**  
     - `Type = "practice"` with embedded `locations[]`

4. **patients**  
   - **PK:** `/tenantId`  
   - **Documents:**  
     - `Type = "patient"` with embedded `coverageEnrollments[]`

5. **encounters**  
   - **PK:** `/tenantId`  
   - **Documents:**  
     - `Type = "encounter"` with embedded `coverageDecision`, `eligibilityChecks[]`, etc.

6. **lookups**  
   - **PK:** `/tenantId`  
   - **Documents:**  
     - Global lookups (TenantId = "GLOBAL")  
     - Tenant-specific lookups  
     - `Type = "lookup"`

---

## ASCII Diagram

```
                           +------------------------+
                           |      Cosmos DB        |
                           |        bfdb           |
                           +-----------+-----------+
                                       |
          -----------------------------------------------------------------
          |              |                 |              |               |
+---------v-----+  +-----v---------+  +----v--------+ +---v---------+ +---v--------+
|  tenants      |  |   payers      |  | practices   | |  patients   | | encounters |
| PK: /id       |  | PK: /id       |  | PK: /tenantId| |PK:/tenantId| |PK:/tenantId|
+---------+-----+  +-----+---------+  +----+--------+ +---+---------+ +---+--------+
          |              |                 |              |               |
   ----------------      |          --------------   --------------   --------------
   |      |       |      |          |            |   |            |   |            |
+--v--+ +--v--+ +--v--+  |  +-------v------+ +---v--+ +---v----+  |  +---v-----+  |
|tenant| |tenant| |payer|  |  practice   | |practice| |patient|  |  |encounter|  |
| doc  | |Config| |Config| |  (tenant)   | |(tenant)| (tenant) | |  |(tenant) |  |
|Type= | |tenant | |payer | |TenantId=   | |TenantId=|TenantId=| |  |TenantId=|  |
|"tenant"|Config | Config| |  ten_xxx    | | ten_xxx |ten_xxx  | |  | ten_xxx |  |
+------+ +------+ +------+  +------------+ +--------+ +--------+  |  +---------+  |
                                 |              |          |      |        |
                                 |              |          |      |        |
                          embedded locations    |     embedded   |   embedded
                                                |  coverageEnroll| eligibility/
                                                |                | coverageDecision
                                                |                |
                                            foreign keys:       |
                                            PracticeId <-+      |
                                                         |      |
                                   PayerId <-------------+------+
                                      (to payers container)


+----------------+
|   lookups      |
| PK: /tenantId  |
+--------+-------+
         |
   -------------  --------------------------
+--v--+    +---v----+                 +----v----+
|Global|   |Global  |                 |Tenant   |
|Svc   |   |Visit   |                 |Visit    |
|Types |   |Types   |                 |Reasons  |
|Tenant|   |Tenant  |                 |TenantId |
|Id=   |   |Id=     |                 |=ten_xxx |
|"GLOBAL"| |"GLOBAL"|                 |        |
+------+   +--------+                 +--------+
```
