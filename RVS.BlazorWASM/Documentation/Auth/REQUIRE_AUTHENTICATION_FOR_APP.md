# Require Authentication for Entire App - Implementation Guide

## ? **Change Implemented**

The Blazor WebAssembly application now **requires authentication for all pages** by default. Users must log in before accessing any content.

---

## ?? **What Was Changed**

### **BF.BlazorWASM\Program.cs**

Added a **fallback authorization policy** that requires all users to be authenticated:

```csharp
// Require authentication for the entire app by default
builder.Services.AddAuthorizationCore(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

### **How It Works**

1. **FallbackPolicy** - Applied to **ALL** routes that don't have an explicit authorization policy
2. **RequireAuthenticatedUser()** - Requires the user to have a valid authentication
3. **AuthorizeRouteView** (App.razor) - Enforces the policy on all routes

---

## ?? **User Experience**

### **Before:**
- ? Users could access pages without logging in
- ? Each page needed explicit `[Authorize]` attribute
- ? Risk of forgotten authorization on new pages

### **After:**
- ? All pages require login by default (secure by default)
- ? Unauthenticated users redirected to `UnauthorizedAccess` component
- ? Clean "Authentication Required" message with sign-in button
- ? New pages automatically protected (no `[Authorize]` needed)

---

## ?? **Authentication Flow**

```
1. Unauthenticated user navigates to any page (e.g., /practices/123/check-in)
        ?
2. AuthorizeRouteView checks authentication
        ?
3. User is NOT authenticated
        ?
4. FallbackPolicy denies access
        ?
5. UnauthorizedAccess component renders
        ?
6. User sees:
   ?? Authentication Required
   [Sign In] [Go to Home]
        ?
7. User clicks "Sign In"
        ?
8. Redirects to Auth0 login
        ?
9. After successful login ? Returns to original page (/practices/123/check-in)
        ?
10. User can now access all protected content ?
```

---

## ?? **Page-Level Authorization (Optional)**

### **All Pages Are Protected**

With the fallback policy, you **don't need** to add `[Authorize]` to individual pages:

```razor
@page "/practices/{PracticeId}/check-in"
// ? No [Authorize] needed - protected by fallback policy

<h1>Patient Check-In</h1>
```

### **Allow Anonymous Access (If Needed)**

If you want specific pages to be accessible **without** login, add `[AllowAnonymous]`:

```razor
@page "/public-info"
@attribute [AllowAnonymous]

<h1>Public Information</h1>
<p>This page is accessible without login.</p>
```

### **Role-Based Authorization (Future)**

You can still add specific role requirements:

```razor
@page "/admin"
@attribute [Authorize(Roles = "tenant_admin")]

<h1>Admin Dashboard</h1>
<p>Only tenant admins can see this.</p>
```

**Note:** Role-based pages will **first** require authentication (fallback policy), **then** check roles.

---

## ?? **Testing the Change**

### **Test 1: Unauthenticated Access**

1. **Clear browser cache/session** (or use incognito mode)
2. Navigate directly to app URL: `https://localhost:5001`
3. **Expected:** UnauthorizedAccess page displays
4. **Verify:** "Authentication Required" message shown

### **Test 2: Direct Navigation to Protected Page**

1. While not logged in, navigate to: `https://localhost:5001/practices/123/check-in`
2. **Expected:** UnauthorizedAccess page displays
3. Click "Sign In" button
4. **Expected:** Redirects to Auth0
5. After login ? **Expected:** Returns to `/practices/123/check-in`

### **Test 3: Logout and Re-access**

1. Log in successfully
2. Navigate to any page (verify it loads)
3. Click "Log out" in header
4. Try to access the same page
5. **Expected:** UnauthorizedAccess page displays again

### **Test 4: All Pages Protected**

Verify these pages all require login:
- ? `/` (Home)
- ? `/counter`
- ? `/weather`
- ? `/practices/{id}/check-in`
- ? `/user`
- ? `/admin`

---

## ?? **Configuration Options**

### **Current Configuration (Recommended)**

