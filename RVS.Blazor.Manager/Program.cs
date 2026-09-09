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

// Configure OIDC Authentication with Auth0
builder.Services.AddOidcAuthentication(options =>
{
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

// Decorate the default IAccessTokenProvider with refresh_token-backed renewal
// so users stay signed in for the full 15-day rolling refresh-token lifetime
// (RVS_Technical_PRD.md §10.1) instead of being bounced to login when the
// iframe silent-renewal path fails.
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
await js.InvokeVoidAsync("console.warn", $"[RVS.Manager] Environment       : {builder.HostEnvironment.Environment}");
await js.InvokeVoidAsync("console.warn", $"[RVS.Manager] BaseAddress       : {builder.HostEnvironment.BaseAddress}");
await js.InvokeVoidAsync("console.warn", $"[RVS.Manager] ApiBaseUrl resolved: {apiBaseUrl}");

await app.RunAsync();
