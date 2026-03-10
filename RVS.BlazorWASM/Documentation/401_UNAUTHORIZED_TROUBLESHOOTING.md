# 401 Unauthorized Error - Diagnostic Guide

## ?? **Error Analysis**

**Error:** 401 Unauthorized when calling `https://localhost:7166/api/payers`

**Note:** The URL shows port **7116** but the error mentions **7166**. This might be a typo in the error message or a port mismatch issue.

---

## ?? **Diagnostic Steps**

### **Step 1: Verify You're Logged In**

1. Open the Blazor WASM app in your browser
2. Check if you see the **LoginDisplay** component in the header
3. **Expected:**
   - ? If logged in: Should show your name and "Log out" button
   - ? If not logged in: Should show "Log in" button

**If not logged in:**
- Click "Log in" and complete Auth0 authentication
- Then retry the API call

---

### **Step 2: Check Bearer Token in Browser**

1. Open **Browser DevTools** (F12)
2. Go to **Network** tab
3. Find the failed request to `/api/payers`
4. Click on the request
5. Go to **Headers** tab
6. Look for **Request Headers** section

**Expected:**
```
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Scenarios:**

| Observation | Problem | Solution |
|------------|---------|----------|
| ? No `Authorization` header | Token not being attached | See Step 3 |
| ? Has `Authorization: Bearer ...` | Token present but invalid | See Step 4 |
| ?? Shows `Authorization: Bearer null` | Token retrieval failed | See Step 5 |

---

### **Step 3: Verify BaseAddressAuthorizationMessageHandler**

Check if the bearer token handler is configured correctly.

**Check: BF.BlazorWASM\Program.cs**

Should have:
```csharp
// Configure HttpClient with automatic token attachment
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddHttpClient("BF.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();  // ? This line is critical

// Register BaseAddressAuthorizationMessageHandler
builder.Services.AddScoped<BaseAddressAuthorizationMessageHandler>();  // ? This line is critical
```

**If missing:** Tokens won't be attached to requests.

---

### **Step 4: Verify API JWT Configuration**

Check if the API is configured to validate Auth0 tokens.

**Check: BF.API\Program.cs**

Should have:
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

**Common Issues:**
- ? Wrong `Auth0:Domain` in configuration
- ? Wrong `Auth0:Audience` in configuration
- ? Token `aud` claim doesn't match API `Audience`

---

### **Step 5: Check Token Expiration**

Tokens expire after 1 hour. If you've been logged in for a while:

1. Log out
2. Clear browser cache
3. Log in again
4. Retry the API call

---

### **Step 6: Verify Port Configuration**

**You mentioned port 7166, but the configuration shows 7116.**

Check these files:

**BF.BlazorWASM\wwwroot\appsettings.Development.json:**
```json
{
  "ApiBaseUrl": "https://localhost:7116"  // ? Should match API port
}
```

**BF.API\Properties\launchSettings.json:**
```json
{
  "profiles": {
    "https": {
      "applicationUrl": "https://localhost:7116"  // ? API port
    }
  }
}
```

**If mismatched:** Update to use the correct port.

---

## ?? **Quick Fixes**

### **Fix 1: Clear Authentication State**

```javascript
// In Browser Console
localStorage.clear();
sessionStorage.clear();
location.reload();
```

Then log in again.

---

### **Fix 2: Verify Auth0 Configuration Match**

**WASM App (appsettings.json):**
```json
{
  "Auth0": {
    "Authority": "https://dev-2jhzz8xmjggh26pm.us.auth0.com",
    "ClientId": "CBdytt7GQhJSejyVwSrCZZRYUkLMXdiY",
    "Audience": "https://api.benefetch.com"  // ? Must match API
  }
}
```

**API (appsettings.json):**
```json
{
  "Auth0": {
    "Domain": "https://dev-2jhzz8xmjggh26pm.us.auth0.com",  // ? Must match
    "Audience": "https://api.benefetch.com"  // ? Must match WASM
  }
}
```

---

### **Fix 3: Check Token Claims**

Navigate to `/user` page in your Blazor app to see token details.

**Look for:**
```
aud: https://api.benefetch.com  ? Should match API Audience
iss: https://dev-2jhzz8xmjggh26pm.us.auth0.com/  ? Should match Auth0 Domain
exp: [timestamp]  ? Should be in the future
```

---

## ?? **Test Sequence**

### **Test 1: Are You Authenticated?**

```csharp
// Add to CheckInPage.razor for debugging
@inject AuthenticationStateProvider AuthStateProvider

@code {
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        
        Logger.LogInformation("User authenticated: {IsAuthenticated}", user.Identity?.IsAuthenticated);
        Logger.LogInformation("User name: {Name}", user.Identity?.Name);
        
        await LoadLookupsAsync();
    }
}
```

Check browser console for log output.

---

### **Test 2: Can You Get a Token?**

```csharp
// Add to CheckInPage.razor
@inject IAccessTokenProvider TokenProvider

private async Task TestTokenAsync()
{
    var tokenResult = await TokenProvider.RequestAccessToken();
    
    if (tokenResult.TryGetToken(out var token))
    {
        Logger.LogInformation("Token acquired successfully");
        Logger.LogInformation("Token expires: {Expires}", token.Expires);
    }
    else
    {
        Logger.LogError("Failed to acquire token");
        Logger.LogError("Status: {Status}", tokenResult.Status);
    }
}
```

---

### **Test 3: Manual API Test**

1. Navigate to `/user` page in Blazor app
2. Copy the access token shown on the page
3. Test in **Postman** or **curl**:

```bash
curl -H "Authorization: Bearer YOUR_TOKEN_HERE" \
     https://localhost:7116/api/payers
```

**Expected:**
- ? 200 OK ? Token is valid, issue is with WASM token attachment
- ? 401 Unauthorized ? Token is invalid or API configuration is wrong

---

## ?? **Most Likely Causes**

### **Cause 1: Not Logged In** (70% probability)
- **Symptom:** No Authorization header in request
- **Fix:** Log in via Auth0

### **Cause 2: Token Not Being Attached** (20% probability)
- **Symptom:** No Authorization header in request, even when logged in
- **Fix:** Verify `BaseAddressAuthorizationMessageHandler` is configured

### **Cause 3: Port Mismatch** (5% probability)
- **Symptom:** Requests go to wrong port (7166 vs 7116)
- **Fix:** Update `ApiBaseUrl` configuration

### **Cause 4: Token/API Configuration Mismatch** (5% probability)
- **Symptom:** Has Authorization header but API returns 401
- **Fix:** Verify Auth0 Domain and Audience match

---

## ? **Checklist**

- [ ] Confirmed user is logged in (see name in header)
- [ ] Verified Authorization header is present in Network tab
- [ ] Checked port configuration matches (7116)
- [ ] Verified Auth0 configuration matches between WASM and API
- [ ] Tested token on `/user` page
- [ ] Cleared browser cache and re-logged in
- [ ] Checked API logs for authentication errors
- [ ] Verified CORS policy allows localhost:7008

---

## ?? **Related Documentation**

- [BEARER_TOKEN_CONFIGURATION.md](./BEARER_TOKEN_CONFIGURATION.md)
- [AUTH0_IMPLEMENTATION_GUIDE.md](./Documentation/Auth/AUTH0_IMPLEMENTATION_GUIDE.md)
- [API_BASE_URL_FIX.md](./API_BASE_URL_FIX.md)
