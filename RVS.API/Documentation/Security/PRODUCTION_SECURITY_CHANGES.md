# BF.API Production Security Configuration - Change Summary

## ? Changes Implemented

### 1. HTTPS Enforcement (Production Only)
**File: `BF.API\Program.cs`**

**Before:**
```csharp
// app.UseHttpsRedirection(); // Commented out
```

**After:**
```csharp
// Production: Enforce HTTPS redirection
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
```

**Impact:**
- ? All HTTP requests automatically redirected to HTTPS in Production
- ? Development environment unchanged - supports both HTTP and HTTPS for local testing
- ? Returns 307 (Temporary Redirect) to preserve request method and body

---

### 2. CORS Policy Restriction
**File: `BF.API\Program.cs`**

**Before:**
```csharp
// TODO secure CORS policy for prod
builder.Services.AddCors(options =>
{
    options.AddPolicy("Open",
    builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// ...later
app.UseCors("Open");
```

**After:**
```csharp
// Configure CORS - Production uses restricted policy, Development uses open policy
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
else
{
    // Development: Allow localhost for testing
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
}

// ...later
// Use environment-specific CORS policy
app.UseCors(app.Environment.IsProduction() ? "ProductionCors" : "DevelopmentCors");
```

**Impact:**
- ? **Production**: Only accepts requests from `https://portal.benefetch.com`
- ? **Development**: Accepts requests from localhost (various ports)
- ? Prevents CSRF attacks from unauthorized domains
- ? Supports credentials (required for authenticated API calls)

---

### 3. Production Configuration Template
**New File: `BF.API\appsettings.Production.json`**

Created production configuration template with:
- Kestrel endpoint configuration for HTTPS-only (port 443)
- Placeholder references for secrets (to be replaced with Key Vault references)
- Production-ready settings (mock disabled, auditing enabled)

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

---

### 4. Updated Documentation
**File: `BF.BlazorWASM\AUTH0_IMPLEMENTATION_GUIDE.md`**

- ? Marked HTTPS configuration as completed
- ? Marked CORS restriction as completed
- ? Updated status indicators to reflect production-ready state
- ? Documented environment-based policy switching

---

## ?? Security Improvements

### Before
- ? HTTPS redirection disabled
- ? CORS allowed requests from ANY origin
- ? No environment-based security configuration
- ? High risk of CSRF attacks

### After
- ? HTTPS enforced in Production
- ? CORS restricted to `portal.benefetch.com` only in Production
- ? Environment-aware security policies
- ? CSRF protection through origin validation
- ? Maintains developer experience with flexible Development settings

---

## ?? Testing the Changes

### Local Development Testing
1. **Start the API in Development mode**
   ```bash
   cd BF.API
   dotnet run --environment Development
   ```

2. **Verify CORS accepts localhost**
   - Blazor app should successfully call API from `https://localhost:5001`
   - Check browser console for CORS errors (should be none)

3. **Verify HTTPS redirection is OFF in Development**
   - HTTP requests to `http://localhost:5236` should work normally
   - No automatic redirect to HTTPS

### Production Testing (After Deployment)

1. **Verify HTTPS redirection**
   ```bash
   curl -I http://api.benefetch.com/api/health
   # Should return 307 redirect to https://
   ```

2. **Verify CORS restriction**
   ```bash
   # From portal.benefetch.com - should succeed
   # From any other domain - should fail with CORS error
   ```

3. **Test unauthorized origin (should fail)**
   ```bash
   curl -H "Origin: https://malicious-site.com" \
        -H "Access-Control-Request-Method: GET" \
        -X OPTIONS https://api.benefetch.com/api/tenants/config
   # Should not include Access-Control-Allow-Origin header
   ```

4. **Test authorized origin (should succeed)**
   ```bash
   curl -H "Origin: https://portal.benefetch.com" \
        -H "Access-Control-Request-Method: GET" \
        -X OPTIONS https://api.benefetch.com/api/tenants/config
   # Should include: Access-Control-Allow-Origin: https://portal.benefetch.com
   ```

---

## ?? Deployment Checklist

### Azure App Service Configuration

- [ ] **Enable "HTTPS Only"** in App Service settings
  - Portal ? App Service ? Settings ? Configuration ? General settings
  - Set "HTTPS Only" to "On"

- [ ] **Configure SSL Certificate**
  - Use App Service Managed Certificate, or
  - Upload custom certificate for `api.benefetch.com`

- [ ] **Set Environment to Production**
  - Configuration ? Application settings
  - Add/Update: `ASPNETCORE_ENVIRONMENT` = `Production`

- [ ] **Verify CORS settings**
  - Test API calls from portal.benefetch.com
  - Verify other origins are blocked

### DNS Configuration

- [ ] **Verify A/CNAME records**
  - Ensure `api.benefetch.com` points to Azure App Service
  - Ensure `portal.benefetch.com` points to Blazor WASM hosting

### Auth0 Configuration

- [ ] **Update Auth0 API settings**
  - Add `https://portal.benefetch.com` to allowed origins (if applicable)
  - Verify API identifier matches configuration

- [ ] **Update Auth0 SPA Application**
  - Add callback URLs for production Blazor app
  - Add logout URLs for production Blazor app

---

## ?? Rollback Plan

If issues occur in production:

1. **Disable HTTPS redirection temporarily:**
   ```csharp
   // Comment out in Program.cs
   // if (app.Environment.IsProduction())
   // {
   //     app.UseHttpsRedirection();
   // }
   ```

2. **Revert to open CORS (NOT RECOMMENDED):**
   ```csharp
   // Temporary workaround only
   app.UseCors("DevelopmentCors");
   ```

3. **Redeploy previous version from Git**
   ```bash
   git revert <commit-hash>
   git push
   # Trigger Azure deployment
   ```

---

## ?? References

- [ASP.NET Core Enforcing SSL](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl)
- [ASP.NET Core CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
- [Azure App Service HTTPS](https://learn.microsoft.com/en-us/azure/app-service/configure-ssl-bindings)
- [OWASP CSRF Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
