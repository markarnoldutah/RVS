using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

public class DealershipsControllerTests
{
    private readonly Mock<IDealershipService> _serviceMock = new();
    private readonly ClaimsService _claimsService;
    private readonly DealershipsController _sut;

    private const string TenantId = "ten_test";

    public DealershipsControllerTests()
    {
        _claimsService = BuildClaimsService(TenantId);
        _sut = new DealershipsController(_serviceMock.Object, _claimsService);
    }

    [Fact]
    public async Task List_ShouldReturnOkWithSummaryDtos()
    {
        var dealerships = new List<Dealership>
        {
            BuildDealership("dlr_1", "Camping World"),
            BuildDealership("dlr_2", "Blue Compass RV")
        };
        _serviceMock.Setup(s => s.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dealerships);

        var result = await _sut.List(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<List<DealershipSummaryDto>>().Subject;
        dtos.Should().HaveCount(2);
        dtos[0].Name.Should().Be("Camping World");
    }

    [Fact]
    public async Task GetById_WhenExists_ShouldReturnOkWithDetailDto()
    {
        var dealership = BuildDealership();
        _serviceMock.Setup(s => s.GetByIdAsync(TenantId, dealership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dealership);

        var result = await _sut.GetById(dealership.Id, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DealershipDetailDto>().Subject;
        dto.Id.Should().Be(dealership.Id);
        dto.Name.Should().Be("Test Dealership");
    }

    [Fact]
    public async Task Update_ShouldReturnOkWithDetailDto()
    {
        var dealership = BuildDealership();
        var request = new DealershipUpdateRequestDto
        {
            Name = dealership.Name,
            Slug = dealership.Slug,
            Phone = dealership.Phone
        };
        _serviceMock.Setup(s => s.UpdateAsync(TenantId, dealership.Id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dealership);

        var result = await _sut.Update(dealership.Id, request, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<DealershipDetailDto>();
    }

    private static Dealership BuildDealership(string id = "dlr_test", string name = "Test Dealership") => new()
    {
        Id = id,
        TenantId = TenantId,
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        Phone = "(801) 555-1000",
        IntakeConfig = new IntakeFormConfigEmbedded()
    };

    private static ClaimsService BuildClaimsService(string tenantId)
    {
        var claims = new List<Claim> { new(ClaimsService.TenantIdClaimType, tenantId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new ClaimsService(accessor.Object);
    }
}
