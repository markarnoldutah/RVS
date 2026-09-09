# RVS — Identity and Authorization

**Version:** 1.0 · September 4, 2026
**Scope:** Auth0 configuration and the API authorization model, as built.

Only the Manager app authenticates. **The intake app is anonymous and must stay that way** — no auth packages, no token handler, no `AuthorizeRouteView`. Customers are never Auth0 users; they reach their own data through an anonymous, rate-limited token endpoint.

---

## Tenant model

Auth0 tenant `rvserviceflow.auth0.com`, API audience `https://api.rvserviceflow.com`, custom claim namespace `https://rvserviceflow.com/`.

**RVS does not use Auth0 Organizations.** Tenant context lives in each user's `app_metadata` and is injected into the access token by a Post-Login Action (`Auth0/Add metadata to accessToken.js`). This runs on the Auth0 Free plan with no organization cap, and keeps tenant-scoping logic in `ClaimsService` rather than in Auth0.

`app_metadata` carries `tenantId`, `orgName`, and optionally `locationIds` and `regionTag`. `app_metadata.tenantId` is the value used everywhere downstream: the Cosmos partition key, the blob path prefix, and the isolation boundary. Its values are conventionally shaped like `org_blue_compass_rv`, but they are ordinary strings — **not** Auth0 organization identifiers.

Consequences worth knowing: there is no per-tenant identity provider and no branded login, both of which would require Organizations. Staff invitations go through the Auth0 dashboard or Management API — an owner adds a user with a matching `tenantId`.

---

## Roles

Roles are global Auth0 roles. A user at Corporation A cannot see Corporation B because their `tenantId` differs, not because of any Auth0 membership boundary.

| Role | Scope | Under new scope |
|---|---|---|
| `platform:admin` | Cross-tenant | RVS internal staff. The `PlatformAdmin` policy exists but no endpoint uses it |
| `dealer:owner` | Tenant | Keep |
| `dealer:corporate-admin` | Tenant | Keep |
| `dealer:regional-manager` | Tenant + region | Thin — multi-location coordination is archived |
| `dealer:manager` | Location | Keep — the primary user of the manager app |
| `dealer:advisor` | Location | Keep |
| `dealer:readonly` | Location | Keep |
| `dealer:technician` | Location | **Archived** — there is no technician workflow |

At the reduced scope the role set is larger than the product needs. Collapsing it is cheap and safe to defer; the permission model below is what actually gates anything.

---

## Permissions

Authorization is **per-permission, never per-role**. Policies are declared in `RVS.API/Program.cs` and each requires a single `permissions` claim value.

| Policy | Claim | Notes |
|---|---|---|
| `CanReadServiceRequests` | `service-requests:read` | |
| `CanSearchServiceRequests` | `service-requests:search` | |
| `CanCreateServiceRequests` | `service-requests:create` | |
| `CanUpdateServiceRequests` | `service-requests:update` | |
| `CanUpdateServiceEvent` | `service-requests:update-service-event` | **Archived** — technician outcome capture |
| `CanDeleteServiceRequests` | `service-requests:delete` | |
| `CanUploadAttachments` | `attachments:upload` | |
| `CanReadAttachments` | `attachments:read` | |
| `CanDeleteAttachments` | `attachments:delete` | |
| `CanReadDealerships` | `dealerships:read` | |
| `CanUpdateDealerships` | `dealerships:update` | |
| `CanReadLocations` | `locations:read` | |
| `CanCreateLocations` | `locations:create` | |
| `CanUpdateLocations` | `locations:update` | |
| `CanReadAnalytics` | `analytics:read` | **Archived** — analytics is out of scope |
| `CanManageTenantConfig` | `tenants:config:read` / `create` / `update` | Any one of the three satisfies it |
| `CanReadLookups` | `lookups:read` | |
| `PlatformAdmin` | `platform:tenants:manage` | Declared, unused |

Packet delivery (Spec B) will need new permissions — at minimum resend and per-location packet configuration. Neither exists yet.

---

## Claims and user context

`ClaimsService` (scoped) owns the claim-type constants and all extraction. `GetTenantIdOrThrow()` throws `UnauthorizedAccessException` — surfacing as 401 — when the tenant claim is absent. Every controller action opens with it.

Services never touch `HttpContext`. `IUserContextAccessor` lives in Domain and exposes `UserId` and `TenantId`; `HttpUserContextAccessor` in the API reads from `IHttpContextAccessor` and is registered scoped. Audit fields on every entity come from `_userContext.UserId`.

---

## Manager app authentication

Auth0 OIDC with PKCE. `AddOidcAuthentication`, `ResponseType = "code"`, scopes `openid profile email offline_access`, audience `https://api.rvserviceflow.com`. The bearer token is attached by `AuthorizationMessageHandler` on the named `RVS.API` client.

A custom `RefreshingAccessTokenProvider` decorates `IAccessTokenProvider` and exchanges refresh tokens directly against Auth0's `/oauth/token`, rather than relying on iframe silent renewal. The app-wide `FallbackPolicy` requires an authenticated user; only `/` and `/authentication/{action}` are anonymous.

---

## Anonymous token surfaces

Two endpoints serve unauthenticated users, both rate-limited **per caller IP** (`RateLimitPartition` on the remote address): the intake endpoints (`IntakeEndpoint`, 20/min) and the customer status feed (`StatusEndpoint`, 10/min).

Spec X-5 requires that every anonymous token be ≥128 bits of entropy, **stored hashed**, TTL-bounded, rate-limited per IP, access-audited, and read-only except for the single status write in C-7.

**Status token — built (issue #440).** The per-customer status token is now `AnonymousTokenHelper.GenerateStatusToken(email)` — `base64url(SHA256(email)[..8]) : base64url(16 random bytes)` — and only its SHA-256 hash is persisted, on `GlobalCustomerAcct.magicLinkTokenHash` (indexed; the raw token is never stored). `ValidateMagicLinkTokenAsync` hashes the incoming token, looks up by hash, checks expiry (`MagicLinkExpiredException` → 410), extends the TTL to 30 days once it drops inside the 29-day renewal window (sliding renewal), and audit-logs every attempt as `outcome=hit|miss|expired` with a hashed email and no raw token. Intake mints a fresh status token on every submission (a still-valid token cannot be recovered from its hash). Existing plaintext tokens are migrated in #441.

**C-7 action links — still pending.** The per-request/per-action one-click links reuse `AnonymousTokenHelper` (bare `GenerateRawToken`, no prefix) but the storage, consume path, and endpoint are not built yet. The prior ARCHIVE decision (`Docs/ARCHIVE/ASOT/RVS_MagicLink_Storage_Guidance.md`) that argued unhashed storage was adequate is overturned — its own stated trigger, a token that can write, is met by C-7. See `RVS_Architecture.md` and Q7 in `../RVS_Plan.md`.

---

## Tenant provisioning

Onboarding a dealership requires, in order: create the Auth0 user with `app_metadata.tenantId` and `orgName`; assign a role; create the `Tenant` and `Dealership` documents; create at least one `Location` with a unique slug; write the `slug-lookups` entry; create the `TenantConfig` with the access gate enabled.

`TenantAccessGateMiddleware` reads that config to block disabled tenants with a 403. It allowlists `/health`, `/swagger`, and `/api/tenants/config`. Note that its backing repository is currently unimplemented — see Known gaps in `RVS_Architecture.md`.

Portal-side configuration steps are in `Auth0/Auth0-Portal-Configuration-Checklist.md`.
