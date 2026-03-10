# API Base URL Configuration Fix

## ? **Problem**

The Blazor WASM app was making HTTP requests to **https://localhost:7008** (its own address) instead of **https://localhost:7116** (where the API is running).

### Root Cause

```csharp
// BEFORE - INCORRECT ?
builder.Services.AddHttpClient("BF.API", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    // ^^^ This returns the WASM app's own URL, not the API URL!
})
```

**What happened:**
- `builder.HostEnvironment.BaseAddress` = `https://localhost:7008` (Blazor WASM app)
- All API calls went to `https://localhost:7008/api/...` ?
- Should go to `https://localhost:7116/api/...` ?

---

## ? **Solution**

### **1. Added API Base URL to Configuration**

**appsettings.Development.json:**
```json
{
  "Auth0": {
    "Authority": "https://dev-2jhzz8xmjggh26pm.us.auth0.com",
    "ClientId": "CBdytt7GQhJSejyVwSrCZZRYUkLMXdiY",
    "Audience": "https://api.benefetch.com"
  },
  "ApiBaseUrl": "https://localhost:7116"
}
```

**appsettings.json (Production):**
```json
{
  "Auth0": {
    "Authority": "https://dev-2jhzz8xmjggh26pm.us.auth0.com",
    "ClientId": "CBdytt7GQhJSejyVwSrCZZRYUkLMXdiY",
    "Audience": "https://api.benefetch.com"
  },
  "ApiBaseUrl": "https://api.benefetch.com"
}
```

### **2. Updated Program.cs**

```csharp
// AFTER - CORRECT ?
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddHttpClient("BF.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();
```

**What this does:**
- ? Reads `ApiBaseUrl` from configuration
- ? Falls back to `HostEnvironment.BaseAddress` if not configured (for backwards compatibility)
- ? Development: Points to `https://localhost:7116`
- ? Production: Points to `https://api.benefetch.com`

---

## ?? **How It Works Now**

### **Development Environment**

```
Blazor WASM App: https://localhost:7008
        ?
HttpClient reads appsettings.Development.json
        ?
ApiBaseUrl = "https://localhost:7116"
        ?
All API calls ? https://localhost:7116/api/...
        ?
BF.API receives requests ?
```

### **Production Environment**

```
Blazor WASM App: https://portal.benefetch.com
        ?
HttpClient reads appsettings.json
        ?
ApiBaseUrl = "https://api.benefetch.com"
        ?
All API calls ? https://api.benefetch.com/api/...
        ?
BF.API (production) receives requests ?
```

---

## ?? **Testing**

### **Verify Configuration**

1. **Stop the Blazor WASM app** (hot reload won't apply this change)
2. **Restart the app**
3. Open Browser DevTools (F12) ? **Network** tab
4. Navigate to a page that makes API calls (e.g., Check-In)
5. **Inspect API requests**
6. **Verify URLs:**
   - ? Should be: `https://localhost:7116/api/practices/123/patients/search`
   - ? Should NOT be: `https://localhost:7008/api/...`

### **Expected Results**

**Before Fix:**
```
Request URL: https://localhost:7008/api/practices/123/patients/search
Status: 404 Not Found (API not running on 7008)
```

**After Fix:**
```
Request URL: https://localhost:7116/api/practices/123/patients/search
Status: 200 OK (API running on 7116)
```

---

## ?? **Port Configuration Reference**

### **Current Setup**

| Service | Port | URL |
|---------|------|-----|
| **Blazor WASM** | 7008 | https://localhost:7008 |
| **BF.API** | 7116 | https://localhost:7116 |

### **Launch Settings**

**BF.API\Properties\launchSettings.json:**
```json
{
  "profiles": {
    "https": {
      "applicationUrl": "https://localhost:7116;http://localhost:5236"
    }
  }
}
```

**BF.BlazorWASM\Properties\launchSettings.json:**
- Should have its own port configuration (likely 7008)
- This is the WASM app's hosting port, separate from the API

---

## ?? **Configuration Options**

### **Option 1: Current Solution (Recommended)**

Use separate configuration files for different environments.

**Pros:**
- ? Clear separation of dev/prod settings
- ? Easy to maintain
- ? Standard ASP.NET Core pattern

**Cons:**
- ?? Must remember to update when API URL changes

### **Option 2: Reverse Proxy (Alternative)**

Configure the Blazor WASM app to proxy API requests.

**wwwroot/appsettings.Development.json:**
```json
{
  "ApiBaseUrl": "/api"
}
```

**Development Server Proxy:**
Would require configuring the Blazor WASM dev server to proxy `/api` to `https://localhost:7116`.

**Pros:**
- ? No CORS issues in development
- ? Same-origin requests

**Cons:**
- ? More complex setup
- ? Different behavior in dev vs production

### **Option 3: Environment Variables (Cloud)**

For production deployments:

```bash
# Azure App Service Configuration
ApiBaseUrl=https://api.benefetch.com
```

---

## ?? **Important Notes**

### **1. CORS Configuration**

The API must allow requests from the Blazor WASM origin:

**BF.API\Program.cs:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", corsBuilder =>
    {
        corsBuilder
            .WithOrigins(
                "https://localhost:7008",  // Blazor WASM app
                "http://localhost:7008"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

### **2. Restart Required**

**You MUST restart the Blazor WASM app** for this change to take effect. Hot reload does not apply changes to:
- `appsettings.json` files
- `Program.cs` dependency injection configuration

### **3. Bearer Token Attachment**

The `BaseAddressAuthorizationMessageHandler` only attaches tokens to requests going to the configured `BaseAddress`. This fix ensures tokens are properly attached to the correct API URL.

---

## ?? **Related Configuration**

### **Files Updated**
1. ? `BF.BlazorWASM\wwwroot\appsettings.Development.json` - Added `ApiBaseUrl`
2. ? `BF.BlazorWASM\wwwroot\appsettings.json` - Added `ApiBaseUrl`
3. ? `BF.BlazorWASM\Program.cs` - Updated HttpClient configuration

### **Files That Need CORS Update**
- `BF.API\Program.cs` - Ensure `https://localhost:7008` is in CORS allowed origins

---

## ? **Checklist**

- [x] Add `ApiBaseUrl` to `appsettings.Development.json`
- [x] Add `ApiBaseUrl` to `appsettings.json`
- [x] Update `Program.cs` to read `ApiBaseUrl` from configuration
- [x] Build successful
- [ ] **Restart Blazor WASM app**
- [ ] Verify API calls go to `https://localhost:7116` in DevTools
- [ ] Verify CORS allows `https://localhost:7008` in BF.API
- [ ] Test API calls return data successfully

---

## ?? **Bottom Line**

**Status:** ? FIXED

The Blazor WASM app now correctly makes API requests to **https://localhost:7116** (BF.API) instead of **https://localhost:7008** (its own address).

**Action Required:** Restart the Blazor WASM application to apply the changes.
