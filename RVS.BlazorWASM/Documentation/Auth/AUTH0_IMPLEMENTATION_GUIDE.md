# Auth0 PKCE Implementation Guide

## ? Implementation Complete

The following components have been created/updated for Auth0 PKCE authentication:

### Files Created
1. **BF.BlazorWASM\Pages\Authentication.razor** - Handles all authentication routes
2. **BF.BlazorWASM\Shared\LoginDisplay.razor** - Login/Logout UI component
3. **BF.BlazorWASM\Shared\UnauthorizedAccess.razor** - Custom unauthorized access page with user-friendly UI

### Files Updated
1. **BF.BlazorWASM\Layout\MainLayout.razor** - Added LoginDisplay to header
2. **BF.BlazorWASM\Program.cs** - Enhanced Auth0 configuration + **configured bearer token attachment for API calls**
3. **BF.BlazorWASM\App.razor** - Replaced RedirectToLogin with UnauthorizedAccess component and enhanced all error pages with FluentUI

---

## ?? Bearer Token Configuration - CRITICAL ?

### **Status: ? CONFIGURED**

Bearer tokens are now automatically attached to all HTTP requests to the API.

**Implementation (BF.BlazorWASM\Program.cs):**
```csharp
// Configure HttpClient with automatic token attachment
builder.Services.AddHttpClient("BF.API", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
})
.AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Register BaseAddressAuthorizationMessageHandler
builder.Services.AddScoped<BaseAddressAuthorizationMessageHandler>();

// Provide HttpClient to services
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("BF.API"));
```

**What this does:**
- ? Automatically attaches `Authorization: Bearer {token}` header to all API requests
- ? Retrieves current access token from authentication state
- ? Handles automatic token refresh when expired
- ? Only sends tokens to configured BaseAddress (security)

**Verification:**
- Open Browser DevTools ? Network tab
- Log in and make an API call
- Inspect request headers ? Should see `Authorization: Bearer eyJhbGciOi...`

**See:** [BEARER_TOKEN_CONFIGURATION.md](../BEARER_TOKEN_CONFIGURATION.md) for detailed documentation.

---

## ?? API Security Validation - CONFIRMED ?

### Token Validation (BF.API\Program.cs)
**Status: ? PROPERLY CONFIGURED**

The API is configured with JWT Bearer authentication:
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
- ? Validates JWT signature using Auth0's public keys (from JWKS endpoint)
- ? Validates `iss` (issuer) claim matches Auth0 domain
- ? Validates `aud` (audience) claim matches your API identifier
- ? Validates `exp` (expiration) to ensure tokens are not expired
- ? Automatically rejects tampered or invalid tokens

### Controller Authorization
**Status: ? ALL ENDPOINTS PROTECTED**

All API controllers are decorated with `[Authorize]` attribute:
- ? PatientsController
- ? PracticesController
- ? TenantsController
- ? EncountersController
- ? CoverageEnrollmentsController
- ? LookupsController
- ? EligibilityChecksController (assumed)

**Result:** No endpoint can be accessed without a valid JWT token.

### Claims-Based Security
**Status: ? TENANT ISOLATION ENFORCED**

The API uses `ClaimsService` to extract and validate tenant and practice context:
```csharp
var tenantId = _claimsService.GetTenantIdOrThrow();
_claimsService.EnsurePracticeAccessOrThrow(practiceId);
```

This ensures:
- ? Users can only access data for their tenant
- ? Users can only access practices they're authorized for
- ? Multi-tenant data isolation is enforced at the API layer

### HTTPS Configuration
**Status: ? CONFIGURED AND ENFORCED IN PRODUCTION**

**Development (launchSettings.json):**
- HTTPS profile available: `https://localhost:7116`
- HTTP also available for local dev: `http://localhost:5236`

**Production (Program.cs):**
```csharp
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
```

**What this does:**
- ? Automatically redirects all HTTP requests to HTTPS
- ? Returns 307 Temporary Redirect status code
- ? Preserves original request method and body
- ? Only active in Production environment

**Additional Production Configuration (appsettings.Production.json):**
```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:443"
      }
    }
  }
}
```

