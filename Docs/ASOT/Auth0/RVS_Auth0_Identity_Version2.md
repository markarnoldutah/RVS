---

# RV Service Flow (RVS) — Auth0 Identity & Authorization

**Authoritative Source of Truth (ASOT) — March 25, 2026**

This document covers Auth0 configuration, RBAC roles and permissions, JWT design, ClaimsService, authorization policies, and the tenant provisioning flow. For domain model, data layer, and API surface, see the companion document **RVS_Core_Architecture_Version3.md**.

---

## 1. Auth0 Tenant Model

### 1.1 Architecture Overview

RVS uses Auth0 as the identity provider for all dealer staff. Customers are **never** Auth0 users in MVP — they interact via `[AllowAnonymous]` endpoints with shadow profiles and magic-link tokens.

| Auth0 Concept | RVS Equivalent | Example |
|---|---|---|
| Auth0 Tenant | Single RVS Auth0 application instance | `rvserviceflow.auth0.com` |
| Auth0 API | `RVS.API` audience | `https://api.rvserviceflow.com` |
| Custom claim namespace | All custom JWT claims | `https://rvserviceflow.com/` |
| User `app_metadata` | Tenant context per user | `tenantId`, `orgName`, `locationIds`, `regionTag` |

### 1.2 Tenant Scoping via `app_metadata`

RVS does **not** use Auth0 Organizations. Tenant context is stored in each user's `app_metadata` and injected into the JWT via a Post-Login Action. This approach:

- Runs on the **Auth0 Free plan** with no tenant/organization cap.
- Supports unlimited dealer corporations without paid tier upgrades.
- Keeps all tenant-scoping logic in the RVS service layer (via `ClaimsService`), not in Auth0.

Each user's `app_metadata.tenantId` becomes the `tenantId` used throughout RVS — the Cosmos DB partition key, the blob storage path prefix, and the data isolation boundary.

### 1.3 User ↔ Tenant Mapping

```
Auth0 Tenant: rvserviceflow.auth0.com
│
├── User: jane@bluecompassrv.com       → dealer:corporate-admin
│   app_metadata: { tenantId: "org_blue_compass_rv", orgName: "Blue Compass RV" }
│
├── User: mike@bluecompassrv.com       → dealer:regional-manager
│   app_metadata: { tenantId: "org_blue_compass_rv", orgName: "Blue Compass RV",
│                    locationIds: ["loc_slc", "loc_denver", "loc_boise"], regionTag: "west" }
│
├── User: tech1@bluecompassrv.com      → dealer:technician
│   app_metadata: { tenantId: "org_blue_compass_rv", orgName: "Blue Compass RV",
│                    locationIds: ["loc_blue_compass_slc"] }
│
├── User: bob@generalrv.com            → dealer:owner
│   app_metadata: { tenantId: "org_general_rv", orgName: "General RV" }
│
├── User: owner@happytrailsrv.com      → dealer:owner
│   app_metadata: { tenantId: "org_happy_trails_rv", orgName: "Happy Trails RV" }
│
└── User: admin@rvserviceflow.com       → platform:admin
    app_metadata: {}  (no tenantId — operates cross-tenant)
```

### 1.4 Design Tradeoffs

| Concern | How RVS Handles It |
|---|---|
| **User isolation** | Enforced by `ClaimsService` + Cosmos partition key, not by Auth0 membership boundaries |
| **Role scoping** | Roles are global Auth0 roles (not per-org). A user belongs to exactly one tenant via `app_metadata.tenantId` |
| **Invitation flows** | Handled via Auth0 Dashboard or Management API; owner invites staff by creating users with matching `tenantId` |
| **Per-tenant IdP** | Not available without Organizations; all tenants share the same Auth0 login experience |
| **Branded login** | Not available without Organizations; deferred to a future paid tier if needed |

---

## 2. Auth0 API (Resource Server)

| Setting | Value |
|---|---|
| Identifier (Audience) | `https://api.rvserviceflow.com` |
| Signing Algorithm | RS256 |
| Token Expiration | 3600s (1 hour) |
| RBAC | Enabled |
| Add Permissions in Access Token | Enabled |

---

## 3. Actors and Auth Methods

