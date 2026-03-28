using RVS.Cust_Intake.Components;
using MudBlazor.Services;
using RVS.Cust_Intake.Client.State;
using RVS.UI.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Razor components with Interactive WebAssembly support
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// MudBlazor component library
builder.Services.AddMudServices();

// Anonymous HttpClient pointing to RVS.API — no authentication required
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7116";
builder.Services.AddHttpClient("RVS.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Provide a default HttpClient via IHttpClientFactory for DI consumers
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API"));

// Services shared with the WASM client — registered here for server-side prerendering
builder.Services.AddScoped<IntakeWizardState>();
builder.Services.AddScoped<IntakeApiClient>();
builder.Services.AddScoped<LookupApiClient>();
builder.Services.AddScoped<AttachmentApiClient>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RVS.Cust_Intake.Client._Imports).Assembly);

app.Run();
