using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using RVS.Blazor.Intake;
using RVS.Blazor.Intake.State;
using RVS.UI.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// MudBlazor component library
builder.Services.AddMudServices();

// Anonymous HttpClient pointing to RVS.API — no authentication required
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddHttpClient("RVS.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Provide a default HttpClient via IHttpClientFactory for DI consumers
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API"));

// Typed API clients for intake wizard
builder.Services.AddScoped<IntakeApiClient>();
builder.Services.AddScoped<LookupApiClient>();
builder.Services.AddScoped<AttachmentApiClient>();

// Intake wizard shared state — scoped (one per browser tab lifetime)
builder.Services.AddScoped<IntakeWizardState>();

await builder.Build().RunAsync();
