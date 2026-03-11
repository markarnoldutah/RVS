

Let me start by reading the architecture document to understand the RVS system before proposing Auth0 roles and permissions.Based on a thorough review of the RVS architecture document, here is a complete Auth0 roles and permissions proposal tailored to every actor, controller, and access pattern in the system.

---

# Auth0 Roles & Permissions Proposal for RVS

## 1. Auth0 Tenant & Organization Model

RVS is multi-tenant where **each dealership = one Auth0 Organization**. This maps directly to your existing `tenantId` / `Dealership` model.

| Auth0 Concept | RVS Equivalent | Example |
|---|---|---|
| **Auth0 Tenant** | Your single Auth0 application instance | `rvserviceflow.auth0.com` |
| **Auth0 Organization** | One RV dealership (`Dealership.TenantId`) | `org_camping_world_slc` |
| **Auth0 API** | `RVS.API` audience | `https://api.rvserviceflow.com` |

**Custom Namespace:** `https://rvserviceflow.com/` (matches your existing `ClaimsService` constants)

---

## 2. Roles

Roles are **scoped to an Organization** (a dealer user at Dealership A may be an Admin, but have no access to Dealership B). One global role exists for platform operations.

| Role | Scope | Description |
|---|---|---|
| **`platform:admin`** | Global (cross-org) | RVS platform operators. Can manage all tenants, access gates, and lookups. |
| **`dealer:owner`** | Per Organization | Dealership owner/GM. Full control of their dealership's config, users, analytics, and all service requests. |
| **`dealer:manager`** | Per Organization | Service department manager. Can manage service requests, view analytics, and manage dealership settings (not user management). |
| **`dealer:advisor`** | Per Organization | Service advisor / front-desk staff. Creates, updates, and searches service requests. Cannot access analytics or dealership config. |
| **`dealer:technician`** | Per Organization | Technician. Can view assigned service requests and update service event (Section 10A) fields only. |
| **`dealer:readonly`** | Per Organization | Read-only observer (e.g., accounting, external auditor). Can view service requests and analytics but cannot modify anything. |

> **Customers are never Auth0 users in MVP.** They are `[AllowAnonymous]` actors with shadow profiles and magic-link tokens. In Phase 2+, customers who opt in get the `customer` role tied to their `CustomerIdentity.Auth0UserId`.

---

## 3. Permissions

Fine-grained permissions assigned to roles. These are carried in the JWT `access_token` and enforced server-side.

### 3.1 Service Requests

| Permission | Description | Used By Controller |
|---|---|---|
| `service-requests:read` | View service request details | `ServiceRequestsController`, `AttachmentsController` |
| `service-requests:search` | Search / filter service requests | `ServiceRequestsController` (POST search) |
| `service-requests:create` | Create a service request from the dealer dashboard | `ServiceRequestsController` |
| `service-requests:update` | Update service request (status, notes, category) | `ServiceRequestsController` |
| `service-requests:update-service-event` | Update Section 10A fields (repair action, parts, labor) | `ServiceRequestsController` |
| `service-requests:delete` | Delete a service request | `ServiceRequestsController` |

### 3.2 Attachments

| Permission | Description | Used By Controller |
|---|---|---|
| `attachments:read` | View / download attachments (SAS URL) | `AttachmentsController` |
| `attachments:delete` | Delete an attachment | `AttachmentsController` |

### 3.3 Dealerships

| Permission | Description | Used By Controller |
|---|---|---|
| `dealerships:read` | View dealership details and list | `DealershipsController` |
| `dealerships:update` | Update dealership settings (logo, contact, intake config) | `DealershipsController` |
| `dealerships:qr-code` | Generate QR code for intake form | `DealershipsController` |

### 3.4 Analytics

| Permission | Description | Used By Controller |
|---|---|---|
| `analytics:read` | View service request analytics / summaries | `AnalyticsController` |

### 3.5 Tenant Configuration

