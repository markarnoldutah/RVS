using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// FluentUI component library
builder.Services.AddFluentUIComponents();

// Anonymous HttpClient pointing to RVS.API — no authentication required
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddHttpClient("RVS.API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Provide a default HttpClient via IHttpClientFactory for DI consumers
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("RVS.API"));

await builder.Build().RunAsync();