| Actor | Auth Method | Profile Model | Status Access |
|---|---|---|---|
| **Customer (intake form)** | `[AllowAnonymous]` + rate limiting | Shadow `CustomerProfile` auto-created | None at submission time |
| **Customer (status page)** | `[AllowAnonymous]` + magic-link token | Existing `CustomerIdentity` | `GET /api/status/{token}` — cross-dealer |
| **Customer (future Phase 2+)** | Auth0 OIDC (optional upgrade) | `CustomerIdentity.Auth0UserId` linked | Full account access |
| **Dealer staff** | Auth0 JWT Bearer (`app_metadata`-scoped) | Auth0 user + tenant/role/location claims | Full dashboard access |
| **System / API-to-API (future)** | Auth0 M2M Client Credentials | N/A | DMS integration |

---

## 4. Roles

Roles are global Auth0 roles. Tenant scoping is enforced by `app_metadata.tenantId` — a user at Corporation A has no access to Corporation B because their `tenantId` differs, not because of Auth0 membership. One global role exists for platform operations.

| Role | Scope | Description |
|---|---|---|
| `platform:admin` | Global (cross-tenant) | RVS internal staff. Full system access across all tenants. |
| `dealer:corporate-admin` | Tenant-wide | Corporate HQ employee with full access across all locations. |
| `dealer:owner` | Tenant-wide | Business owner or general manager with full control of their organization. |
| `dealer:regional-manager` | Tenant + region tag | Manager with visibility across multiple locations within their region. |
| `dealer:manager` | Location-scoped | Service department manager at a single location. |
| `dealer:advisor` | Location-scoped | Service advisor who creates and manages service requests day-to-day. |
| `dealer:technician` | Location-scoped | Technician who views assigned work and logs repair details. |
| `dealer:readonly` | Location-scoped | View-only access for observers such as accounting or auditors. |

---

## 5. Permissions

Fine-grained permissions assigned to roles. Carried in the JWT `permissions` array and enforced server-side via ASP.NET authorization policies.

### 5.1 Service Requests

| Permission | Description |
|---|---|
| `service-requests:read` | View service request details |
| `service-requests:search` | Search / filter service requests |
| `service-requests:create` | Create a service request from the dealer dashboard |
| `service-requests:update` | Update service request (status, notes, category) |
| `service-requests:update-service-event` | Update Section 10A fields (repair action, parts, labor) |
| `service-requests:delete` | Delete a service request |

### 5.2 Attachments

| Permission | Description |
|---|---|
| `attachments:read` | View / download attachments (SAS URL) |
| `attachments:upload` | Upload an attachment (authenticated — technician/staff photo capture during repair) |
| `attachments:delete` | Delete an attachment |

### 5.3 Dealerships

| Permission | Description |
|---|---|
| `dealerships:read` | View dealership (corporation) details |
| `dealerships:update` | Update dealership settings (logo, corporate name) |

### 5.4 Locations

| Permission | Description |
|---|---|
| `locations:read` | View location details and list |
| `locations:create` | Create a new physical location |
| `locations:update` | Update location settings (contact, intake config, logo, region tag) |

### 5.5 Analytics

| Permission | Description |
|---|---|
| `analytics:read` | View service request analytics and summaries |

### 5.6 Tenant Configuration

| Permission | Description |
|---|---|
| `tenants:config:read` | View tenant configuration and access gate |
| `tenants:config:create` | Bootstrap tenant configuration |
| `tenants:config:update` | Update tenant configuration |

### 5.7 Lookups

| Permission | Description |
|---|---|
| `lookups:read` | Read lookup sets (issue categories, component types) |

### 5.8 Platform Administration

| Permission | Description |
|---|---|
| `platform:tenants:manage` | Create / disable / enable tenants across the platform |
| `platform:lookups:manage` | Create / update global lookup sets |

---

## 6. Role → Permission Matrix