| Permission | Description | Used By Controller |
|---|---|---|
| `tenants:config:read` | View tenant configuration and access gate | `TenantsController` |
| `tenants:config:create` | Bootstrap tenant configuration | `TenantsController` |
| `tenants:config:update` | Update tenant configuration | `TenantsController` |

### 3.6 Lookups

| Permission | Description | Used By Controller |
|---|---|---|
| `lookups:read` | Read lookup sets (issue categories, component types) | `LookupsController` |

### 3.7 Platform Administration (future/global)

| Permission | Description | Used By |
|---|---|---|
| `platform:tenants:manage` | Create/disable/enable tenants across the platform | Platform admin tooling |
| `platform:lookups:manage` | Create/update global lookup sets | Platform admin tooling |

---

## 4. Role → Permission Matrix

| Permission | `platform:admin` | `dealer:owner` | `dealer:manager` | `dealer:advisor` | `dealer:technician` | `dealer:readonly` |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| `service-requests:read` | ✅ | ✅ | ✅ | ✅ | ✅ *(assigned only)* | ✅ |
| `service-requests:search` | ✅ | ✅ | ✅ | ✅ | — | ✅ |
| `service-requests:create` | ✅ | ✅ | ✅ | ✅ | — | — |
| `service-requests:update` | ✅ | ✅ | ✅ | ✅ | — | — |
| `service-requests:update-service-event` | ✅ | ✅ | ✅ | — | ✅ | — |
| `service-requests:delete` | ✅ | ✅ | ✅ | — | — | — |
| `attachments:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `attachments:delete` | ✅ | ✅ | ✅ | — | — | — |
| `dealerships:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `dealerships:update` | ✅ | ✅ | ✅ | — | — | — |
| `dealerships:qr-code` | ✅ | ✅ | ✅ | ✅ | — | — |
| `analytics:read` | ✅ | ✅ | ✅ | — | — | ✅ |
| `tenants:config:read` | ✅ | ✅ | — | — | — | — |
| `tenants:config:create` | ✅ | ✅ | — | — | — | — |
| `tenants:config:update` | ✅ | ✅ | — | — | — | — |
| `lookups:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `platform:tenants:manage` | ✅ | — | — | — | — | — |
| `platform:lookups:manage` | ✅ | — | — | — | — | — |

---

## 5. Auth0 Configuration

### 5.1 Auth0 API (Resource Server)

```
Identifier:  https://api.rvserviceflow.com
Signing:     RS256
Token Expiration: 3600s (1 hour)
RBAC Settings:
  ✅ Enable RBAC
  ✅ Add Permissions in the Access Token
```

### 5.2 Auth0 Action — Enrich Access Token

This Login Action injects the `tenantId` (from the Organization) and roles/permissions into the JWT so `ClaimsService` can read them:

```javascript name=auth0-actions/enrich-access-token.js
exports.onExecutePostLogin = async (event, api) => {
  const namespace = "https://rvserviceflow.com";

  // Organization context (maps to RVS tenantId)
  if (event.organization) {
    api.accessToken.setCustomClaim(`${namespace}/tenantId`, event.organization.id);
    api.accessToken.setCustomClaim(`${namespace}/orgName`, event.organization.name);
  }

  // Roles assigned within the organization
  const roles = event.authorization?.roles || [];
  api.accessToken.setCustomClaim(`${namespace}/roles`, roles);

  // User ID (maps to CreatedByUserId / UpdatedByUserId)
  api.accessToken.setCustomClaim(`${namespace}/userId`, event.user.user_id);
};
```

### 5.3 Resulting JWT Claims

```json name=sample-access-token-claims.json
{
  "iss": "https://rvserviceflow.auth0.com/",
  "sub": "auth0|abc123",
  "aud": "https://api.rvserviceflow.com",
  "org_id": "org_camping_world_slc",
  "https://rvserviceflow.com/tenantId": "org_camping_world_slc",
  "https://rvserviceflow.com/roles": ["dealer:manager"],
  "https://rvserviceflow.com/userId": "auth0|abc123",
  "permissions": [
    "service-requests:read",
    "service-requests:search",
    "service-requests:create",
    "service-requests:update",
    "service-requests:delete",
    "attachments:read",
    "attachments:delete",
    "dealerships:read",
    "dealerships:update",
    "dealerships:qr-code",
    "analytics:read",
    "lookups:read"
  ],
  "exp": 1741564800
}
```

---

## 6. RVS Backend Integration

### 6.1 Updated `ClaimsService`

```csharp name=RVS.API/Services/ClaimsService.cs
public sealed class ClaimsService
{
    public const string TenantIdClaimType = "https://rvserviceflow.com/tenantId";
    public const string RolesClaimType = "https://rvserviceflow.com/roles";
    public const string UserIdClaimType = "https://rvserviceflow.com/userId";
    public const string PermissionsClaimType = "permissions";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("User is not available in the current context.");

