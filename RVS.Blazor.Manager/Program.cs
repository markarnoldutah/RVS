using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using RVS.Blazor.Manager;
using RVS.Blazor.Manager.Services;
using RVS.Blazor.Manager.State;
using RVS.UI.Shared.Services;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// MudBlazor component library
builder.Services.AddMudServices();

// Centralized app state (singleton in WASM — single user)
builder.Services.AddSingleton<ManagerAppState>();

// Theme switcher state (scoped per browser tab)
builder.Services.AddScoped<ThemeService>();

// Get API base URL from configuration
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Configure HttpClient for API calls with automatic bearer token attachment
builder.Services.AddHttpClient("RVS.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>()
        .ConfigureHandler(authorizedUrls: [apiBaseUrl]);
    return handler;
});

// Register AuthorizationMessageHandler
builder.Services.AddScoped<AuthorizationMessageHandler>();

// Provide a default HttpClient via IHttpClientFactory for DI consumers
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API"));

// MudBlazor component library
builder.Services.AddMudServices();

// Register typed API clients using the named HttpClient
builder.Services.AddScoped<RVS.UI.Shared.Services.ServiceRequestApiClient>(sp =>
    new(sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API")));
builder.Services.AddScoped<RVS.UI.Shared.Services.AnalyticsApiClient>(sp =>
    new(sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API")));
builder.Services.AddScoped<RVS.UI.Shared.Services.LookupApiClient>(sp =>
    new(sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API")));
builder.Services.AddScoped<RVS.UI.Shared.Services.AttachmentApiClient>(sp =>
    new(sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API")));

// Platform-admin provisioning (issue #563) — the hidden /admin pages. The API is the only gate.
builder.Services.AddScoped<AdminApiClient>(sp =>
    new(sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API")));

// This device's "Keep me signed in" answer, read from js/session-persist.js once the host is
// built (below). The OIDC options are resolved lazily on first use, after that read; if they were
// ever resolved first, the null default fails closed and forces the password prompt.
string? keepSignedInPreference = null;
var keepSignedInPreferenceResolved = false;

// Configure OIDC Authentication with Auth0
builder.Services.AddOidcAuthentication(options =>
{
    // Force Auth0's password prompt unless this device opted in to staying signed in, so Auth0's
    // own session cookie cannot silently sign the next person into a shared computer (issue #498).
    KeepSignedInPolicy.ApplyTo(
        options.ProviderOptions.AdditionalProviderParameters,
        keepSignedInPreferenceResolved ? keepSignedInPreference : null);

    builder.Configuration.Bind("Auth0", options.ProviderOptions);

    // PKCE: Use authorization code flow
    options.ProviderOptions.ResponseType = "code";

    // Standard OIDC scopes — `offline_access` triggers Auth0 to issue a
    // refresh_token, which RefreshingAccessTokenProvider exchanges for a new
    // access_token at the /oauth/token endpoint when the cached token is
    // expired/expiring. Without it, Microsoft's library falls back to an
    // iframe SSO check that browsers increasingly block.
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
    options.ProviderOptions.DefaultScopes.Add("offline_access");

    // Auth0 API access: audience parameter required for access tokens
    var audience = builder.Configuration["Auth0:Audience"];
    if (!string.IsNullOrEmpty(audience))
    {
        options.ProviderOptions.AdditionalProviderParameters.Add("audience", audience);
    }

    // Force Auth0 to always show the login screen (no silent SSO re-use)
    // options.ProviderOptions.AdditionalProviderParameters.Add("prompt", "login");

    // Auth0 claim mapping
    options.UserOptions.RoleClaim = "roles";
    options.UserOptions.NameClaim = "name";
});

// Dedicated HttpClient for the Auth0 token endpoint — separate from the
// API client so the bearer-attaching AuthorizationMessageHandler does not
// run on token-refresh requests (which authenticate via refresh_token, not
// the access token we are trying to renew).
builder.Services.AddHttpClient("Auth0.Token");

// Revokes the refresh token at Auth0 on sign-out (issue #498) — uses the same bearer-free client.
builder.Services.AddScoped(sp => new RefreshTokenRevocationClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Auth0.Token"),
    builder.Configuration["Auth0:Authority"]
        ?? throw new InvalidOperationException("Auth0:Authority is not configured."),
    builder.Configuration["Auth0:ClientId"]
        ?? throw new InvalidOperationException("Auth0:ClientId is not configured.")));

// Decorate the default IAccessTokenProvider with refresh_token-backed renewal
// so users stay signed in for the full 30-day rotating refresh-token lifetime
// (Spec C-7, issue #498) instead of being bounced to login when the iframe
// silent-renewal path fails. js/session-persist.js keeps the token across
// browser and installed-PWA restarts.
//
// The inner provider is the RemoteAuthenticationService<> that AddOidcAuthentication
// registers as the AuthenticationStateProvider implementation. It is NOT registered
// under its own concrete type, so we resolve AuthenticationStateProvider and cast to
// IAccessTokenProvider (the framework's own idiom). Resolving IAccessTokenProvider
// here would recurse into this very factory.
builder.Services.AddScoped<IAccessTokenProvider>(sp =>
    new RefreshingAccessTokenProvider(
        (IAccessTokenProvider)sp.GetRequiredService<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<IJSRuntime>(),
        builder.Configuration,
        sp.GetRequiredService<ILogger<RefreshingAccessTokenProvider>>()));

// Add cascading authentication state
builder.Services.AddCascadingAuthenticationState();

// Require authentication for the entire app by default
builder.Services.AddAuthorizationCore(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// Startup diagnostics — console.warn is always visible in browser DevTools (F12 → Console)
var js = app.Services.GetRequiredService<IJSRuntime>();

// Read this device's "Keep me signed in" answer before anything resolves the OIDC options
// (see KeepSignedInPolicy above). A failed read leaves it unresolved, which forces the prompt.
try
{
    keepSignedInPreference = await js.InvokeAsync<string?>("rvsSession_getPersistPreference");
    keepSignedInPreferenceResolved = true;
}
catch (JSException)
{
    // session-persist.js missing or storage blocked — fail closed.
}
await js.InvokeVoidAsync("console.warn", $"[RVS.Manager] Environment       : {builder.HostEnvironment.Environment}");
await js.InvokeVoidAsync("console.warn", $"[RVS.Manager] BaseAddress       : {builder.HostEnvironment.BaseAddress}");
await js.InvokeVoidAsync("console.warn", $"[RVS.Manager] ApiBaseUrl resolved: {apiBaseUrl}");

await app.RunAsync();