| Permission | `platform:admin` | `dealer:corporate-admin` | `dealer:owner` | `dealer:regional-manager` | `dealer:manager` | `dealer:advisor` | `dealer:technician` | `dealer:readonly` |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `service-requests:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ ¹ | ✅ |
| `service-requests:search` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ ² | ✅ |
| `service-requests:create` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | — |
| `service-requests:update` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | — |
| `service-requests:update-service-event` | ✅ | ✅ | ✅ | ✅ | ✅ | — | ✅ | — |
| `service-requests:delete` | ✅ | ✅ | ✅ | ✅ | ✅ | — | — | — |
| `attachments:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `attachments:upload` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| `attachments:delete` | ✅ | ✅ | ✅ | ✅ | ✅ | — | — | — |
| `dealerships:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `dealerships:update` | ✅ | ✅ | ✅ | — | — | — | — | — |
| `locations:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `locations:create` | ✅ | ✅ | ✅ | — | — | — | — | — |
| `locations:update` | ✅ | ✅ | ✅ | ✅ | ✅ | — | — | — |
| `analytics:read` | ✅ | ✅ | ✅ | ✅ | ✅ | — | — | ✅ |
| `tenants:config:read` | ✅ | ✅ | ✅ | — | — | — | — | — |
| `tenants:config:create` | ✅ | ✅ | ✅ | — | — | — | — | — |
| `tenants:config:update` | ✅ | ✅ | ✅ | — | — | — | — | — |
| `lookups:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `platform:tenants:manage` | ✅ | — | — | — | — | — | — | — |
| `platform:lookups:manage` | ✅ | — | — | — | — | — | — | — |

¹ Technicians have `service-requests:read` but the application layer further restricts to their assigned SRs only (checked in `ServiceRequestService`, not at the Auth0 level).

² Technicians have `service-requests:search` but the application layer restricts search results to SRs where `AssignedTechnicianId` matches the technician's `userId` claim. This enables the "My Jobs" queue on the technician mobile app.

---

## 7. Auth0 Login Action — Enrich Access Token

A Post-Login Action runs after every authentication and injects tenant context, roles, location scoping, and user ID into the JWT. This eliminates any need for the API to call Auth0 at request time.

**What the action does:**

| Step | Source | Claim Set | Description |
|---|---|---|---|
| 1 | `event.user.app_metadata.tenantId` | `https://rvserviceflow.com/tenantId` | Corporation partition key |
| 2 | `event.user.app_metadata.orgName` | `https://rvserviceflow.com/orgName` | Corporation display name |
| 3 | `event.authorization.roles` | `https://rvserviceflow.com/roles` | Array of role strings |
| 4 | `event.user.user_id` | `https://rvserviceflow.com/userId` | Auth0 user ID |
| 5 | `event.user.app_metadata.locationIds` | `https://rvserviceflow.com/locationIds` | Array of location IDs (if present) |
| 6 | `event.user.app_metadata.regionTag` | `https://rvserviceflow.com/regionTag` | Regional scope string (if present) |

**Location scoping:** Location IDs are stored in `app_metadata` (not in Auth0 roles) because Auth0 roles are permission bundles, not data scopes. A `dealer:advisor` at Blue Compass SLC has `locationIds: ["loc_slc"]` in their `app_metadata`. A `dealer:corporate-admin` has no `locationIds` (which means "all locations").

---

## 8. JWT Access Token Structure

### 8.1 Sample: Blue Compass Regional Manager

```json
{
  "iss": "https://rvserviceflow.auth0.com/",
  "sub": "auth0|xyz789",
  "aud": "https://api.rvserviceflow.com",
  "https://rvserviceflow.com/tenantId": "org_blue_compass_rv",
  "https://rvserviceflow.com/orgName": "Blue Compass RV",
  "https://rvserviceflow.com/roles": ["dealer:regional-manager"],
  "https://rvserviceflow.com/userId": "auth0|xyz789",
  "https://rvserviceflow.com/locationIds": ["loc_slc", "loc_denver", "loc_boise"],
  "https://rvserviceflow.com/regionTag": "west",
  "permissions": [
    "service-requests:read",
    "service-requests:search",
    "service-requests:create",
    "service-requests:update",
    "service-requests:update-service-event",
    "service-requests:delete",
    "attachments:read",
    "attachments:delete",
    "dealerships:read",
    "locations:read",
    "locations:update",
    "analytics:read",
    "lookups:read"
  ],
  "iat": 1741561200,
  "exp": 1741564800
}
```

