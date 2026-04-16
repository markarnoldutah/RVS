using System.Diagnostics;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using RVS.API.Telemetry;

namespace RVS.API.Tests.Telemetry;

public sealed class TenantActivityProcessorTests
{
    private readonly Mock<IHttpContextAccessor> _accessorMock = new();
    private readonly TenantActivityProcessor _processor;

    public TenantActivityProcessorTests()
    {
        _processor = new TenantActivityProcessor(_accessorMock.Object);
    }

    [Fact]
    public void OnEnd_WhenHttpContextHasTenantClaim_ShouldSetTenantIdTag()
    {
        var context = CreateContextWithClaims(tenantId: "tenant-42");
        _accessorMock.Setup(a => a.HttpContext).Returns(context);
        using var activity = CreateActivity();

        _processor.OnEnd(activity);

        activity.GetTagItem("TenantId").Should().Be("tenant-42");
    }

    [Fact]
    public void OnEnd_WhenHttpContextHasLocationIdsClaim_ShouldSetLocationIdsTag()
    {
        var context = CreateContextWithClaims(locationIds: "loc-1,loc-2");
        _accessorMock.Setup(a => a.HttpContext).Returns(context);
        using var activity = CreateActivity();

        _processor.OnEnd(activity);

        activity.GetTagItem("LocationIds").Should().Be("loc-1,loc-2");
    }

    [Fact]
    public void OnEnd_WhenCorrelationIdInResponseHeaders_ShouldSetCorrelationIdTag()
    {
        var context = CreateContextWithClaims();
        context.Response.Headers["X-Correlation-ID"] = "corr-abc";
        _accessorMock.Setup(a => a.HttpContext).Returns(context);
        using var activity = CreateActivity();

        _processor.OnEnd(activity);

        activity.GetTagItem("CorrelationId").Should().Be("corr-abc");
    }

    [Fact]
    public void OnEnd_WhenCorrelationIdInRequestHeaders_ShouldSetCorrelationIdTag()
    {
        var context = CreateContextWithClaims();
        context.Request.Headers["X-Correlation-ID"] = "corr-from-request";
        _accessorMock.Setup(a => a.HttpContext).Returns(context);
        using var activity = CreateActivity();

        _processor.OnEnd(activity);

        activity.GetTagItem("CorrelationId").Should().Be("corr-from-request");
    }

    [Fact]
    public void OnEnd_WhenHttpContextIsNull_ShouldNotThrow()
    {
        _accessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        using var activity = CreateActivity();

        var act = () => _processor.OnEnd(activity);

        act.Should().NotThrow();
        activity.GetTagItem("TenantId").Should().BeNull();
    }

    [Fact]
    public void OnEnd_WhenNoClaims_ShouldNotSetTenantOrLocationTags()
    {
        var context = new DefaultHttpContext();
        _accessorMock.Setup(a => a.HttpContext).Returns(context);
        using var activity = CreateActivity();

        _processor.OnEnd(activity);

        activity.GetTagItem("TenantId").Should().BeNull();
        activity.GetTagItem("LocationIds").Should().BeNull();
    }

    [Fact]
    public void OnEnd_WhenAllDimensionsPresent_ShouldStampAll()
    {
        var context = CreateContextWithClaims(tenantId: "t-1", locationIds: "loc-a");
        context.Response.Headers["X-Correlation-ID"] = "corr-123";
        _accessorMock.Setup(a => a.HttpContext).Returns(context);
        using var activity = CreateActivity();

        _processor.OnEnd(activity);

        activity.GetTagItem("TenantId").Should().Be("t-1");
        activity.GetTagItem("LocationIds").Should().Be("loc-a");
        activity.GetTagItem("CorrelationId").Should().Be("corr-123");
    }

    private static DefaultHttpContext CreateContextWithClaims(
        string? tenantId = null,
        string? locationIds = null)
    {
        var claims = new List<Claim>();
        if (tenantId is not null)
        {
            claims.Add(new Claim(TenantActivityProcessor.TenantIdClaimType, tenantId));
        }
        if (locationIds is not null)
        {
            claims.Add(new Claim(TenantActivityProcessor.LocationIdsClaimType, locationIds));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        return context;
    }

    private static Activity CreateActivity()
    {
        var source = new ActivitySource("RVS.Tests");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        return source.StartActivity("TestOperation")!;
    }
}
