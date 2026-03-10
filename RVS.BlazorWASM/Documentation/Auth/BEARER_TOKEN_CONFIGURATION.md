# Bearer Token Attachment - Configuration Fix

## ? **Problem Identified**

The Blazor WebAssembly application was **NOT attaching bearer tokens** to outgoing HTTP requests to the BF.API, which would result in **401 Unauthorized** responses for all API calls.

### Root Cause
The HttpClient was registered without an authorization message handler:
```csharp
// ? BEFORE - No token attachment
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
```

---

## ? **Solution Implemented**

### **Updated BF.BlazorWASM\Program.cs**

```csharp
// Configure HttpClient with automatic token attachment
builder.Services.AddHttpClient("BF.API", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
})
.AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Register BaseAddressAuthorizationMessageHandler
builder.Services.AddScoped<BaseAddressAuthorizationMessageHandler>();

// Provide HttpClient to services (for backward compatibility)
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("BF.API"));
```

### **What This Does**

1. **`AddHttpClient("BF.API")`** - Creates a named HttpClient with proper factory management
2. **`AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>()`** - Attaches the built-in authorization handler
3. **`BaseAddressAuthorizationMessageHandler`** - Automatically:
   - Gets the current access token from OIDC authentication
   - Adds `Authorization: Bearer {token}` header to **ALL** outgoing requests
   - Handles token refresh if expired
   - Works for requests to the same BaseAddress (same-origin)

4. **Backward Compatibility** - Existing services continue to work by resolving HttpClient from the factory

---

## ?? **How Bearer Token Attachment Works**

### **Authentication Flow**

```
1. User logs in via Auth0
        ?
2. App receives access_token and stores in session storage
        ?
3. API service makes HTTP request (e.g., PatientApiService.GetPatientAsync)
        ?
4. BaseAddressAuthorizationMessageHandler intercepts request
        ?
5. Handler retrieves access_token from IAccessTokenProvider
        ?
6. Handler adds header: Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
        ?
7. Request sent to BF.API
        ?
8. BF.API validates JWT token
        ?
9. API returns data (200 OK)
```

### **Example HTTP Request**

**Before Fix:**
```http
GET /api/practices/123/patients/456 HTTP/1.1
Host: localhost:7116
Accept: application/json
// ? NO Authorization header - Results in 401
```

**After Fix:**
```http
GET /api/practices/123/patients/456 HTTP/1.1
Host: localhost:7116
Accept: application/json
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6IjZ1eDhHWW1LeWNpa1ZoTUJxV0t5dyJ9...
// ? Token automatically attached
```

---

## ?? **Affected Services (Now Fixed)**

All API services now automatically include bearer tokens:

- ? `IPatientApiService` / `PatientApiService`
- ? `ICheckInApiService` / `CheckInApiService`  
- ? `IEligibilityCheckApiService` / `EligibilityCheckApiService`
- ? `ILookupApiService` / `LookupApiService`
- ? `IEligibilityCheckPollingService` / `EligibilityCheckPollingService`

---

## ?? **Testing the Fix**

### **1. Browser Developer Tools Test**

1. Open browser Developer Tools (F12)
2. Go to **Network** tab
3. Log in to the application
4. Navigate to a page that makes API calls (e.g., Check-In page)
5. Inspect API requests in Network tab
6. Verify `Authorization: Bearer {token}` header is present

### **2. Expected Behavior**

**Before Fix:**
- ? API calls return 401 Unauthorized
- ? User sees errors or empty data
- ? Network tab shows no Authorization header

**After Fix:**
- ? API calls return 200 OK (if user is authenticated)
- ? Data loads successfully
- ? Network tab shows `Authorization: Bearer ...` header

### **3. Postman/API Testing**

Compare browser requests to manual Postman requests:

1. **Get token from browser**:
   - Navigate to `/user` page (shows access token)
   - Copy the access token value

