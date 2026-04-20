using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace RVS.API.Tests.Authorization;

public sealed class AuthorizationPolicyTests
{
    private readonly IAuthorizationService _authorizationService;

    public AuthorizationPolicyTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("CanReadServiceRequests", policy =>
                policy.RequireClaim("permissions", "service-requests:read"));
            options.AddPolicy("CanSearchServiceRequests", policy =>
                policy.RequireClaim("permissions", "service-requests:search"));
            options.AddPolicy("CanCreateServiceRequests", policy =>
                policy.RequireClaim("permissions", "service-requests:create"));
            options.AddPolicy("CanUpdateServiceRequests", policy =>
                policy.RequireClaim("permissions", "service-requests:update"));
            options.AddPolicy("CanUpdateServiceEvent", policy =>
                policy.RequireClaim("permissions", "service-requests:update-service-event"));
            options.AddPolicy("CanDeleteServiceRequests", policy =>
                policy.RequireClaim("permissions", "service-requests:delete"));
            options.AddPolicy("CanUploadAttachments", policy =>
                policy.RequireClaim("permissions", "attachments:upload"));
            options.AddPolicy("CanReadAttachments", policy =>
                policy.RequireClaim("permissions", "attachments:read"));
            options.AddPolicy("CanDeleteAttachments", policy =>
                policy.RequireClaim("permissions", "attachments:delete"));
            options.AddPolicy("CanReadDealerships", policy =>
                policy.RequireClaim("permissions", "dealerships:read"));
            options.AddPolicy("CanUpdateDealerships", policy =>
                policy.RequireClaim("permissions", "dealerships:update"));
            options.AddPolicy("CanReadLocations", policy =>
                policy.RequireClaim("permissions", "locations:read"));
            options.AddPolicy("CanCreateLocations", policy =>
                policy.RequireClaim("permissions", "locations:create"));
            options.AddPolicy("CanUpdateLocations", policy =>
                policy.RequireClaim("permissions", "locations:update"));
            options.AddPolicy("CanReadAnalytics", policy =>
                policy.RequireClaim("permissions", "analytics:read"));
            options.AddPolicy("CanManageTenantConfig", policy =>
                policy.RequireClaim("permissions", "tenants:config:read", "tenants:config:create", "tenants:config:update"));
            options.AddPolicy("CanReadLookups", policy =>
                policy.RequireClaim("permissions", "lookups:read"));
            options.AddPolicy("PlatformAdmin", policy =>
                policy.RequireClaim("permissions", "platform:tenants:manage"));
        });

        var provider = services.BuildServiceProvider();
        _authorizationService = provider.GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal CreateUserWithPermissions(params string[] permissions)
    {
        var claims = permissions.Select(p => new Claim("permissions", p)).ToList();
        claims.Add(new Claim(ClaimTypes.Name, "test-user"));
        var identity = new ClaimsIdentity(claims, "TestScheme");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateUnauthenticatedUser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    [Theory]
    [InlineData("CanReadServiceRequests", "service-requests:read")]
    [InlineData("CanSearchServiceRequests", "service-requests:search")]
    [InlineData("CanCreateServiceRequests", "service-requests:create")]
    [InlineData("CanUpdateServiceRequests", "service-requests:update")]
    [InlineData("CanUpdateServiceEvent", "service-requests:update-service-event")]
    [InlineData("CanDeleteServiceRequests", "service-requests:delete")]
    [InlineData("CanUploadAttachments", "attachments:upload")]
    [InlineData("CanReadAttachments", "attachments:read")]
    [InlineData("CanDeleteAttachments", "attachments:delete")]
    [InlineData("CanReadDealerships", "dealerships:read")]
    [InlineData("CanUpdateDealerships", "dealerships:update")]
    [InlineData("CanReadLocations", "locations:read")]
    [InlineData("CanCreateLocations", "locations:create")]
    [InlineData("CanUpdateLocations", "locations:update")]
    [InlineData("CanReadAnalytics", "analytics:read")]
    [InlineData("CanReadLookups", "lookups:read")]
    [InlineData("PlatformAdmin", "platform:tenants:manage")]
    public async Task Policy_WithCorrectPermission_ShouldSucceed(string policyName, string permission)
    {
        var user = CreateUserWithPermissions(permission);

        var result = await _authorizationService.AuthorizeAsync(user, policyName);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("CanReadServiceRequests")]
    [InlineData("CanSearchServiceRequests")]
    [InlineData("CanCreateServiceRequests")]
    [InlineData("CanUpdateServiceRequests")]
    [InlineData("CanUpdateServiceEvent")]
    [InlineData("CanDeleteServiceRequests")]
    [InlineData("CanUploadAttachments")]
    [InlineData("CanReadAttachments")]
    [InlineData("CanDeleteAttachments")]
    [InlineData("CanReadDealerships")]
    [InlineData("CanUpdateDealerships")]
    [InlineData("CanReadLocations")]
    [InlineData("CanCreateLocations")]
    [InlineData("CanUpdateLocations")]
    [InlineData("CanReadAnalytics")]
    [InlineData("CanManageTenantConfig")]
    [InlineData("CanReadLookups")]
    [InlineData("PlatformAdmin")]
    public async Task Policy_WithMissingPermission_ShouldFail(string policyName)
    {
        var user = CreateUserWithPermissions("unrelated:permission");

        var result = await _authorizationService.AuthorizeAsync(user, policyName);

        result.Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData("CanReadServiceRequests")]
    [InlineData("CanSearchServiceRequests")]
    [InlineData("CanCreateServiceRequests")]
    [InlineData("CanUpdateServiceRequests")]
    [InlineData("CanUpdateServiceEvent")]
    [InlineData("CanDeleteServiceRequests")]
    [InlineData("CanUploadAttachments")]
    [InlineData("CanReadAttachments")]
    [InlineData("CanDeleteAttachments")]
    [InlineData("CanReadDealerships")]
    [InlineData("CanUpdateDealerships")]
    [InlineData("CanReadLocations")]
    [InlineData("CanCreateLocations")]
    [InlineData("CanUpdateLocations")]
    [InlineData("CanReadAnalytics")]
    [InlineData("CanManageTenantConfig")]
    [InlineData("CanReadLookups")]
    [InlineData("PlatformAdmin")]
    public async Task Policy_WithUnauthenticatedUser_ShouldFail(string policyName)
    {
        var user = CreateUnauthenticatedUser();

        var result = await _authorizationService.AuthorizeAsync(user, policyName);

        result.Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData("tenants:config:read")]
    [InlineData("tenants:config:create")]
    [InlineData("tenants:config:update")]
    public async Task CanManageTenantConfig_WithAnyTenantConfigPermission_ShouldSucceed(string permission)
    {
        var user = CreateUserWithPermissions(permission);

        var result = await _authorizationService.AuthorizeAsync(user, "CanManageTenantConfig");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task PlatformAdmin_WithPlatformTenantsManagePermission_ShouldSucceed()
    {
        var user = CreateUserWithPermissions("platform:tenants:manage");

        var result = await _authorizationService.AuthorizeAsync(user, "PlatformAdmin");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task PlatformAdmin_WithNonPlatformPermission_ShouldFail()
    {
        var user = CreateUserWithPermissions("tenants:config:update");

        var result = await _authorizationService.AuthorizeAsync(user, "PlatformAdmin");

        result.Succeeded.Should().BeFalse();
    }
}
