using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace RVS.API.Tests.Integration;

/// <summary>
/// End-to-end coverage for <c>TenantAccessGateMiddleware</c> (issue #465). Unlike
/// <c>TenantAccessGateMiddlewareTests</c> (which mocks <c>ITenantConfigService</c>), these
/// drive the real <c>Program.cs</c> pipeline — middleware order, DI lifetimes, the allowlist
/// against real route paths — with only <see cref="ITenantConfigRepository"/> and Auth0
/// faked. The middleware → <c>TenantConfigService</c> → repository chain runs for real.
/// </summary>
public sealed class TenantAccessGateIntegrationTests : IClassFixture<TenantAccessGateApiFactory>
{
    private const string EnabledTenant = "org_enabled_rv";
    private const string DisabledTenant = "org_disabled_rv";
    private const string GatedPath = "/api/gate-probe";

    private readonly TenantAccessGateApiFactory _factory;

    public TenantAccessGateIntegrationTests(TenantAccessGateApiFactory factory)
    {
        _factory = factory;
        _factory.TenantConfigRepository.Seed(EnabledTenant, loginsEnabled: true);
        _factory.TenantConfigRepository.Seed(DisabledTenant, loginsEnabled: false);
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    private static HttpRequestMessage AuthedGet(string path, string? tenantId, string? permissions = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.AuthenticatedHeader, "true");
        if (tenantId is not null)
        {
            request.Headers.Add(TestAuthHandler.TenantIdHeader, tenantId);
        }
        if (permissions is not null)
        {
            request.Headers.Add(TestAuthHandler.PermissionsHeader, permissions);
        }
        return request;
    }

    // 1. Authenticated request, tenant whose config has LoginsEnabled=false → 403 { message, errorId }.
    [Fact]
    public async Task DisabledTenant_AuthenticatedGatedRequest_Returns403TenantDisabled()
    {
        var response = await CreateClient().SendAsync(AuthedGet(GatedPath, DisabledTenant));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("message").GetString().Should().Be("Tenant disabled");
        doc.RootElement.TryGetProperty("errorId", out var errorId).Should().BeTrue();
        errorId.GetString().Should().NotBeNullOrWhiteSpace();
    }

    // 2. Same request, LoginsEnabled=true → passes through to the controller.
    [Fact]
    public async Task EnabledTenant_AuthenticatedGatedRequest_ReachesController()
    {
        var response = await CreateClient().SendAsync(AuthedGet(GatedPath, EnabledTenant));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("reached");
    }

    // 2b. No tenant-config document at all → service defaults to LoginsEnabled=true → passes through.
    [Fact]
    public async Task UnknownTenant_AuthenticatedGatedRequest_ReachesController()
    {
        var response = await CreateClient().SendAsync(AuthedGet(GatedPath, "org_never_seeded"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 3. Authenticated request with no tenantId claim → 403 "Tenant context is missing".
    [Fact]
    public async Task AuthenticatedRequest_NoTenantIdClaim_Returns403TenantContextMissing()
    {
        var response = await CreateClient().SendAsync(AuthedGet(GatedPath, tenantId: null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("message").GetString().Should().Contain("Tenant context is missing");
        doc.RootElement.TryGetProperty("errorId", out var errorId).Should().BeTrue();
        errorId.GetString().Should().NotBeNullOrWhiteSpace();
    }

    // 4. Allowlisted paths pass through even when the tenant is disabled.
    [Fact]
    public async Task AllowlistedHealthPath_DisabledTenant_IsNotGateBlocked()
    {
        var response = await CreateClient().SendAsync(AuthedGet("/health", DisabledTenant));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AllowlistedSwaggerPath_DisabledTenant_IsNotGateBlocked()
    {
        var response = await CreateClient().SendAsync(AuthedGet("/swagger/index.html", DisabledTenant));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AllowlistedTenantConfigPath_DisabledTenant_ReachesControllerAndReturnsConfig()
    {
        // /api/tenants/config is allowlisted precisely so the Manager UI can still load the
        // "tenant disabled" screen. With the required permission it reaches the controller and
        // returns the (disabled) tenant's config rather than a gate 403.
        var response = await CreateClient().SendAsync(
            AuthedGet("/api/tenants/config", DisabledTenant, permissions: "tenants:config:read"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("loginsEnabled");
    }

    [Theory]
    [InlineData("/api/intake/some-dealership-slug")]
    [InlineData("/api/status/")]
    public async Task AllowlistedApiPrefix_DisabledTenant_IsNotGateBlocked(string path)
    {
        // These prefixes are allowlisted so the anonymous intake / status flows keep working
        // for a disabled tenant. No downstream action matches these exact paths, so a 404
        // (routing) rather than a gate 403 confirms the request was let through.
        var response = await CreateClient().SendAsync(AuthedGet(path, DisabledTenant));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertNotBlockedByGateAsync(response);
    }

    // 5. Unauthenticated request to a gated path → not this middleware's 403 (auth handles it → 401).
    [Fact]
    public async Task UnauthenticatedRequest_GatedPath_Returns401NotGate403()
    {
        var response = await CreateClient().GetAsync(GatedPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    private static async Task AssertNotBlockedByGateAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Forbidden)
        {
            return;
        }

        // A 403 is only acceptable here if it did NOT come from the tenant access gate.
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return; // authorization 403 (empty body), not the gate
        }

        string? message = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m))
            {
                message = m.GetString();
            }
        }
        catch (JsonException)
        {
            return;
        }

        message.Should().NotBe("Tenant disabled");
        message.Should().NotContain("Tenant context is missing");
    }
}
