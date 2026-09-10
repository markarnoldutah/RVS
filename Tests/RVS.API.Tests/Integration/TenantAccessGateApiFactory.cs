using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Integration;

/// <summary>
/// Boots the real <c>RVS.API</c> host (the actual <c>Program.cs</c> middleware pipeline, DI
/// lifetimes and allowlist) for the tenant access gate integration tests, swapping only the
/// pieces that need a live Azure account:
/// <list type="bullet">
///   <item><description><see cref="ITenantConfigRepository"/> → <see cref="FakeTenantConfigRepository"/> (seeded per test).</description></item>
///   <item><description>Authentication → <see cref="TestAuthHandler"/> (header-driven principal).</description></item>
///   <item><description>Integration clients forced to their mock/no-op implementations; Cosmos/Blob health checks removed.</description></item>
/// </list>
/// <see cref="RVS.API.Services.TenantConfigService"/> and <c>TenantAccessGateMiddleware</c>
/// themselves run unmodified.
/// </summary>
public sealed class TenantAccessGateApiFactory : WebApplicationFactory<Program>
{
    static TenantAccessGateApiFactory()
    {
        // Program.cs reads these during service registration (before the host is built), so
        // ConfigureAppConfiguration / in-memory sources land too late. Environment variables
        // are picked up by WebApplicationBuilder's default config and override appsettings.
        // Force the rule-based / mock / no-op integration fallbacks so the host builds with no
        // Azure OpenAI / ACS / Cosmos configuration.
        Environment.SetEnvironmentVariable("Integrations__UseMocks", "true");
        Environment.SetEnvironmentVariable("CosmosDb__Endpoint", "https://localhost:8081");
        // Cosmos emulator well-known key — valid base64 so the (unused) CosmosClient singleton
        // factory never throws on format if something resolves it.
        Environment.SetEnvironmentVariable(
            "CosmosDb__Key",
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
    }

    public FakeTenantConfigRepository TenantConfigRepository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // Real TenantConfigService, faked backing store.
            services.RemoveAll<ITenantConfigRepository>();
            services.AddSingleton<ITenantConfigRepository>(TenantConfigRepository);

            // Header-driven auth in place of Auth0 JWT Bearer. Program.cs sets the default
            // schemes explicitly to "Bearer", so override them after registering the handler.
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });

            // Make the test-only GateProbeController discoverable.
            services.AddControllers().AddApplicationPart(typeof(GateProbeController).Assembly);

            // Cosmos / Blob health checks would hit real infrastructure — drop them so the
            // /health allowlist assertion doesn't depend on a live account.
            services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
        });
    }
}