### 8.2 Sample: Single-Location Advisor

```json
{
  "iss": "https://rvserviceflow.auth0.com/",
  "sub": "auth0|abc456",
  "aud": "https://api.rvserviceflow.com",
  "https://rvserviceflow.com/tenantId": "org_happy_trails_rv",
  "https://rvserviceflow.com/orgName": "Happy Trails RV",
  "https://rvserviceflow.com/roles": ["dealer:advisor"],
  "https://rvserviceflow.com/userId": "auth0|abc456",
  "https://rvserviceflow.com/locationIds": ["loc_happy_trails_boise"],
  "permissions": [
    "service-requests:read",
    "service-requests:search",
    "service-requests:create",
    "service-requests:update",
    "attachments:read",
    "dealerships:read",
    "locations:read",
    "lookups:read"
  ],
  "iat": 1741561200,
  "exp": 1741564800
}
```

### 8.3 Sample: Platform Admin (No Organization)

```json
{
  "iss": "https://rvserviceflow.auth0.com/",
  "sub": "auth0|platform001",
  "aud": "https://api.rvserviceflow.com",
  "https://rvserviceflow.com/roles": ["platform:admin"],
  "https://rvserviceflow.com/userId": "auth0|platform001",
  "permissions": [
    "service-requests:read",
    "service-requests:search",
    "service-requests:create",
    "service-requests:update",
    "service-requests:update-service-event",
    "service-requests:delete",
    "attachments:read",
    "attachments:delete",
    "dealerships:read",
    "dealerships:update",
    "locations:read",
    "locations:create",
    "locations:update",
    "analytics:read",
    "tenants:config:read",
    "tenants:config:create",
    "tenants:config:update",
    "lookups:read",
    "platform:tenants:manage",
    "platform:lookups:manage"
  ],
  "iat": 1741561200,
  "exp": 1741564800
}
```

Note: No `tenantId` claim — platform admins operate cross-tenant.

---

### 8.4 Sample: Location Technician

```json
{
  "iss": "https://rvserviceflow.auth0.com/",
  "sub": "auth0|tech001",
  "aud": "https://api.rvserviceflow.com",
  "https://rvserviceflow.com/tenantId": "org_blue_compass_rv",
  "https://rvserviceflow.com/orgName": "Blue Compass RV",
  "https://rvserviceflow.com/roles": ["dealer:technician"],
  "https://rvserviceflow.com/userId": "auth0|tech001",
  "https://rvserviceflow.com/locationIds": ["loc_blue_compass_slc"],
  "permissions": [
    "service-requests:read",
    "service-requests:search",
    "service-requests:update-service-event",
    "attachments:read",
    "attachments:upload",
    "dealerships:read",
    "locations:read",
    "lookups:read"
  ],
  "iat": 1741561200,
  "exp": 1741564800
}
```

Note: Technicians have `service-requests:search` (for "My Jobs" queue) and `attachments:upload` (for repair photo capture) but not `service-requests:update` (they can only update Section 10A fields via `service-requests:update-service-event`).

---

## 9. ClaimsService

