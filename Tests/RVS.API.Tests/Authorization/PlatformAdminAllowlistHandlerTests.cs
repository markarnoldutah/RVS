using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Moq;
using RVS.API.Authorization;
using RVS.API.Options;

namespace RVS.API.Tests.Authorization;

/// <summary>
/// Spec P-7: the second check on every <c>api/admin/*</c> endpoint — the caller's <c>sub</c>
/// must be on <c>Admin:AllowedUserIds</c>, in addition to holding <c>platform:tenants:manage</c>.
/// </summary>
public sealed class PlatformAdminAllowlistHandlerTests
{
    private const string AdminUserId = "auth0|admin123";

    [Fact]
    public async Task HandleAsync_WhenNameIdentifierIsAllowlisted_ShouldSucceed()
    {
        var context = CreateContext(new Claim(ClaimTypes.NameIdentifier, AdminUserId));

        await CreateHandler(AdminUserId).HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUnmappedSubClaimIsAllowlisted_ShouldSucceed()
    {
        var context = CreateContext(new Claim("sub", AdminUserId));

        await CreateHandler(AdminUserId).HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAllowlisted_ShouldNotSucceed()
    {
        var context = CreateContext(new Claim(ClaimTypes.NameIdentifier, "auth0|someone-else"));

        await CreateHandler(AdminUserId).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenAllowlistIsEmpty_ShouldNotSucceed()
    {
        var context = CreateContext(new Claim(ClaimTypes.NameIdentifier, AdminUserId));

        await CreateHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenCaseDiffers_ShouldNotSucceed()
    {
        var context = CreateContext(new Claim(ClaimTypes.NameIdentifier, AdminUserId.ToUpperInvariant()));

        await CreateHandler(AdminUserId).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenCallerHasNoUserId_ShouldNotSucceed()
    {
        var context = CreateContext();

        await CreateHandler(AdminUserId).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenAllowlistContainsBlankEntry_ShouldNotLetBlankCallerThrough()
    {
        var context = CreateContext(new Claim(ClaimTypes.NameIdentifier, ""));

        await CreateHandler("", AdminUserId).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static PlatformAdminAllowlistHandler CreateHandler(params string[] allowedUserIds)
    {
        var monitor = new Mock<IOptionsMonitor<AdminOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new AdminOptions { AllowedUserIds = [.. allowedUserIds] });
        return new PlatformAdminAllowlistHandler(monitor.Object);
    }

    private static AuthorizationHandlerContext CreateContext(params Claim[] claims)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestScheme"));
        return new AuthorizationHandlerContext([new PlatformAdminAllowlistRequirement()], user, resource: null);
    }
}
