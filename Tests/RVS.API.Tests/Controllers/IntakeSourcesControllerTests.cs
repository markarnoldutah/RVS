using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="IntakeSourcesController"/> — the per-location channel report
/// (<c>Spec A-13</c>, issue #599).
/// </summary>
public class IntakeSourcesControllerTests
{
    private const string TenantId = "ten_nova_rv";
    private const string LocationId = "loc_hurricane";

    private readonly Mock<IIntakeSourceReportService> _serviceMock = new();
    private readonly IntakeSourcesController _sut;

    public IntakeSourcesControllerTests()
    {
        _serviceMock.Setup(s => s.GetForLocationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntakeSourceReportResponseDto { LocationId = LocationId });

        _sut = new IntakeSourcesController(_serviceMock.Object, BuildClaimsService(TenantId));
    }

    [Fact]
    public async Task GetReport_ShouldReturnOkWithTheReport()
    {
        var result = await _sut.GetReport(LocationId, null, null, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<IntakeSourceReportResponseDto>()
            .Which.LocationId.Should().Be(LocationId);
    }

    [Fact]
    public async Task GetReport_ShouldScopeToTheCallersTenantAndForwardTheWindow()
    {
        var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

        await _sut.GetReport(LocationId, from, to, CancellationToken.None);

        _serviceMock.Verify(s => s.GetForLocationAsync(
            TenantId, LocationId, from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReport_WhenTenantClaimMissing_ShouldThrowUnauthorizedAccessException()
    {
        var sut = new IntakeSourcesController(_serviceMock.Object, BuildClaimsService(null));

        var act = () => sut.GetReport(LocationId, null, null, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static ClaimsService BuildClaimsService(string? tenantId)
    {
        var claims = tenantId is null ? new List<Claim>() : [new Claim(ClaimsService.TenantIdClaimType, tenantId)];
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new ClaimsService(accessor.Object);
    }
}
