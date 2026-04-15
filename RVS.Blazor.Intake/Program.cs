using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using RVS.Blazor.Intake;
using RVS.Blazor.Intake.Services;
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

// Theme switcher — scoped (one per browser tab lifetime)
builder.Services.AddScoped<ThemeService>();

var app = builder.Build();

// Startup diagnostics — console.warn is always visible in browser DevTools (F12 → Console)
var js = app.Services.GetRequiredService<IJSRuntime>();
await js.InvokeVoidAsync("console.warn", $"[RVS.Intake] Environment       : {builder.HostEnvironment.Environment}");
await js.InvokeVoidAsync("console.warn", $"[RVS.Intake] BaseAddress       : {builder.HostEnvironment.BaseAddress}");
await js.InvokeVoidAsync("console.warn", $"[RVS.Intake] ApiBaseUrl config : {apiBaseUrl ?? "(not set)"}");
await js.InvokeVoidAsync("console.warn", $"[RVS.Intake] ApiBaseUrl resolved: {apiBaseUrl}");

await app.RunAsync();
