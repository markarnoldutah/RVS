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

**Persistent session (Spec C-7, issue #498).** The framework keeps the OIDC user record — including the rotating refresh token — in `sessionStorage` and offers no option to change the store, so a closed tab or installed PWA used to mean a fresh login. `wwwroot/js/session-persist.js` loads before `AuthenticationService.js`, restores that one record from a `localStorage` mirror on startup, and mirrors every later write or removal (sign-in, refresh, sign-out).

Persistence is **opt-in per device**. After sign-in, `KeepSignedInPrompt` asks once, "Keep me signed in on this device?", and warns against shared computers; the answer is stored in `localStorage` (`rvs.keepSignedIn`). Until the user says yes, nothing is mirrored and the session dies with the tab, as before; saying no removes any mirror left over from earlier. Refresh tokens are rotating, **30 days absolute and 7 days idle** (`auth0-bootstrap-prod.sh`; the staging tenant is set by hand in the Auth0 dashboard). On sign-out, `LoginDisplay` revokes the refresh token at Auth0's `/oauth/revoke` (`RefreshTokenRevocationClient`) and clears the mirror before the framework's own sign-out, so a token copied earlier stops working.

**Auth0's own session follows the same answer.** Auth0 keeps a separate session cookie on its domain, and the Blazor authentication library silently signs in against it on page load, so the app's opt-in alone would not stop Auth0 signing the next person into a shared computer. `KeepSignedInPolicy` (`RVS.UI.Shared`) therefore adds `prompt=login` to every authorize request unless the device opted in. An interactive sign-in then always asks for the password, and the silent sign-in fails fast: Auth0 rejects a request carrying both `prompt=none` and `prompt=login` with `invalid_request` (verified against the tenant). Token renewal still works through the refresh token. Sign-out goes through Auth0's `/oidc/logout` (advertised as `end_session_endpoint`), which ends the Auth0 session — but only if `{origin}/authentication/logout-callback` is an Allowed Logout URL, because Auth0 matches that list exactly and returns 400 otherwise.

The remaining trade-off is that an opted-in refresh token can be read by script on the manager origin. The Content-Security-Policy in `wwwroot/staticwebapp.config.json` limits which scripts can run there and where they can send data: no inline script, and scripts only from the app itself and the Application Insights CDN. Rotation with reuse detection further bounds a stolen token. Packet-email deep links (`/sr/{id}`, `/sr/{id}?action=…`) rely on this session — the status write happens only on an explicit confirm tap through the authenticated update endpoint, so there is no anonymous write surface and a scanner fetching the link changes nothing.

---

## Anonymous token surfaces

Two endpoints serve unauthenticated users, both rate-limited **per caller IP** (fixed window, partitioned on the `X-Forwarded-For` client address with the socket address as fallback — see `RVS.API/RateLimiting/ClientIpResolver.cs`): the intake endpoints (`IntakeEndpoint`, 20/min) and the customer status feed (`StatusEndpoint`, 10/min). Over-limit callers get `429`.

Spec X-5 requires that every anonymous token be ≥128 bits of entropy, **stored hashed**, TTL-bounded, rate-limited per IP, access-audited, and read-only. C-7 status writes go through the authenticated manager app, not an anonymous token (#498).

**The current implementation only partly meets this** — the per-IP rate limiting is in place (issue #442), but tokens are still stored unhashed on `GlobalCustomerAcct.magicLinkToken` with a 90-day expiry. The model is now decided (issue #427, closes Q7): SHA-256-hashed storage with the raw token never persisted; the **status token stays per-customer** (TTL ≤ 30 days, sliding renewal on use) and is read-only. **C-7 status changes are not on this token model** — issue #498 replaced the anonymous action-link design with authenticated deep links into the manager app (see "Manager app authentication" above), so there is no anonymous write surface. The prior ARCHIVE decision (`Docs/ARCHIVE/ASOT/RVS_MagicLink_Storage_Guidance.md`) that argued unhashed storage was adequate stays overturned by #427's decision on hashing. Implementation and migration are tracked in #440 / #441. See `RVS_Architecture.md` and Q7 in `../RVS_Plan.md`.

---

## Tenant provisioning

Onboarding a dealership requires, in order: create the Auth0 user with `app_metadata.tenantId` and `orgName`; assign a role; create the `Tenant` and `Dealership` documents; create at least one `Location` with a unique slug; write the `slug-lookups` entry; create the `TenantConfig` with the access gate enabled.

`TenantAccessGateMiddleware` reads that config to block disabled tenants with a 403. It allowlists `/health`, `/swagger`, and `/api/tenants/config`. Note that its backing repository is currently unimplemented — see Known gaps in `RVS_Architecture.md`.

Portal-side configuration steps are in `Auth0/Auth0-Portal-Configuration-Checklist.md`.
