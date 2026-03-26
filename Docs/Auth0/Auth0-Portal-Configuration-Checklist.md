# Auth0 Portal Configuration Checklist

**Updated:** March 25, 2026
**Source:** RVS_Auth0_Identity_Version2.md (v2.2), RVS_Technical_PRD.md, Program.cs, ClaimsService.cs

---

## Architecture Note

RVS does **not** use Auth0 Organizations. Tenant context is stored in each user's `app_metadata` and injected into the JWT via a Post-Login Action. This approach:

- Runs on the **Auth0 Free plan** with no tenant/organization cap.
- Supports unlimited dealer corporations without paid tier upgrades.
- Keeps all tenant-scoping logic in the RVS service layer (via `ClaimsService`), not in Auth0.

Each user's `app_metadata.tenantId` becomes the `tenantId` used throughout RVS — the Cosmos DB partition key, the blob storage path prefix, and the data isolation boundary.

---

## 1. API (Resource Server)

Create under **Applications > APIs**:

| Setting | Value |
|---|---|
| **Name** | `RVS API` |
| **Identifier (Audience)** | `https://api.rvserviceflow.com` |
| **Signing Algorithm** | RS256 |
| **Token Expiration** | 3600 (1 hour) |
| **Token Expiration for Browser Flows** | 3600 |
| **Allow Offline Access** | Enabled (for refresh tokens) |
| **RBAC Settings > Enable RBAC** | Enabled |
| **RBAC Settings > Add Permissions in the Access Token** | Enabled |

---

## 2. API Permissions

Define all 21 permissions on the API under **APIs > RVS API > Permissions**:

| Permission | Description |
|---|---|
| `service-requests:read` | View service request details |
| `service-requests:search` | Search / filter service requests |
| `service-requests:create` | Create a service request from the dealer dashboard |
| `service-requests:update` | Update service request (status, notes, category) |
| `service-requests:update-service-event` | Update Section 10A fields (repair action, parts, labor) |
| `service-requests:delete` | Delete a service request |
| `attachments:read` | View / download attachments |
| `attachments:upload` | Upload an attachment |
| `attachments:delete` | Delete an attachment |
| `dealerships:read` | View dealership details |
| `dealerships:update` | Update dealership settings |
| `locations:read` | View location details and list |
| `locations:create` | Create a new physical location |
| `locations:update` | Update location settings |
| `analytics:read` | View service request analytics |
| `tenants:config:read` | View tenant configuration |
| `tenants:config:create` | Bootstrap tenant configuration |
| `tenants:config:update` | Update tenant configuration |
| `lookups:read` | Read lookup sets |
| `platform:tenants:manage` | Create / disable / enable tenants |
| `platform:lookups:manage` | Create / update global lookup sets |

---

## 3. Roles

Create under **User Management > Roles**. Assign the permissions per the matrix below:

| Role | Permissions |
|---|---|
| **`platform:admin`** | All 21 permissions |
| **`dealer:corporate-admin`** | All except `platform:tenants:manage`, `platform:lookups:manage` (19 permissions) |
| **`dealer:owner`** | Same as `dealer:corporate-admin` (19 permissions) |
| **`dealer:regional-manager`** | `service-requests:read`, `search`, `create`, `update`, `update-service-event`, `delete` + `attachments:read`, `upload`, `delete` + `dealerships:read` + `locations:read`, `update` + `analytics:read` + `lookups:read` (14 permissions) |
| **`dealer:manager`** | Same as `dealer:regional-manager` (14 permissions) |
| **`dealer:advisor`** | `service-requests:read`, `search`, `create`, `update` + `attachments:read`, `upload` + `dealerships:read` + `locations:read` + `lookups:read` (9 permissions) |
| **`dealer:technician`** | `service-requests:read`, `search`, `update-service-event` + `attachments:read`, `upload` + `dealerships:read` + `locations:read` + `lookups:read` (8 permissions) |
| **`dealer:readonly`** | `service-requests:read`, `search` + `attachments:read` + `dealerships:read` + `locations:read` + `analytics:read` + `lookups:read` (7 permissions) |

---

## 4. Application (SPA)

Create under **Applications > Applications** (type: Single Page Application):

| Setting | Value |
|---|---|
| **Name** | `RVS Blazor Client` |
| **Application Type** | Single Page Application |
| **Allowed Callback URLs** | `https://localhost:7008/authentication/login-callback`, `http://localhost:5050/authentication/login-callback`, `https://app.rvserviceflow.com/authentication/login-callback` |
| **Allowed Logout URLs** | `https://localhost:7008`, `http://localhost:5050`, `https://app.rvserviceflow.com` |
| **Allowed Web Origins** | `https://localhost:7008`, `http://localhost:5050`, `https://app.rvserviceflow.com` |
| **Token Endpoint Auth Method** | None (SPA) |
| **Refresh Token Rotation** | Enabled |
| **Refresh Token Expiration** | 15 days rolling (Auth0 default — adjust per UX requirements) |

---

## 5. Custom Claims Namespace

All custom claims use the namespace: **`https://rvserviceflow.com/`**

---

## 6. Post-Login Action (Actions > Flows > Login)

