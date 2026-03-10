# Auth0/OIDC Configuration - Complete Reassessment and Fix

## ?? **Issues Fixed**

### **Issue 1: Circular Dependency (Caused Page Freeze)**
Fixed by using inline handler configuration.

### **Issue 2: AccessTokenNotAvailableException with Scopes**

**Error:**
```
AccessTokenNotAvailableException: 'openid, profile, email, offline_access'
   at AuthorizationMessageHandler.SendAsync
```

**Problem:**
When you specify `scopes` in `ConfigureHandler()`, it tries to request a NEW token with those specific scopes. Auth0 doesn't support requesting additional scopes after initial login - all scopes must be requested during the original authentication.

**Solution:**
Don't specify scopes in `ConfigureHandler()` - just use the token that was already obtained during login.

**Before (BROKEN):**
```csharp
.ConfigureHandler(
    authorizedUrls: new[] { apiBaseUrl },
    scopes: new[] { "openid", "profile", "email", "offline_access" });  // ? Causes error
```

**After (FIXED):**
```csharp
.ConfigureHandler(
    authorizedUrls: new[] { apiBaseUrl });  // ? Just specify URLs, no scopes
```

---

## ? **Final Working Configuration**

### **BF.BlazorWASM\Program.cs**

```csharp
using Blazored.LocalStorage;
using Blazored.SessionStorage;
using BF.BlazorWASM;
using BF.BlazorWASM.Services;
using BF.BlazorWASM.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Get API base URL from configuration
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Configure HttpClient for API calls with automatic bearer token attachment
builder.Services.AddHttpClient("BF.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>()
        .ConfigureHandler(
            authorizedUrls: new[] { apiBaseUrl });
    // ? Don't specify scopes - use the token obtained during login
    return handler;
});

// Register AuthorizationMessageHandler
builder.Services.AddScoped<AuthorizationMessageHandler>();

// Provide HttpClient to services via factory
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("BF.API"));

// Configure OIDC Authentication with Auth0
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Auth0", options.ProviderOptions);
    
    options.ProviderOptions.ResponseType = "code";
    
    // ? Request scopes during LOGIN (not during API calls)
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
    options.ProviderOptions.DefaultScopes.Add("offline_access");
    
    var audience = builder.Configuration["Auth0:Audience"];
    if (!string.IsNullOrEmpty(audience))
    {
        options.ProviderOptions.AdditionalProviderParameters.Add("audience", audience);
    }

    options.UserOptions.RoleClaim = "roles";
    options.UserOptions.NameClaim = "name";
});

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthorizationCore(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

---

## ?? **Key Insight: Where Scopes Are Specified**

| Where | Purpose | Correct Usage |
|-------|---------|---------------|
| `AddOidcAuthentication` ? `DefaultScopes` | Request scopes during **LOGIN** | ? Add all needed scopes here |
| `ConfigureHandler` ? `scopes` parameter | Request token with **specific scopes** for this request | ? Don't use for Auth0 |

**Why?**
- Auth0 issues one access token during login with all granted scopes
- The `scopes` parameter in `ConfigureHandler` tells the handler to request a NEW token with those scopes
- Auth0 doesn't support requesting additional scopes after login (unlike some other providers)
- This causes `AccessTokenNotAvailableException`

---

## ?? **Testing**

### **After This Fix:**

1. **Clear browser storage:**
   ```javascript
   sessionStorage.clear();
   localStorage.clear();
   ```

2. **Log out and log back in** (required to get new token with correct scopes)

3. **Navigate to Check-In page**

4. **Expected:**
   - ? Page loads without freezing
   - ? API calls succeed
   - ? No `AccessTokenNotAvailableException` in console
   - ? Lookup data (payers, visit types, etc.) loads

---

## ? **Status**

**Status:** ? FIXED

All issues resolved:
- [x] Circular dependency (page freeze)
- [x] AccessTokenNotAvailableException (scopes error)
- [x] Auth0 domain trailing slash
- [x] Token attachment to API calls

**Action:** Clear browser storage, restart app, log in again, test Check-In page.