    public string GetTenantIdOrThrow()
    {
        var tenantId = User.FindFirst(TenantIdClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new UnauthorizedAccessException("Tenant identifier is missing.");
        return tenantId;
    }

    public string GetUserIdOrThrow()
    {
        var userId = User.FindFirst(UserIdClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("User identifier is missing.");
        return userId;
    }

    public List<string> GetRoles() =>
        User.FindAll(RolesClaimType).Select(c => c.Value).ToList();

    public bool HasPermission(string permission) =>
        User.FindAll(PermissionsClaimType).Any(c => c.Value == permission);

    public bool IsInRole(string role) =>
        GetRoles().Contains(role, StringComparer.OrdinalIgnoreCase);

    public bool IsPlatformAdmin() =>
        IsInRole("platform:admin");
}
```

### 6.2 Authorization Policies in `Program.cs`

```csharp name=RVS.API/Program.cs (authorization policies)
builder.Services.AddAuthorization(options =>
{
    // Service Requests
    options.AddPolicy("CanReadServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:read"));

    options.AddPolicy("CanSearchServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:search"));

    options.AddPolicy("CanCreateServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:create"));

    options.AddPolicy("CanUpdateServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:update"));

    options.AddPolicy("CanUpdateServiceEvent", policy =>
        policy.RequireClaim("permissions", "service-requests:update-service-event"));

    options.AddPolicy("CanDeleteServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:delete"));

    // Attachments
    options.AddPolicy("CanReadAttachments", policy =>
        policy.RequireClaim("permissions", "attachments:read"));

    options.AddPolicy("CanDeleteAttachments", policy =>
        policy.RequireClaim("permissions", "attachments:delete"));

    // Dealerships
    options.AddPolicy("CanReadDealerships", policy =>
        policy.RequireClaim("permissions", "dealerships:read"));

    options.AddPolicy("CanUpdateDealerships", policy =>
        policy.RequireClaim("permissions", "dealerships:update"));

    // Analytics
    options.AddPolicy("CanReadAnalytics", policy =>
        policy.RequireClaim("permissions", "analytics:read"));

    // Tenant Config
    options.AddPolicy("CanManageTenantConfig", policy =>
        policy.RequireClaim("permissions", "tenants:config:update"));

    // Platform Admin
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireClaim("https://rvserviceflow.com/roles", "platform:admin"));
});
```

### 6.3 Controller Usage Example

```csharp name=RVS.API/Controllers/ServiceRequestsController.cs
[ApiController]
[Route("api/dealerships/{dealershipId}/service-requests")]
[Authorize]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _serviceRequestService;
    private readonly ClaimsService _claimsService;

    [HttpGet("{serviceRequestId}")]
    [Authorize(Policy = "CanReadServiceRequests")]
    public async Task<IActionResult> GetServiceRequest(
        string dealershipId, string serviceRequestId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();
        var entity = await _serviceRequestService.GetServiceRequestAsync(
            tenantId, serviceRequestId);
        return Ok(entity.ToDetailDto());
    }

    [HttpPost("search")]
    [Authorize(Policy = "CanSearchServiceRequests")]
    public async Task<IActionResult> SearchServiceRequests(
        string dealershipId, ServiceRequestSearchRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();
        var results = await _serviceRequestService.SearchServiceRequestsAsync(
            tenantId, request);
        return Ok(results.ToSearchResultDto());
    }

    [HttpPut("{serviceRequestId}")]
    [Authorize(Policy = "CanUpdateServiceRequests")]
    public async Task<IActionResult> UpdateServiceRequest(
        string dealershipId, string serviceRequestId,
        ServiceRequestUpdateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();
        var entity = await _serviceRequestService.UpdateServiceRequestAsync(
            tenantId, serviceRequestId, request);
        return Ok(entity.ToDetailDto());
    }

    [HttpDelete("{serviceRequestId}")]
    [Authorize(Policy = "CanDeleteServiceRequests")]
    public async Task<IActionResult> DeleteServiceRequest(
        string dealershipId, string serviceRequestId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();
        await _serviceRequestService.DeleteServiceRequestAsync(
            tenantId, serviceRequestId);
        return NoContent();
    }
}
```

---

## 7. Access Pattern Summary by Actor

| Actor | Auth | Endpoints Accessible | Tenant Isolation |
|---|---|---|---|
| **Customer (intake)** | Anonymous + rate limit | `POST /api/intake/{slug}/service-requests`, `POST .../attachments` | Dealership slug resolves tenantId |
| **Customer (status)** | Anonymous + magic-link token | `GET /api/status/{token}` | Token resolves `CustomerIdentity` → cross-dealer |
| **Technician** | Auth0 JWT `dealer:technician` | Read assigned SRs, update Section 10A fields | `tenantId` from JWT claim |
| **Service Advisor** | Auth0 JWT `dealer:advisor` | CRUD service requests, upload attachments, read lookups | `tenantId` from JWT claim |
| **Service Manager** | Auth0 JWT `dealer:manager` | Everything an advisor can + delete SRs, analytics, dealership config | `tenantId` from JWT claim |
| **Dealership Owner** | Auth0 JWT `dealer:owner` | Everything a manager can + tenant config management | `tenantId` from JWT claim |
| **Platform Admin** | Auth0 JWT `platform:admin` | All endpoints across all organizations | Explicit tenantId in routes |

---

## 8. Phase 2+ Customer Auth (Future)

When customers optionally create Auth0 accounts:

| Role | Scope | Permissions |
|---|---|---|
| `customer` | Global (cross-org) | `customer:status:read`, `customer:profile:read`, `customer:profile:update` |

The `CustomerIdentity.Auth0UserId` field (already in your entity) gets linked, and the magic-link flow becomes a fallback for non-registered customers.

---

## 9. Auth0 Organization Provisioning Flow

When a new dealership is onboarded:

1. **Create Auth0 Organization** → returns `org_id`
2. **Store `org_id` as `TenantId`** in Cosmos `config` and `dealerships` containers
3. **Create initial `dealer:owner` user** → invite via Auth0
4. **Owner invites staff** → assigns `dealer:manager`, `dealer:advisor`, `dealer:technician` roles within the organization
5. **Bootstrap `TenantConfig`** → `POST /api/tenants/config` with access gate enabled

This aligns perfectly with your `TenantAccessGateMiddleware` — a new org starts with the gate enabled until the owner completes setup.

---

**Key design decisions:**
- **Permissions in the token** (not just roles) so the API never needs to call Auth0 at runtime — zero external latency on every request
- **Organization-scoped roles** so the same user could theoretically work at multiple dealerships with different roles
- **`tenantId` = Auth0 `org_id`** — single source of truth, no mapping table needed
- **Technician has `update-service-event` but not `update`** — they can fill in repair details without changing status or customer info