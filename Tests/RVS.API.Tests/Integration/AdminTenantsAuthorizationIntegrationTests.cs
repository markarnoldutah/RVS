using System.Net;
using System.Text;
using FluentAssertions;

namespace RVS.API.Tests.Integration;

/// <summary>
/// Spec P-7 (issue #563): every <c>api/admin/*</c> endpoint needs <b>both</b> the
/// <c>platform:tenants:manage</c> permission and a caller <c>sub</c> on
/// <c>Admin:AllowedUserIds</c>. Driven through the real <c>Program.cs</c> pipeline — the
/// <c>PlatformAdmin</c> policy, its allowlist requirement and the middleware order — with only
/// Auth0 and the tenant repository faked.
/// </summary>
public sealed class AdminTenantsAuthorizationIntegrationTests : IClassFixture<TenantAccessGateApiFactory>
{
    private const string TenantsPath = "/api/admin/tenants";
    private const string PlatformPermission = "platform:tenants:manage";
    private const string PlatformTenant = "org_rvs_platform";

    private readonly TenantAccessGateApiFactory _factory;

    public AdminTenantsAuthorizationIntegrationTests(TenantAccessGateApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DealerToken_Returns403()
    {
        var request = Authed(HttpMethod.Get, TenantsPath,
            userId: "auth0|dealer-owner",
            tenantId: "org_dealer_rv",
            permissions: "service-requests:read,locations:create,tenants:config:update");

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformPermission_CallerNotOnAllowlist_Returns403()
    {
        var request = Authed(HttpMethod.Get, TenantsPath,
            userId: "auth0|not-on-allowlist",
            tenantId: PlatformTenant,
            permissions: PlatformPermission);

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AllowlistedCaller_WithoutPlatformPermission_Returns403()
    {
        var request = Authed(HttpMethod.Get, TenantsPath,
            userId: TenantAccessGateApiFactory.AdminUserId,
            tenantId: PlatformTenant,
            permissions: "service-requests:read");

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformPermission_AndAllowlistedCaller_Returns200()
    {
        var request = Authed(HttpMethod.Get, TenantsPath,
            userId: TenantAccessGateApiFactory.AdminUserId,
            tenantId: PlatformTenant,
            permissions: PlatformPermission);

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().StartWith("[");
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync(TenantsPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST", "/api/admin/tenants")]
    [InlineData("PUT", "/api/admin/tenants/org_dealer_rv")]
    [InlineData("POST", "/api/admin/tenants/org_dealer_rv/users")]
    [InlineData("POST", "/api/admin/tenants/org_dealer_rv/users/auth0%7Cu1/password-ticket")]
    [InlineData("PUT", "/api/admin/tenants/org_dealer_rv/access-gate")]
    [InlineData("POST", "/api/admin/tenants/org_dealer_rv/locations")]
    public async Task WriteEndpoints_PlatformPermissionButNotAllowlisted_Return403(string method, string path)
    {
        var request = Authed(new HttpMethod(method), path,
            userId: "auth0|not-on-allowlist",
            tenantId: PlatformTenant,
            permissions: PlatformPermission);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static HttpRequestMessage Authed(HttpMethod method, string path, string userId, string tenantId, string permissions)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthHandler.AuthenticatedHeader, "true");
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.TenantIdHeader, tenantId);
        request.Headers.Add(TestAuthHandler.PermissionsHeader, permissions);
        return request;
    }
}
