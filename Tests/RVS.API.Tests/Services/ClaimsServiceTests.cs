using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using RVS.API.Services;

namespace RVS.API.Tests.Services;

public sealed class ClaimsServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();

    private ClaimsService CreateService(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
        return new ClaimsService(_httpContextAccessorMock.Object);
    }

    [Fact]
    public void GetTenantIdOrThrow_WhenClaimPresent_ShouldReturnTenantId()
    {
        var sut = CreateService(new Claim(ClaimsService.TenantIdClaimType, "tenant-123"));

        var result = sut.GetTenantIdOrThrow();

        result.Should().Be("tenant-123");
    }

    [Fact]
    public void GetTenantIdOrThrow_WhenClaimMissing_ShouldThrowUnauthorizedAccessException()
    {
        var sut = CreateService();

        var act = () => sut.GetTenantIdOrThrow();

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("Tenant identifier is missing.");
    }

    [Fact]
    public void GetLocationIdsOrThrow_WhenClaimPresent_ShouldReturnLocationIds()
    {
        var sut = CreateService(new Claim(ClaimsService.LocationIdsClaimType, "[\"loc_slc\",\"loc_denver\"]"));

        var result = sut.GetLocationIdsOrThrow();

        result.Should().HaveCount(2);
        result.Should().Contain("loc_slc");
        result.Should().Contain("loc_denver");
    }

    [Fact]
    public void GetLocationIdsOrThrow_WhenClaimMissing_ShouldThrowUnauthorizedAccessException()
    {
        var sut = CreateService();

        var act = () => sut.GetLocationIdsOrThrow();

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("locationIds claim is missing.");
    }

    [Fact]
    public void GetLocationIdsOrThrow_WhenClaimEmpty_ShouldThrowUnauthorizedAccessException()
    {
        var sut = CreateService(new Claim(ClaimsService.LocationIdsClaimType, ""));

        var act = () => sut.GetLocationIdsOrThrow();

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("locationIds claim is missing.");
    }

    [Fact]
    public void GetLocationIdsOrThrow_WhenClaimWhitespace_ShouldThrowUnauthorizedAccessException()
    {
        var sut = CreateService(new Claim(ClaimsService.LocationIdsClaimType, "   "));

        var act = () => sut.GetLocationIdsOrThrow();

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("locationIds claim is missing.");
    }

    [Fact]
    public void GetLocationIdsOrThrow_WhenEmptyJsonArray_ShouldReturnEmptyList()
    {
        var sut = CreateService(new Claim(ClaimsService.LocationIdsClaimType, "[]"));

        var result = sut.GetLocationIdsOrThrow();

        result.Should().BeEmpty();
    }

    [Fact]
    public void HasAccessToLocation_WhenLocationInList_ShouldReturnTrue()
    {
        var sut = CreateService(new Claim(ClaimsService.LocationIdsClaimType, "[\"loc_slc\",\"loc_denver\"]"));

        var result = sut.HasAccessToLocation("loc_slc");

        result.Should().BeTrue();
    }

    [Fact]
    public void HasAccessToLocation_WhenLocationNotInList_ShouldReturnFalse()
    {
        var sut = CreateService(new Claim(ClaimsService.LocationIdsClaimType, "[\"loc_slc\",\"loc_denver\"]"));

        var result = sut.HasAccessToLocation("loc_provo");

        result.Should().BeFalse();
    }

    [Fact]
    public void HasAccessToLocation_ShouldBeCaseInsensitive()
    {
        var sut = CreateService(new Claim(ClaimsService.LocationIdsClaimType, "[\"LOC_SLC\"]"));

        var result = sut.HasAccessToLocation("loc_slc");

        result.Should().BeTrue();
    }

    [Fact]
    public void HasAccessToLocation_WhenClaimMissing_ShouldThrowUnauthorizedAccessException()
    {
        var sut = CreateService();

        var act = () => sut.HasAccessToLocation("loc_slc");

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("locationIds claim is missing.");
    }
}