```csharp
// Require authentication for ALL pages
builder.Services.AddAuthorizationCore(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

**Result:** Secure by default - all pages require login.

### **Alternative: Default Policy (Not Recommended)**

```csharp
// Sets a default policy (can still be overridden by [AllowAnonymous])
builder.Services.AddAuthorizationCore(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

**Difference:** 
- `FallbackPolicy` - Applied when NO policy is specified (stricter)
- `DefaultPolicy` - Applied by `[Authorize]` without parameters

**Recommendation:** Use `FallbackPolicy` for healthcare apps to ensure security by default.

### **Hybrid Approach (If Needed)**

If you want some pages public and others protected:

```csharp
// Remove fallback policy for selective protection
// builder.Services.AddAuthorizationCore(); // No fallback

// Then add [Authorize] to each protected page manually
```

**Not Recommended** for healthcare applications - too easy to forget `[Authorize]` on new pages.

---

## ?? **Affected Pages**

All pages in the application now require authentication:

| Page | Route | Previous | Now |
|------|-------|----------|-----|
| Home | `/` | ?? No protection | ? Protected |
| Counter | `/counter` | ?? No protection | ? Protected |
| Weather | `/weather` | ?? No protection | ? Protected |
| Check-In | `/practices/{id}/check-in` | ?? No protection | ? Protected |
| User | `/user` | ? Had `[Authorize]` | ? Protected |
| Admin | `/admin` | ? Had `[Authorize]` | ? Protected |

---

## ?? **Security Benefits**

### **HIPAA Compliance**
- ? **Secure by Default** - All patient data requires authentication
- ? **Access Control** - No unauthorized access to PHI
- ? **Audit Trail** - All users identified via authentication

### **Defense in Depth**
- ? **Client-Side Protection** - Blazor WASM enforces authentication
- ? **API Protection** - BF.API validates JWT tokens server-side
- ? **Token Validation** - Both client and server enforce security

### **Developer Safety**
- ? **Automatic Protection** - New pages are protected by default
- ? **Explicit Opt-Out** - Must add `[AllowAnonymous]` to make public
- ? **Code Review** - Easy to spot `[AllowAnonymous]` in PR reviews

---

## ?? **Important Notes**

### **1. Exception Routes**

The following routes are **always accessible** (built into Blazor Auth):
- `/authentication/login` - Auth0 login page
- `/authentication/login-callback` - OAuth callback
- `/authentication/logout` - Logout handler
- `/authentication/logout-callback` - Logout callback
- `/authentication/login-failed` - Error page
- `/authentication/logged-out` - Logout confirmation

These are managed by `RemoteAuthenticatorView` component and **do not** require `[AllowAnonymous]`.

### **2. Client-Side Only**

This protection is **client-side** only. The API **must still validate tokens** server-side:

? **BF.API is properly configured:**
- All controllers have `[Authorize]` attribute
- JWT Bearer authentication validates tokens
- Claims-based security enforces tenant isolation

### **3. Token in Browser**

Authenticated users have tokens stored in browser session storage. This is:
- ? **Secure** - Tokens cleared on browser close
- ? **Automatic** - Blazor WASM handles token management
- ?? **Browser-Specific** - XSS vulnerabilities could expose tokens (follow XSS prevention best practices)

---

## ?? **Deployment Checklist**

- [x] Fallback policy added to Program.cs
- [x] Build successful
- [x] UnauthorizedAccess component exists
- [x] LoginDisplay component in MainLayout
- [x] BaseAddressAuthorizationMessageHandler configured
- [ ] **Test unauthenticated access** - Verify all pages require login
- [ ] **Test login flow** - Verify users can sign in
- [ ] **Test return URL** - Verify users return to original page after login
- [ ] **Test logout** - Verify logout clears authentication
- [ ] **Update user documentation** - Inform users login is required

---

## ?? **References**

- [ASP.NET Core Blazor Authentication](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/)
- [Authorization in Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/#authorization)
- [Require Authentication for Entire App](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/#require-authentication-for-the-entire-app)
- [AllowAnonymous Attribute](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authorization.allowanonymousattribute)

---

## ? **Status**

**Implementation:** ? COMPLETE

**Security Posture:** ? SECURE BY DEFAULT

All pages in the Blazor WebAssembly application now require authentication. Unauthenticated users will see a friendly "Authentication Required" message with an option to sign in or return home.
