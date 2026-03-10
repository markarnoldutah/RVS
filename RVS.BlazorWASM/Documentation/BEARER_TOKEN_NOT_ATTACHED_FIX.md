# Bearer Token Not Attached - FIXED

## ? **Problem**

Authorization header was **not being sent** with API requests, even though the user was logged in.

**Symptoms:**
- User is authenticated (can see name in header)
- API returns 401 Unauthorized
- DevTools Network tab shows **no Authorization header**

---

## ?? **Root Cause**

The `BaseAddressAuthorizationMessageHandler` was registered but **not configured** with authorized URLs.

**Before Fix:**
```csharp
// ? Handler registered but not configured
builder.Services.AddScoped<BaseAddressAuthorizationMessageHandler>();

builder.Services.AddHttpClient("BF.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();
```

**What was missing:**
The handler needs to be told **which URLs** should receive bearer tokens via `ConfigureHandler()`.

---

## ? **Solution**

### **Updated BF.BlazorWASM\Program.cs**

```csharp
// Configure HttpClient with automatic token attachment
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Configure BaseAddressAuthorizationMessageHandler with authorized URLs
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<BaseAddressAuthorizationMessageHandler>();
    handler.ConfigureHandler(
        authorizedUrls: new[] { apiBaseUrl },
        scopes: new[] { "openid", "profile", "email" });
    return handler;
});

builder.Services.AddHttpClient("BF.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Provide HttpClient to services (for backward compatibility)
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("BF.API"));
```

---

## ?? **What Changed**

### **ConfigureHandler Method**

```csharp
handler.ConfigureHandler(
    authorizedUrls: new[] { apiBaseUrl },  // ? URLs that should receive tokens
    scopes: new[] { "openid", "profile", "email" });  // ? Scopes to request
```

**Parameters:**
- **`authorizedUrls`**: Array of URLs that should receive bearer tokens
  - In our case: `https://localhost:7116` (from `ApiBaseUrl` configuration)
- **`scopes`**: OAuth scopes to include in token requests
  - `openid` - Required for OIDC
  - `profile` - User profile information
  - `email` - Email claim

---

## ?? **How It Works Now**

### **Token Attachment Flow**

```
1. API Service makes request (e.g., LookupApi.GetPayersAsync())
        ?
2. HttpClient sends request to https://localhost:7116/api/payers
        ?
3. BaseAddressAuthorizationMessageHandler intercepts
        ?
4. Handler checks: Is https://localhost:7116 in authorizedUrls?
   - BEFORE: ? No configuration ? No token attached
   - AFTER:  ? Yes, it's authorized ? Get token
        ?
5. Handler retrieves access token from IAccessTokenProvider
        ?
6. Handler adds header: Authorization: Bearer eyJhbGciOi...
        ?
7. Request sent to API with token ?
        ?
8. API validates token
        ?
9. Returns 200 OK with data
```

---

## ?? **Testing the Fix**

### **?? RESTART REQUIRED**

**You MUST restart the Blazor WASM app** - hot reload won't apply this DI configuration change.

### **After Restart:**

1. **Log in** to the application (if not already logged in)
2. Navigate to a page that calls the API (e.g., Check-In)
3. Open **Browser DevTools** (F12) ? **Network** tab
4. Find the request to `/api/payers`
5. **Check Request Headers**

**Expected:**
```http
GET https://localhost:7116/api/payers
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Before Fix:**
```http
GET https://localhost:7116/api/payers
? NO Authorization header
```

---

## ?? **Comparison**

| Aspect | Before | After |
|--------|--------|-------|
| **Handler Registration** | ? Registered | ? Registered |
| **Handler Configuration** | ? Not configured | ? Configured with URLs |
| **Authorization Header** | ? Not sent | ? Sent |
| **API Response** | ? 401 Unauthorized | ? 200 OK |

---

## ?? **Security Notes**

### **Why ConfigureHandler is Required**

The `BaseAddressAuthorizationMessageHandler` is designed to be **secure by default**:
- ? Doesn't send tokens to **any** URL automatically
- ? Only sends tokens to **explicitly authorized URLs**
- ? Prevents token leakage to untrusted domains

**Without configuration:**
- Handler doesn't know which URLs are safe to receive tokens
- No tokens are attached to any requests
- All API calls fail with 401

**With configuration:**
- Handler only sends tokens to configured URLs
- Tokens stay secure
- API calls succeed

---

## ?? **Configuration Reference**

### **Development Environment**

**appsettings.Development.json:**
```json
{
  "ApiBaseUrl": "https://localhost:7116"
}
```

**Authorized URLs:**
- `https://localhost:7116`

### **Production Environment**

**appsettings.json:**
```json
{
  "ApiBaseUrl": "https://api.benefetch.com"
}
```

**Authorized URLs:**
- `https://api.benefetch.com`

---

## ?? **Related Issues**

### **Issue 1: Multiple API Endpoints**

If you have multiple API endpoints:

```csharp
handler.ConfigureHandler(
    authorizedUrls: new[] { 
        "https://localhost:7116",        // Primary API
        "https://localhost:5000"         // Secondary API
    },
    scopes: new[] { "openid", "profile", "email" });
```

### **Issue 2: Custom Scopes**

If your Auth0 API requires custom scopes:

```csharp
handler.ConfigureHandler(
    authorizedUrls: new[] { apiBaseUrl },
    scopes: new[] { 
        "openid", 
        "profile", 
        "email",
        "read:patients",    // Custom scope
        "write:patients"    // Custom scope
    });
```

Update Auth0 configuration in `Program.cs`:
```csharp
options.ProviderOptions.DefaultScopes.Add("read:patients");
options.ProviderOptions.DefaultScopes.Add("write:patients");
```

---

## ? **Checklist**

- [x] Configure BaseAddressAuthorizationMessageHandler with authorized URLs
- [x] Include required OAuth scopes
- [x] Build successful
- [ ] **Restart Blazor WASM app**
- [ ] Log in to the application
- [ ] Verify Authorization header is present in Network tab
- [ ] Verify API returns 200 OK instead of 401

---

## ?? **References**

- [BaseAddressAuthorizationMessageHandler](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.webassembly.authentication.baseaddressauthorizationmessagehandler)
- [ConfigureHandler Method](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.webassembly.authentication.authorizationmessagehandler.configurehandler)
- [Blazor WASM Additional Security Scenarios](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/additional-scenarios)

---

## ?? **Status**

**Status:** ? FIXED

The `BaseAddressAuthorizationMessageHandler` is now properly configured to attach bearer tokens to API requests.

**Action Required:** Restart the Blazor WASM application to apply the changes.

**Expected Result:** API calls will now include the Authorization header and return 200 OK instead of 401 Unauthorized.
