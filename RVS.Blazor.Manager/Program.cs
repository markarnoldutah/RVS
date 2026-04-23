using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using RVS.Blazor.Manager;
using RVS.Blazor.Manager.Services;
using RVS.Blazor.Manager.State;
using RVS.Domain.Interfaces;
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

// Intake config client — anonymous slug-based endpoint for fetching issue categories / intake config.
// Used by the walk-in dialog to populate the shared step components.
builder.Services.AddScoped<RVS.UI.Shared.Services.IntakeApiClient>(sp =>
    new(sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API")));

// IIntakeAiClient — authenticated dealer-scoped implementation. Transient so each step
// instance picks up the current dealership id resolved into ManagerAppState. Callers must
// call ManagerAppState.EnsureDealershipResolvedAsync() before the shared intake steps render.
builder.Services.AddTransient<IIntakeAiClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API");
    var appState = sp.GetRequiredService<ManagerAppState>();
    var dealershipId = appState.SelectedDealershipId
        ?? throw new InvalidOperationException(
            "SelectedDealershipId is not yet resolved. Call ManagerAppState.EnsureDealershipResolvedAsync() before rendering components that inject IIntakeAiClient.");
    return new DealershipIntakeAiClient(http, dealershipId);
});

// Configure OIDC Authentication with Auth0
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Auth0", options.ProviderOptions);

    // PKCE: Use authorization code flow
    options.ProviderOptions.ResponseType = "code";

    // Standard OIDC scopes
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
