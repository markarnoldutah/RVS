using Blazored.LocalStorage;
using Blazored.SessionStorage;
using RVS.BlazorWASM;
using RVS.BlazorWASM.Services;
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
    // Note: Don't specify scopes here - use the token obtained during login
    return handler;
});

// Register AuthorizationMessageHandler
builder.Services.AddScoped<AuthorizationMessageHandler>();

// Provide HttpClient to services via factory
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("BF.API"));

builder.Services.AddFluentUIComponents();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredSessionStorage();

// Register API services
builder.Services.AddScoped<IPatientApiService, PatientApiService>();
builder.Services.AddScoped<ICheckInApiService, CheckInApiService>();
builder.Services.AddScoped<IEligibilityCheckApiService, EligibilityCheckApiService>();
builder.Services.AddScoped<IEligibilityCheckPollingService, EligibilityCheckPollingService>();
builder.Services.AddScoped<ILookupApiService, LookupApiService>();

// Register lookup cache as scoped (same lifetime as HttpClient in WASM)
builder.Services.AddScoped<ILookupCacheService, LookupCacheService>();

// Register user session service (handles auth state changes and session initialization)
builder.Services.AddScoped<IUserSessionService, UserSessionService>();

// Configure OIDC Authentication with Auth0
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Auth0", options.ProviderOptions);
    
    // PKCE: Use authorization code flow
    options.ProviderOptions.ResponseType = "code";
    
    // Request scopes during login (not during API calls)
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

    // Auth0 claim mapping
    options.UserOptions.RoleClaim = "roles";
    options.UserOptions.NameClaim = "name";
});

// Add cascading authentication state
builder.Services.AddCascadingAuthenticationState();

// Require authentication for the entire app by default
builder.Services.AddAuthorizationCore(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

await builder.Build().RunAsync();