Create a Login Action named e.g. `Enrich Access Token` that injects these custom claims:

| Claim | Namespace + Key | Source |
|---|---|---|
| **Tenant ID** | `https://rvserviceflow.com/tenantId` | `event.user.app_metadata.tenantId` |
| **Org Name** | `https://rvserviceflow.com/orgName` | `event.user.app_metadata.orgName` |
| **Roles** | `https://rvserviceflow.com/roles` | `event.authorization.roles` |
| **User ID** | `https://rvserviceflow.com/userId` | `event.user.user_id` |
| **Location IDs** | `https://rvserviceflow.com/locationIds` | `event.user.app_metadata.locationIds` |
| **Region Tag** | `https://rvserviceflow.com/regionTag` | `event.user.app_metadata.regionTag` |

### Sample Action Code

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const namespace = "https://rvserviceflow.com/";
  const md = event.user.app_metadata;

  if (md?.tenantId) {
    api.accessToken.setCustomClaim(`${namespace}tenantId`, md.tenantId);
  }
  if (md?.orgName) {
    api.accessToken.setCustomClaim(`${namespace}orgName`, md.orgName);
  }
  if (event.authorization?.roles?.length) {
    api.accessToken.setCustomClaim(`${namespace}roles`, event.authorization.roles);
  }
  api.accessToken.setCustomClaim(`${namespace}userId`, event.user.user_id);

  if (md?.locationIds) {
    api.accessToken.setCustomClaim(`${namespace}locationIds`, md.locationIds);
  }
  if (md?.regionTag) {
    api.accessToken.setCustomClaim(`${namespace}regionTag`, md.regionTag);
  }
};
```

---

## 7. User `app_metadata` Structure (per user)

Set on each user under **User Management > Users > {user} > app_metadata**:

```json
{
  "tenantId": "org_blue_compass_rv",
  "orgName": "Blue Compass RV",
  "locationIds": ["loc_blue_compass_slc"],
  "regionTag": "west"
}
```

- **`tenantId`** — required for all dealer staff; maps to Cosmos partition key
- **`orgName`** — display name for the corporation
- **`locationIds`** — array of location IDs the user can access; omit or leave empty for corporate-wide roles (`dealer:corporate-admin`, `dealer:owner`)
- **`regionTag`** — optional; only for `dealer:regional-manager` users

---

## 8. Token Settings

| Setting | Value |
|---|---|
| **Access Token Lifetime** | 3600 seconds (1 hour) |
| **Refresh Token Lifetime** | 1,296,000 seconds (15 days, rolling — Auth0 default; adjust per UX requirements) |
| **ID Token Lifetime** | 36000 seconds (default) |
| **Client storage** | Memory-only or `sessionStorage` — **never** `localStorage` |

---

## 9. Blazor Client Configuration Fix Needed

**Current mismatch detected** — the Blazor WASM dev config points to a different Auth0 tenant than the API:

| Config | API | Blazor WASM |
|---|---|---|
| **Authority/Domain** | `https://dev-rvserviceflow.us.auth0.com/` | `https://dev-2jhzz8xmjggh26pm.us.auth0.com/` |
| **Audience** | `https://api.rvserviceflow.com` | `https://api.benefetch.com` (dev) |

Both apps must use the same Auth0 tenant domain and audience. Ensure the Blazor client's `Authority` and `Audience` match:

- **Authority:** `https://dev-rvserviceflow.us.auth0.com/`
- **Audience:** `https://api.rvserviceflow.com`

---

## 10. Scopes (requested by Blazor SPA at login)

The SPA should request these scopes in the authorization request:

| Scope | Purpose |
|---|---|
| `openid` | Required for OIDC |
| `profile` | User name/avatar |
| `email` | User email |
| `offline_access` | Refresh token support |

The `permissions` are **not** requested as scopes — they're automatically included in the access token because "Add Permissions in Access Token" is enabled on the API.

---

## 11. Design Tradeoffs (from ASOT)

| Concern | How RVS Handles It |
|---|---|
| **User isolation** | Enforced by `ClaimsService` + Cosmos partition key, not by Auth0 membership boundaries |
| **Role scoping** | Roles are global Auth0 roles (not per-org). A user belongs to exactly one tenant via `app_metadata.tenantId` |
| **Invitation flows** | Handled via Auth0 Dashboard or Management API; owner invites staff by creating users with matching `tenantId` |
| **Per-tenant IdP** | Not available without Organizations; all tenants share the same Auth0 login experience |
| **Branded login** | Not available without Organizations; deferred to a future paid tier if needed |

---

## 12. Summary of Auth0 Portal Sections to Touch

1. **APIs** — Create `RVS API` with audience, RS256, RBAC enabled, permissions defined
2. **Applications** — Create SPA app with callback/logout/origin URLs
3. **Roles** — Create 8 roles with correct permission assignments
4. **Actions > Login Flow** — Deploy the Post-Login Action for custom claims
5. **Users** — Set `app_metadata` (tenantId, orgName, locationIds, regionTag) per user
6. **Organizations** — Not used. Tenant scoping is permanently via `app_metadata`