`ClaimsService` is a `sealed` class registered as scoped. It reads the JWT claims injected by the Login Action and provides strongly-typed accessor methods used by controllers, services, and middleware. Follows the [MF ClaimsService pattern](https://github.com/markarnoldutah/MF/blob/8a37d47dd684403ea67176c3bb13c186c20c889d/MF.API/Services/ClaimsService.cs).

### 9.1 Claim Type Constants

| Constant | Value |
|---|---|
| `TenantIdClaimType` | `https://rvserviceflow.com/tenantId` |
| `RolesClaimType` | `https://rvserviceflow.com/roles` |
| `UserIdClaimType` | `https://rvserviceflow.com/userId` |
| `LocationIdsClaimType` | `https://rvserviceflow.com/locationIds` |
| `RegionTagClaimType` | `https://rvserviceflow.com/regionTag` |

### 9.2 Methods

**Tenant & User Identity:**

| Method | Returns | Description |
|---|---|---|
| `GetTenantIdOrThrow()` | `string` | Reads `tenantId` claim. Throws `UnauthorizedAccessException` if missing. |
| `GetTenantIdOrNull()` | `string?` | Returns `tenantId` or null (for platform admins). |
| `GetUserIdOrThrow()` | `string` | Reads `userId` claim. Throws if missing. |

**Roles:**

| Method | Returns | Description |
|---|---|---|
| `GetRoles()` | `List<string>` | All role strings from the `roles` claim array. |
| `IsInRole(string role)` | `bool` | Checks if the user has the specified role. |
| `IsPlatformAdmin()` | `bool` | Shortcut for `IsInRole("platform:admin")`. |
| `HasCorporateWideAccess()` | `bool` | Returns `true` if user is `platform:admin`, `dealer:corporate-admin`, or `dealer:owner`. These roles see all locations without restriction. |

**Location Scoping:**

| Method | Returns | Description |
|---|---|---|
| `GetLocationIds()` | `List<string>` | Location IDs from the `locationIds` claim. Empty list if not present (corporate-wide users). |
| `GetRegionTag()` | `string?` | Region tag from the `regionTag` claim. Null if not present. |
| `HasAccessToLocation(string locationId)` | `bool` | Returns `true` if: (a) user has corporate-wide access, OR (b) `locationId` is in user's `locationIds` list. This is the primary location-scoping check used throughout the service layer. |

**Permissions:**

| Method | Returns | Description |
|---|---|---|
| `HasPermission(string permission)` | `bool` | Checks the JWT `permissions` array for the specified permission string. |

### 9.3 How Location Scoping Works

Location scoping is **not** an Auth0-level concept — it's enforced in the RVS service layer using claims injected via `app_metadata`.

```
Corporate-admin (no locationIds claim)
  → HasCorporateWideAccess() = true
  → HasAccessToLocation("any_location") = true
  → Queries: WHERE tenantId = @t  (no location filter)

Regional manager (locationIds = ["loc_slc", "loc_denver", "loc_boise"])
  → HasCorporateWideAccess() = false
  → HasAccessToLocation("loc_slc") = true
  → HasAccessToLocation("loc_tampa") = false
  → Queries: WHERE tenantId = @t AND locationId IN (@loc_slc, @loc_denver, @loc_boise)

Location advisor (locationIds = ["loc_slc"])
  → HasCorporateWideAccess() = false
  → HasAccessToLocation("loc_slc") = true
  → HasAccessToLocation("loc_denver") = false
  → Queries: WHERE tenantId = @t AND locationId = @loc_slc
```

---

## 10. ASP.NET Authorization Policies

Authorization policies are registered in `Program.cs` using `AddAuthorizationBuilder()`. Each policy checks for the corresponding permission string in the JWT `permissions` claim.

### 10.1 Policy Definitions

| Policy Name | Required Permission |
|---|---|
| `CanReadServiceRequests` | `service-requests:read` |
| `CanSearchServiceRequests` | `service-requests:search` |
| `CanCreateServiceRequests` | `service-requests:create` |
| `CanUpdateServiceRequests` | `service-requests:update` |
| `CanUpdateServiceEvent` | `service-requests:update-service-event` |
| `CanDeleteServiceRequests` | `service-requests:delete` |
| `CanReadAttachments` | `attachments:read` |
| `CanUploadAttachments` | `attachments:upload` |
| `CanDeleteAttachments` | `attachments:delete` |
| `CanReadDealerships` | `dealerships:read` |
| `CanUpdateDealerships` | `dealerships:update` |
| `CanReadLocations` | `locations:read` |
| `CanCreateLocations` | `locations:create` |
| `CanUpdateLocations` | `locations:update` |
| `CanReadAnalytics` | `analytics:read` |
| `CanManageTenantConfig` | `tenants:config:read` OR `tenants:config:create` OR `tenants:config:update` |
| `CanReadLookups` | `lookups:read` |
| `PlatformAdmin` | `platform:tenants:manage` |

### 10.2 How Policies Are Applied

Policies are applied at the controller action level using `[Authorize(Policy = "PolicyName")]`. Example from `ServiceRequestsController`:

- `GET {id}` → `[Authorize(Policy = "CanReadServiceRequests")]`
- `POST search` → `[Authorize(Policy = "CanSearchServiceRequests")]`
- `PUT {id}` → `[Authorize(Policy = "CanUpdateServiceRequests")]`
- `DELETE {id}` → `[Authorize(Policy = "CanDeleteServiceRequests")]`

### 10.3 Two-Layer Authorization

Authorization in RVS is two-layered:

| Layer | Mechanism | What It Checks |
|---|---|---|
| **Layer 1: Permission** | ASP.NET `[Authorize(Policy)]` | Does the JWT contain the required permission? (e.g., `service-requests:read`) |
| **Layer 2: Data scope** | `ClaimsService` in service layer | Does this user have access to this specific location's data? (e.g., `HasAccessToLocation(sr.LocationId)`) |

Layer 1 runs in the ASP.NET authorization middleware (before the controller action). Layer 2 runs in the service layer (inside the action). Both must pass for the request to succeed.

Example: A `dealer:advisor` at location SLC requests a service request from location Denver.
- Layer 1: ✅ — advisor has `service-requests:read` permission.
- Layer 2: ❌ — `HasAccessToLocation("loc_denver")` returns `false` because advisor's `locationIds` only contains `["loc_slc"]`.
- Result: 403 Forbidden.

---

## 11. Auth0 Program.cs Configuration

### 11.1 Authentication Setup

The API is configured as an Auth0 JWT Bearer resource server:

- **Authority:** `https://rvserviceflow.auth0.com/`
- **Audience:** `https://api.rvserviceflow.com`
- **TokenValidationParameters:** Validate issuer, audience, lifetime, and signing key. `NameClaimType` set to `sub`. `RoleClaimType` set to `https://rvserviceflow.com/roles`.

### 11.2 Authorization Policy Registration

All policies from Section 10.1 are registered using `AddAuthorizationBuilder()` with `RequireClaim("permissions", "permission-string")` for each.

### 11.3 Pipeline Position

Authentication and authorization middleware run at position 6 in the pipeline (see **RVS_Core_Architecture.md** Section 9):

```
1. Dev-only endpoints (OpenAPI, Swagger)
2. HTTPS redirection (production only)
3. CORS
4. Rate limiting
5. ExceptionHandlingMiddleware
6. UseAuthentication() + UseAuthorization()    ← Auth0 JWT validation + policy checks
7. TenantAccessGateMiddleware                  ← Checks tenant is active
8. MapControllers()
```

---

## 12. Tenant Provisioning Flow (New Dealership Onboarding)

When a new dealership signs up for RVS:

```
Step 1: Generate Tenant ID
        ───────────────────
        Convention: "org_{slug}" (e.g., "org_blue_compass_rv")
        This becomes the Cosmos partition key, blob path prefix,
        and the tenantId claim in every user's JWT.

             │
             ▼

Step 2: Store in Cosmos
        ────────────────
        Create Dealership document:
          tenantId = "org_blue_compass_rv"
          corporateName = "Blue Compass RV"
          slug = "blue-compass-rv"
          isMultiLocation = true

        Create TenantConfig document:
          tenantId = "org_blue_compass_rv"
          accessGate = { isActive: true, ... }

        Create initial Location document(s):
          tenantId = "org_blue_compass_rv"
          slug = "blue-compass-salt-lake"
          displayName = "Blue Compass RV - Salt Lake City"
          regionTag = "west"

             │
             ▼

Step 3: Create Owner User in Auth0
        ───────────────────────────
        POST /api/v2/users  (Auth0 Management API)
        {
          "email": "owner@bluecompassrv.com",
          "connection": "Username-Password-Authentication",
          "app_metadata": {
            "tenantId": "org_blue_compass_rv",
            "orgName": "Blue Compass RV"
          }
        }

        Assign role: dealer:owner (global Auth0 role)
        Owner receives password-reset email → sets password → lands in dashboard

             │
             ▼

Step 4: Owner Invites Staff
        ───────────────────
        Via dashboard UI (future) or Auth0 Dashboard:
        - Create Auth0 user with matching app_metadata.tenantId
        - Assign role (dealer:manager, dealer:advisor, etc.)
        - Set app_metadata.locationIds per user
        - Set app_metadata.regionTag for regional managers

             │
             ▼

Step 5: Ready
        ─────
        Intake URL is live:
          rvserviceflow.com/intake/blue-compass-salt-lake
        Dashboard is accessible:
          dashboard.rvserviceflow.com
```

---

## 13. Future: Customer Authentication (Phase 2+)

In MVP, customers are anonymous. In Phase 2+, customers who want persistent account access can:

1. **Opt-in to create an Auth0 account** from the status page.
2. Auth0 creates a user. The `CustomerIdentity.Auth0UserId` field is populated.
3. A `customer` role is assigned. Customers have no `tenantId` in `app_metadata` — they are cross-dealer.
4. The customer can now log in to see their status page without a magic link, access history, manage vehicles, etc.

Customer authentication does not use tenant scoping. The customer's `CustomerIdentity` links to their tenant-scoped `CustomerProfile` records, which are what the dealer sees.

---

## 14. Security Summary

| Concern | Mitigation |
|---|---|
| **Tenant isolation** | `tenantId` from JWT claim → Cosmos partition key. No cross-tenant data leakage possible at the data layer. |
| **Location isolation** | `ClaimsService.HasAccessToLocation()` enforced in service layer. Location-scoped users cannot see other locations' data even within the same tenant. |
| **Permission enforcement** | Two-layer: ASP.NET policy (permission check) + service layer (data scope check). |
| **Token security** | RS256 signed, 1-hour expiry, audience-validated. |
| **Claim injection** | All custom claims injected server-side by Auth0 Login Action from `app_metadata`. Client cannot forge claims. |
| **Platform admin scope** | `platform:admin` has no `tenantId` claim. API explicitly handles the cross-tenant case. |
| **Magic link tokens** | 32-byte cryptographic random, 30-day expiry, rotated on every submission, rate-limited. |
| **PII in JWTs** | No PII in access tokens. Only IDs, roles, and permissions. Email/name are in the ID token only (used client-side). |
| **`app_metadata` integrity** | `app_metadata` is writable only via the Auth0 Management API (server-side). Users cannot modify their own `app_metadata` through client-side flows. |

---

## 15. Version History

| Version | Date | Author | Changes |
|---|---|---|---|
| v2.0 | March 10, 2026 | GitHub Copilot | Initial Auth0 identity document. Roles, permissions, JWT design, ClaimsService, authorization policies, tenant provisioning flow. |
| v2.1 | March 18, 2026 | GitHub Copilot | **Reconciled with RVS_Core_Architecture_Version3.md Section 17.1 (Technician Mobile App — API Readiness).** Date bumped. Two critical permission additions confirmed present (see below). Companion document reference updated to Version3. |
| v2.2 | March 25, 2026 | GitHub Copilot | **Removed Auth0 Organizations.** Tenant scoping is permanently via `app_metadata`. Removed `org_id` from JWT samples. Rewrote Sections 1, 7, 12, 13, 14 to reflect `app_metadata`-only architecture. Simplified provisioning flow. |

### 15.1 V3 Reconciliation — Technician Mobile App Permission Fixes

The following permissions were resolved as part of the V3 Core Architecture's Section 17.1 gap analysis and are confirmed present in this document:

| Gap (from Section 17.1) | Fix | Location in This Document | Status |
|---|---|---|---|
| `dealer:technician` was missing `service-requests:search` | Added permission to role → permission matrix | Section 6 (row: `service-requests:search`, column: `dealer:technician` = ✅ ²) | ✅ Confirmed |
| No authenticated attachment upload for dealer staff | `attachments:upload` added for `dealer:technician` | Section 6 (row: `attachments:upload`, column: `dealer:technician` = ✅) | ✅ Confirmed |

**Footnote ²** in Section 6 documents the application-layer restriction that accompanies the `service-requests:search` grant: the service layer restricts technician search results to SRs where `AssignedTechnicianId` matches the technician's own `userId` claim. This powers the **My Jobs** queue (QR/VIN scan → job lookup, bay-based access) on the technician mobile app documented in `Docs/Research/FrontEnd/RVS_Features_MAUI.Tech.md`.

**Section 8.4** (Sample JWT — Location Technician) confirms both permissions appear in the technician access token payload.