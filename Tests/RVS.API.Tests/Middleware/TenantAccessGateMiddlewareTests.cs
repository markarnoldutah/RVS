using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Middleware;

public sealed class TenantAccessGateMiddlewareTests
{
    private readonly Mock<ITenantService> _tenantServiceMock = new();
    private readonly TenantAccessGateMiddleware _middleware;
    private bool _nextCalled;

    public TenantAccessGateMiddlewareTests()
    {
        _middleware = new TenantAccessGateMiddleware(ctx =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        });
    }

    private HttpContext CreateAuthenticatedContext(string path, string tenantId = "t1")
    {
        var claims = new[] { new Claim("https://rvserviceflow.com/tenantId", tenantId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private HttpContext CreateUnauthenticatedContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Theory]
    [InlineData("/api/tenants/config")]
    [InlineData("/health")]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    public async Task InvokeAsync_AllowlistedPrefixPath_ShouldPassThrough(string path)
    {
        var ctx = CreateAuthenticatedContext(path);

        await _middleware.InvokeAsync(ctx, _tenantServiceMock.Object);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_IntakeEndpoint_ShouldPassThrough()
    {
        var ctx = CreateAuthenticatedContext("/api/service-requests/intake");

        await _middleware.InvokeAsync(ctx, _tenantServiceMock.Object);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_IntakeSubpath_ShouldPassThrough()
    {
        var ctx = CreateAuthenticatedContext("/api/service-requests/intake/attachments");

        await _middleware.InvokeAsync(ctx, _tenantServiceMock.Object);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_StatusEndpoint_ShouldPassThrough()
    {
        var ctx = CreateAuthenticatedContext("/api/service-requests/sr-123/status");

        await _middleware.InvokeAsync(ctx, _tenantServiceMock.Object);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_ShouldPassThrough()
    {
        var ctx = CreateUnauthenticatedContext("/api/some-endpoint");

        await _middleware.InvokeAsync(ctx, _tenantServiceMock.Object);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedNoTenantId_ShouldReturn403()
    {
        var identity = new ClaimsIdentity([], "TestAuth");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context.Request.Path = "/api/some-endpoint";
        context.Response.Body = new MemoryStream();

        await _middleware.InvokeAsync(context, _tenantServiceMock.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_TenantDisabled_ShouldReturn403WithCorrectFormat()
    {
        var ctx = CreateAuthenticatedContext("/api/some-endpoint");
        _tenantServiceMock
            .Setup(s => s.GetAccessGateAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAccessGateEmbedded { LoginsEnabled = false });

        await _middleware.InvokeAsync(ctx, _tenantServiceMock.Object);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _nextCalled.Should().BeFalse();

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(ctx.Response.Body);
        var body = await reader.ReadToEndAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Tenant disabled");
        doc.RootElement.TryGetProperty("errorId", out var errorIdProp).Should().BeTrue();
        errorIdProp.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_TenantEnabled_ShouldPassThrough()
    {
        var ctx = CreateAuthenticatedContext("/api/some-endpoint");
        _tenantServiceMock
            .Setup(s => s.GetAccessGateAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAccessGateEmbedded { LoginsEnabled = true });

        await _middleware.InvokeAsync(ctx, _tenantServiceMock.Object);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MissingTenantId_ResponseContainsErrorId()
    {
        var identity = new ClaimsIdentity([], "TestAuth");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context.Request.Path = "/api/some-endpoint";
        context.Response.Body = new MemoryStream();

        await _middleware.InvokeAsync(context, _tenantServiceMock.Object);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("message", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("errorId", out var errorIdProp).Should().BeTrue();
        errorIdProp.GetString().Should().NotBeNullOrWhiteSpace();
    }
}
