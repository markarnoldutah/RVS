# Auth0 OAuth/OIDC Authentication System

> **Scope:** Documents the end-to-end Auth0 integration across `MF.BlazorWASM` (Blazor WebAssembly SPA) and `MF.API` (ASP.NET Core Web API).

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Auth0 Tenant Configuration](#2-auth0-tenant-configuration)
3. [Blazor WASM — Client-Side Authentication](#3-blazor-wasm--client-side-authentication)
4. [API — Server-Side Authentication](#4-api--server-side-authentication)
5. [Custom Claims](#5-custom-claims)
6. [Token Flow — End to End](#6-token-flow--end-to-end)
7. [Authorization Enforcement](#7-authorization-enforcement)
8. [Swagger / OpenAPI Authentication](#8-swagger--openapi-authentication)
9. [Configuration Reference](#9-configuration-reference)
10. [Key Files Reference](#10-key-files-reference)

---

## 1. Architecture Overview

```
┌─────────────────────┐      OIDC/PKCE       ┌─────────────────┐
│  MF.BlazorWASM      │◄────────────────────►│  Auth0 Tenant   │
│  (Blazor WASM SPA)  │   Authorization Code │  (IdP)          │
│                     │   + audience param   │                 │
└────────┬────────────┘                      └────────┬────────┘
         │                                            │
         │  Bearer token                              │  JWT signing keys
         │  (Authorization header)                    │  (JWKS / .well-known)
         ▼                                            ▼
┌─────────────────────┐      Validates JWT   ┌─────────────────┐
│  MF.API             │◄─────────────────────│  Auth0 JWKS     │
│  (ASP.NET Core API) │   Authority + Audience  endpoint       │
└─────────────────────┘                      └─────────────────┘
```

| Component | Role |
|---|---|
| **Auth0** | Identity Provider — issues ID tokens (OIDC) and access tokens (OAuth 2.0) |
| **MF.BlazorWASM** | Public SPA client — uses Authorization Code + PKCE flow |
| **MF.API** | Resource server — validates JWT bearer tokens issued by Auth0 |

**Protocol:** OAuth 2.0 Authorization Code flow with PKCE (Proof Key for Code Exchange) — the recommended flow for browser-based SPAs.

---

## 2. Auth0 Tenant Configuration

### Required Auth0 Setup

| Auth0 Entity | Purpose |
|---|---|
| **Tenant** | `dev-2jhzz8xmjggh26pm.us.auth0.com` |
| **SPA Application** | Registered as a Single Page Application. Client ID used by Blazor WASM. No client secret (public client). |
| **API** | Registered with identifier (audience) `https://api.managersfriend.com`. API validates tokens against this audience. |
| **Actions / Rules** | Must inject custom claims into the access token: `tenantId` (see [Custom Claims](#5-custom-claims)). |

### Custom Claims (Auth0 Action)

Auth0 must be configured with a post-login Action or Rule that adds the following namespaced claims to the **access token**:

| Claim | Type | Example Value |
|---|---|---|
| `http://managersfriend.com/tenantId` | `string` | `"tenant-abc-123"` |

These use a namespaced URI prefix (`http://managersfriend.com/`) to comply with Auth0's requirement that custom claims not collide with OIDC reserved claim names.

---

## 3. Blazor WASM — Client-Side Authentication

### Packages

- `Microsoft.AspNetCore.Components.WebAssembly.Authentication` — provides OIDC integration, `AuthorizationMessageHandler`, `RemoteAuthenticatorView`, and `AuthorizeRouteView`.

### OIDC Configuration (`Program.cs`)

```
wwwroot/appsettings.json
{
  "Auth0": {
    "Authority": "https://dev-2jhzz8xmjggh26pm.us.auth0.com/",
    "ClientId": "<Auth0 SPA Client ID>",
    "Audience": "https://api.managersfriend.com"
  },
  "ApiBaseUrl": "https://localhost:7116"
}
```

Registration in `Program.cs`:

```csharp
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Auth0", options.ProviderOptions);

    // PKCE authorization code flow
    options.ProviderOptions.ResponseType = "code";

    // Standard OIDC scopes
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
    options.ProviderOptions.DefaultScopes.Add("offline_access");

    // Auth0 API audience (required to get an access token for the API)
    options.ProviderOptions.AdditionalProviderParameters.Add("audience", "<audience>");

    // Auth0 claim mapping
    options.UserOptions.RoleClaim = "roles";
    options.UserOptions.NameClaim = "name";
});
```

**Key points:**
- `ResponseType = "code"` — uses authorization code flow (not implicit).
- `audience` parameter — tells Auth0 to issue an access token scoped to the API, not just an opaque ID token.
- `offline_access` scope — requests a refresh token for silent renewal.

### Global Authorization Policy

```csharp
builder.Services.AddAuthorizationCore(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();
```

Every page and component requires authentication by default. No `[Authorize]` attribute needed on individual pages.

### Automatic Bearer Token Attachment

```csharp
builder.Services.AddHttpClient("MF.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>()
        .ConfigureHandler(authorizedUrls: new[] { apiBaseUrl });
    return handler;
});
```

`AuthorizationMessageHandler` automatically:
1. Retrieves the access token from the OIDC token store.
2. Attaches it as `Authorization: Bearer <token>` on every request to `authorizedUrls`.
3. Triggers a redirect to login if the token cannot be obtained (expired, not authenticated).

### Razor Components

| Component | File | Purpose |
|---|---|---|
| `App.razor` | Root | Wraps routing in `AuthorizeRouteView`. Shows `UnauthorizedAccess` for unauthenticated users, "Access Denied" for authenticated but unauthorized. |
| `Authentication.razor` | `/authentication/{action}` | Handles OIDC callback actions (`login`, `logout`, etc.) via `RemoteAuthenticatorView`. |
| `LoginDisplay.razor` | Shared | Shows profile menu (authenticated) or "Log in" button (anonymous). Uses `NavigationManager.NavigateToLogin/Logout`. |
| `UnauthorizedAccess.razor` | Shared | Full-page prompt to sign in when accessing a protected page without authentication. |

### User Session Service

`UserSessionService` subscribes to `AuthenticationStateProvider.AuthenticationStateChanged` and:

1. **On login:** Extracts custom claims (`tenantId`) from the **access token** (not the ID token) by parsing the JWT payload client-side.
2. **Initializes session state:** Sets `TenantId`.
3. **On logout:** Clears session state and lookup cache.

**Why parse the access token?** Auth0 puts custom claims (`tenantId`) on the access token via Actions. The ID token only contains standard OIDC claims. The `AuthenticationStateProvider` in Blazor WASM exposes the ID token claims, so custom claims must be extracted separately by decoding the access token JWT.

---

## 4. API — Server-Side Authentication

### Packages

- `Microsoft.AspNetCore.Authentication.JwtBearer` — JWT bearer token validation.

### JWT Bearer Configuration (`Program.cs`)

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Auth0:Domain"];
    options.Audience = builder.Configuration["Auth0:Audience"];
});
```

**What this does:**
- `Authority` — Points to the Auth0 tenant. The middleware automatically fetches `.well-known/openid-configuration` and JWKS signing keys.
- `Audience` — Validates the `aud` claim in the JWT matches the registered API identifier.
- Token signature, expiry, and issuer are validated automatically by the middleware.

### Middleware Pipeline Order

```csharp
app.UseCors(...);
app.UseMiddleware<ExceptionHandlingMiddleware>();  // Catches auth exceptions too
app.UseAuthentication();                           // Validates JWT, populates HttpContext.User
app.UseAuthorization();                            // Enforces [Authorize] attributes
// app.UseMiddleware<TenantAccessGateMiddleware>(); // Checks tenant enabled status
app.MapControllers();
```

### Claims Extraction

After the JWT bearer middleware validates the token, all claims from the access token are available on `HttpContext.User`:

| Claim Type | Source | Usage |
|---|---|---|
| `sub` (mapped to `ClaimTypes.NameIdentifier`) | Standard JWT | User ID for audit fields |
| `http://managersfriend.com/tenantId` | Auth0 Action | Tenant isolation on every request |

---

## 5. Custom Claims

### Claim Type Constants

Both projects use the same namespaced claim URI. Constants are defined in two places:

**API — `ClaimsService`:**
```csharp
public const string TenantIdClaimType = "http://managersfriend.com/tenantId";
```

**Blazor WASM — `UserSessionService`:**
```csharp
private const string TenantIdClaimType = "http://managersfriend.com/tenantId";
```

### How Claims Are Consumed

| Layer | Class | What It Does |
|---|---|---|
| **Controller** | `ClaimsService` | `GetTenantIdOrThrow()` — extracts tenant ID or throws `UnauthorizedAccessException`. |
| **Service** | `HttpUserContextAccessor` (implements `IUserContextAccessor`) | Exposes `UserId`, `TenantId` to service layer without ASP.NET dependency. Used for audit stamping (`CreatedByUserId`, `UpdatedByUserId`). |
| **Middleware** | `TenantAccessGateMiddleware` | Extracts `tenantId` from claims, queries tenant config to verify logins are enabled. |
| **Middleware** | `ExceptionHandlingMiddleware` | Extracts `tenantId` and `userId` for structured error logging. |
| **Blazor** | `UserSessionService` | Parses the access token JWT to extract `tenantId` for client-side session state. |

---

## 6. Token Flow — End to End

```
1. User clicks "Sign In" in Blazor WASM
   └─► NavigationManager.NavigateToLogin("authentication/login")

2. OIDC library redirects browser to Auth0 /authorize endpoint
   └─► Includes: client_id, redirect_uri, response_type=code, code_challenge (PKCE),
       scope=openid profile email offline_access, audience=https://api.managersfriend.com

3. User authenticates at Auth0 (Universal Login)
   └─► Auth0 Action runs: adds tenantId claim to access token

4. Auth0 redirects back to /authentication/login-callback with authorization code
   └─► RemoteAuthenticatorView handles the callback

5. OIDC library exchanges code for tokens (via PKCE, no client secret)
   └─► Receives: ID token (OIDC claims), access token (API claims), refresh token

6. Blazor stores tokens in browser session
   └─► AuthenticationStateChanged fires
   └─► UserSessionService extracts tenantId from access token

7. HttpClient makes API call
   └─► AuthorizationMessageHandler attaches: Authorization: Bearer <access_token>

8. API receives request
   └─► JwtBearer middleware validates signature, expiry, audience, issuer
   └─► HttpContext.User populated with claims from access token
   └─► TenantAccessGateMiddleware checks tenant is enabled
   └─► Controller calls ClaimsService.GetTenantIdOrThrow()
   └─► Service uses IUserContextAccessor.UserId for audit fields
```

---

## 7. Authorization Enforcement

### Blazor WASM (Client-Side)

| Mechanism | Scope | How |
|---|---|---|
| **Fallback policy** | All pages | `RequireAuthenticatedUser()` as fallback — every page requires authentication unless `[AllowAnonymous]`. |
| **`AuthorizeRouteView`** | Router | In `App.razor` — renders `UnauthorizedAccess` component if not authenticated, "Access Denied" if authenticated but unauthorized. |
| **`AuthorizeView`** | Per-component | Used in `LoginDisplay.razor` to toggle between authenticated/anonymous UI. |

### API (Server-Side)

| Mechanism | Scope | How |
|---|---|---|
| **`[Authorize]`** | Controller class | Every controller applies `[Authorize]` at the class level. |
| **`ClaimsService.GetTenantIdOrThrow()`** | Every action | Throws `UnauthorizedAccessException` → 401 if tenant claim is missing. |
| **`TenantAccessGateMiddleware`** | All authenticated requests | Returns 403 if tenant is disabled (with structured JSON error body). |
| **`ExceptionHandlingMiddleware`** | Global | Converts `UnauthorizedAccessException` → 401 response. |

### Authorization Error Responses

| Scenario | HTTP Status | Source |
|---|---|---|
| No token / invalid token | `401 Unauthorized` | JWT bearer middleware |
| Valid token, missing tenant claim | `401 Unauthorized` | `ClaimsService` → `ExceptionHandlingMiddleware` |
| Valid token, tenant disabled | `403 Forbidden` | `TenantAccessGateMiddleware` |
| Valid token, tenant ID missing from claims | `403 Forbidden` | `TenantAccessGateMiddleware` |

---

## 8. Swagger / OpenAPI Authentication

The API configures Swagger UI with Auth0 OAuth for interactive testing:

```csharp
options.OAuthClientId(builder.Configuration["Auth0:ClientId"]);
options.OAuthClientSecret(builder.Configuration["Auth0:ClientSecret"]);
options.OAuthAdditionalQueryStringParams(new Dictionary<string, string>
{
    { "audience", builder.Configuration["Auth0:Audience"] ?? "" }
});
options.OAuthScopes("openid", "profile");
options.OAuthUsePkce();
options.EnablePersistAuthorization();
```

A `BearerSecuritySchemeTransformer` (`IOpenApiDocumentTransformer`) adds the OAuth2 security scheme to the OpenAPI document so Swagger UI shows the "Authorize" button with Auth0's `/authorize` and `/oauth/token` endpoints.

---

## 9. Configuration Reference

### Blazor WASM — `wwwroot/appsettings.json`

```json
{
  "Auth0": {
    "Authority": "https://<auth0-tenant>.us.auth0.com/",
    "ClientId": "<SPA Client ID>",
    "Audience": "https://api.managersfriend.com"
  },
  "ApiBaseUrl": "https://localhost:7116"
}
```

| Key | Description |
|---|---|
| `Auth0:Authority` | Auth0 tenant URL (OIDC discovery endpoint root) |
| `Auth0:ClientId` | Auth0 SPA application client ID |
| `Auth0:Audience` | API identifier — passed as `audience` param to get an API-scoped access token |
| `ApiBaseUrl` | Base URL of the API — `AuthorizationMessageHandler` attaches tokens to this origin |

### API — `appsettings.json` / User Secrets

| Key | Description |
|---|---|
| `Auth0:Domain` | Auth0 tenant URL (used as JWT `Authority`) |
| `Auth0:Audience` | API identifier (validated in JWT `aud` claim) |
| `Auth0:ClientId` | Used for Swagger UI OAuth config only |
| `Auth0:ClientSecret` | Used for Swagger UI OAuth config only (not used at runtime for token validation) |

---

## 10. Key Files Reference

### Blazor WASM

| File | Purpose |
|---|---|
| `Program.cs` | OIDC registration, `AuthorizationMessageHandler`, `HttpClient` config, fallback auth policy |
| `wwwroot/appsettings.json` | Auth0 `Authority`, `ClientId`, `Audience` |
| `App.razor` | `AuthorizeRouteView` — root authorization gate |
| `Pages/Authentication.razor` | `RemoteAuthenticatorView` — handles OIDC callbacks (login, logout, errors) |
| `Shared/LoginDisplay.razor` | Profile menu / login button using `AuthorizeView` |
| `Shared/UnauthorizedAccess.razor` | Full-page sign-in prompt for unauthenticated access |
| `Services/UserSessionService.cs` | Access token JWT parsing, session initialization, `tenantId` extraction |

### API

| File | Purpose |
|---|---|
| `Program.cs` | JWT bearer authentication registration, Swagger OAuth config, middleware pipeline |
| `Services/ClaimsService.cs` | Claim type constants, `GetTenantIdOrThrow()` |
| `Services/HttpUserContextAccessor.cs` | `IUserContextAccessor` impl — bridges claims to domain layer |
| `Middleware/ExceptionHandlingMiddleware.cs` | Converts `UnauthorizedAccessException` → 401, logs with tenant/user context |
| `Middleware/TenantAccessGateMiddleware.cs` | Post-auth tenant enabled check → 403 if disabled |

### Domain (Shared)

| File | Purpose |
|---|---|
| `Interfaces/IUserContextAccessor.cs` | Abstraction for `UserId`, `TenantId` — no ASP.NET dependency |