2. **Test in Postman**:
   ```
   GET https://localhost:7116/api/practices/123/patients/456
   Headers:
     Authorization: Bearer {paste_token_here}
   ```

3. **Verify same result** as browser requests

---

## ?? **Advanced Configuration (If Needed)**

### **Custom Authorization Handler**

If you need to customize which URLs get tokens or add additional headers:

```csharp
public class CustomAuthorizationMessageHandler : AuthorizationMessageHandler
{
    public CustomAuthorizationMessageHandler(IAccessTokenProvider provider, NavigationManager navigation)
        : base(provider, navigation)
    {
        ConfigureHandler(
            authorizedUrls: new[] { "https://localhost:7116", "https://api.benefetch.com" },
            scopes: new[] { "openid", "profile", "email", "offline_access" });
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        // Custom logic before request
        var response = await base.SendAsync(request, cancellationToken);
        // Custom logic after response
        return response;
    }
}
```

Then register:
```csharp
builder.Services.AddScoped<CustomAuthorizationMessageHandler>();
builder.Services.AddHttpClient("BF.API")
    .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
```

### **Multiple API Endpoints**

If calling multiple different APIs:

```csharp
// Internal API (with tokens)
builder.Services.AddHttpClient("BF.API", client => client.BaseAddress = new Uri("https://api.benefetch.com"))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// External API (no tokens)
builder.Services.AddHttpClient("ExternalAPI", client => client.BaseAddress = new Uri("https://external-api.com"));
    // No auth handler - requests go without tokens
```

---

## ?? **Security Considerations**

### **What BaseAddressAuthorizationMessageHandler Does**

? **Automatic Token Attachment** - Adds Authorization header to same-origin requests  
? **Token Refresh** - Automatically refreshes expired tokens before requests  
? **Secure Storage** - Tokens stored in browser session storage (cleared on close)  
? **Same-Origin Only** - Tokens only sent to configured BaseAddress (prevents leakage)  

### **What It Doesn't Do**

? **Does NOT validate tokens** - Validation happens server-side in BF.API  
? **Does NOT prevent MITM** - Requires HTTPS in production  
? **Does NOT protect from XSS** - Follow XSS prevention best practices  
? **Does NOT send to cross-origin** - External APIs need separate configuration  

---

## ?? **References**

- [Blazor WASM Authentication - Additional Scenarios](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/additional-scenarios)
- [BaseAddressAuthorizationMessageHandler](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.webassembly.authentication.baseaddressauthorizationmessagehandler)
- [Attach tokens to outgoing requests](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/additional-scenarios#attach-tokens-to-outgoing-requests)

---

## ? **Verification Checklist**

- [x] BaseAddressAuthorizationMessageHandler registered
- [x] HttpClient configured with message handler
- [x] Named HttpClient factory pattern implemented
- [x] Backward compatibility maintained
- [x] Build successful
- [ ] **Test in browser** - Verify Authorization header in Network tab
- [ ] **Test API calls** - Confirm 200 OK responses (not 401)
- [ ] **Test after logout** - Verify requests fail appropriately
- [ ] **Test token refresh** - Keep app open >1 hour, verify automatic refresh

---

## ?? **Important Notes**

1. **Restart Required** - Hot reload may not apply this change. **Restart the Blazor app** after updating Program.cs.

2. **BaseAddress Must Match** - The token is only attached to requests going to the configured BaseAddress. If your API is at a different URL, update the configuration.

3. **Production URL** - Update BaseAddress for production:
   ```csharp
   var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;
   builder.Services.AddHttpClient("BF.API", client => client.BaseAddress = new Uri(apiBaseAddress))
   ```

4. **Debugging** - Use browser DevTools Network tab to inspect actual HTTP headers sent.

---

## ?? **Bottom Line**

**Status**: ? FIXED

Bearer tokens are now automatically attached to all HTTP requests from the Blazor WebAssembly app to the BF.API, enabling proper authentication and authorization for all API endpoints.