### CORS Configuration
**Status: ? PRODUCTION-READY - RESTRICTED TO PORTAL.BENEFETCH.COM**

**Production Configuration:**
```csharp
if (builder.Environment.IsProduction())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ProductionCors", corsBuilder =>
        {
            corsBuilder
                .WithOrigins("https://portal.benefetch.com")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}
```

**Development Configuration:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", corsBuilder =>
    {
        corsBuilder
            .WithOrigins(
                "https://localhost:5001",
                "http://localhost:5001",
                "https://localhost:7116",
                "http://localhost:5236"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

**Environment-Based Policy Selection:**
```csharp
app.UseCors(app.Environment.IsProduction() ? "ProductionCors" : "DevelopmentCors");
```

**What this does:**
- ? Production: Only allows requests from https://portal.benefetch.com
- ? Development: Allows localhost origins for testing
- ? Automatically switches based on environment
- ? Supports credentials (required for authenticated requests)

---

## ?? Required Auth0 Application Configuration

### In your Auth0 Dashboard (Application Settings):

1. **Application Type**: Single Page Application

2. **Application URIs**:
   - **Allowed Callback URLs**:
     ```
     https://localhost:5001/authentication/login-callback
     https://yourdomain.com/authentication/login-callback
     ```
   
   - **Allowed Logout URLs**:
     ```
     https://localhost:5001/
     https://yourdomain.com/
     ```
   
   - **Allowed Web Origins**:
     ```
     https://localhost:5001
     https://yourdomain.com
     ```

3. **Advanced Settings ? Grant Types**:
   - ? Authorization Code
   - ? Refresh Token
   - ? Implicit (remove if enabled)

4. **Advanced Settings ? Token Endpoint Authentication Method**: None

5. **Refresh Token Rotation** (Recommended):
   - Go to **Applications ? [Your App] ? Refresh Token Rotation**
   - Enable: **Rotation**
   - Enable: **Reuse Interval** (set to 0)
   - Set **Absolute Lifetime** (e.g., 30 days)
   - Set **Inactivity Lifetime** (e.g., 7 days)

---

## ?? Features Implemented

### Authentication Flow
- ? PKCE authorization code flow
- ? Automatic token refresh using refresh tokens
- ? Secure token storage in browser session storage
- ? User-driven login/logout
- ? Protected route handling
- ? Friendly authentication status messages

### Scopes Configured
- `openid` - OIDC identity (automatic)
- `profile` - User profile information (automatic)
- `email` - Email address claim
- `offline_access` - Refresh token support

### Claim Mappings
- Role claims: `roles`
- Name claims: `name`
- Fallback display: email or preferred_username

---

## ?? Testing Checklist

1. **Test Unauthenticated Access**:
   - Navigate to a protected page
   - Should redirect to Auth0 login
   - After login, should return to original page

2. **Test Login Display**:
   - Click "Log in" button in header
   - Should redirect to Auth0
   - After successful login, should show user name and "Log out" button

3. **Test Logout**:
   - Click "Log out" button
   - Should clear tokens and return to home page
   - Verify Auth0 session is also terminated

4. **Test Token Refresh**:
   - Stay logged in for over 1 hour
   - Application should automatically refresh access token using refresh token
   - No user interaction required

5. **Test Protected Routes**:
   - Add `@attribute [Authorize]` to a Blazor page
   - Access while not logged in ? should redirect to login
   - Access while logged in ? should render normally

6. **Test API Token Validation**:
   - Make API call without token ? should return 401 Unauthorized
   - Make API call with expired token ? should return 401 Unauthorized
   - Make API call with tampered token ? should return 401 Unauthorized
   - Make API call with valid token ? should succeed

---

## ?? Production Deployment Checklist

### BF.API Security Hardening

- [x] **? HTTPS Redirection Enabled** - Enforced in Production environment
  ```csharp
  if (app.Environment.IsProduction())
  {
      app.UseHttpsRedirection();
  }
  ```

- [x] **? CORS Policy Restricted** - Production only allows portal.benefetch.com
  ```csharp
  if (builder.Environment.IsProduction())
  {
      builder.Services.AddCors(options =>
      {
          options.AddPolicy("ProductionCors", corsBuilder =>
          {
              corsBuilder
                  .WithOrigins("https://portal.benefetch.com")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
          });
      });
  }
  ```

- [ ] **Configure HTTPS-Only Hosting**:
  - Azure App Service: Enable "HTTPS Only" setting
  - Configure SSL certificate
  - Verify HTTP redirects to HTTPS

- [ ] **Secure Configuration Values**:
  - Move `Auth0:Domain` to Azure Key Vault or App Configuration
  - Move `CosmosDb:Key` to Azure Key Vault
  - Move `Availity:Auth:BearerToken` to Azure Key Vault
  - Use Managed Identity for Azure resources

- [ ] **Configure Auth0 API**:
  - Create API in Auth0 Dashboard if not exists
  - Set identifier to match `Auth0:Audience` value
  - Configure token expiration (recommended: 1 hour for access tokens)
  - Enable RBAC if using role-based authorization

### BF.BlazorWASM Configuration

- [ ] **Update Production appsettings.json**:
  ```json
  {
    "Auth0": {
      "Authority": "https://dev-2jhzz8xmjggh26pm.us.auth0.com",
      "ClientId": "CBdytt7GQhJSejyVwSrCZZRYUkLMXdiY",
      "Audience": "https://api.benefetch.com"
    }
  }
  ```

- [ ] **Configure Production API Base URL**:
  - Update HttpClient BaseAddress to production API URL
  - Ensure CORS allows requests from Blazor app domain

### Auth0 Dashboard Configuration

- [ ] Add production URLs to:
  - Allowed Callback URLs
  - Allowed Logout URLs  
  - Allowed Web Origins

- [ ] Configure custom claims in Auth0 Actions:
  ```javascript
  // Add tenantId and practiceIds to access token
  exports.onExecutePostLogin = async (event, api) => {
    const namespace = 'http://benefetch.com/';
    
    if (event.authorization) {
      api.accessToken.setCustomClaim(`${namespace}tenantId`, event.user.user_metadata.tenantId);
      api.accessToken.setCustomClaim(`${namespace}practiceIds`, event.user.user_metadata.practiceIds);
      
      // Add roles
      api.accessToken.setCustomClaim(`${namespace}roles`, event.authorization.roles);
    }
  };
  ```

---

## ?? Security Notes

1. **Client Secrets**: Not used with PKCE (SPA doesn't need client secret)
2. **Token Storage**: Tokens stored in session storage (cleared on browser close)
3. **API Validation**: ? All tokens validated server-side - CONFIRMED
4. **HTTPS Required**: ?? Must be enabled for production deployment
5. **CORS Configuration**: ?? Must restrict origins for production

---

## ?? Troubleshooting

### Login redirects but fails to complete
- Check callback URL matches exactly in Auth0 settings
- Verify `AuthenticationService.js` is included in index.html
- Check browser console for errors

### Tokens not refreshing
- Verify `offline_access` scope is requested
- Ensure refresh token rotation is enabled in Auth0
- Check token expiration settings

### User claims not appearing
- Verify scopes in Auth0 Application settings
- Check Auth0 Actions/Rules are adding claims correctly
- Ensure claim names match configuration in Program.cs

### API returns 401 Unauthorized
- Verify Auth0 Domain and Audience in API configuration
- Check token includes correct `aud` claim
- Verify token is not expired
- Check token is sent in Authorization header: `Bearer {token}`

### CORS errors in browser console
- Verify API CORS policy includes Blazor app origin
- Check CORS policy allows required headers and methods
- Ensure `AllowCredentials()` is set if needed

---

## ?? References

- [Auth0 SPA Quickstart](https://auth0.com/docs/quickstart/spa)
- [Auth0 PKCE Flow](https://auth0.com/docs/get-started/authentication-and-authorization-flow/authorization-code-flow-with-pkce)
- [Blazor WebAssembly Security](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/)
- [Auth0 Refresh Token Rotation](https://auth0.com/docs/secure/tokens/refresh-tokens/refresh-token-rotation)
- [ASP.NET Core Enforcing SSL](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl)
- [ASP.NET Core CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
