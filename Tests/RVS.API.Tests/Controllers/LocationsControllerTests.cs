using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using RVS.API.Controllers;
using RVS.API.Options;
using MsOptions = Microsoft.Extensions.Options.Options;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

public class LocationsControllerTests
{
    private readonly Mock<ILocationService> _serviceMock = new();
    private readonly ClaimsService _claimsService;
    private readonly LocationsController _sut;

    private const string TenantId = "ten_test";

    private const string IntakeBaseUrl = "https://rvintake.com";

    public LocationsControllerTests()
    {
        _claimsService = BuildClaimsService(TenantId);
        var intakeUrlOptions = MsOptions.Create(new IntakeUrlOptions { BaseUrl = IntakeBaseUrl });
        _sut = new LocationsController(_serviceMock.Object, _claimsService, intakeUrlOptions);
    }

    [Fact]
    public async Task List_ShouldReturnOkWithSummaryDtos()
    {
        var locations = new List<Location>
        {
            BuildLocation("loc_1", "Salt Lake"),
            BuildLocation("loc_2", "Provo")
        };
        _serviceMock.Setup(s => s.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);

        var result = await _sut.List(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<List<LocationSummaryResponseDto>>().Subject;
        dtos.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_WhenExists_ShouldReturnOkWithDetailDto()
    {
        var location = BuildLocation();
        _serviceMock.Setup(s => s.GetByIdAsync(TenantId, location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var result = await _sut.GetById(location.Id, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<LocationDetailDto>().Subject;
        dto.Id.Should().Be(location.Id);
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtActionWithDetailDto()
    {
        var location = BuildLocation();
        _serviceMock.Setup(s => s.CreateAsync(TenantId, It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var request = new LocationCreateRequestDto { Name = "New Location", Slug = "new-location" };
        var result = await _sut.Create(request, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<LocationDetailDto>().Subject;
        dto.Id.Should().Be(location.Id);
    }

    [Fact]
    public async Task Update_ShouldReturnOkWithDetailDto()
    {
        var location = BuildLocation();
        _serviceMock.Setup(s => s.GetByIdAsync(TenantId, location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _serviceMock.Setup(s => s.UpdateAsync(TenantId, location.Id, It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var request = new LocationCreateRequestDto { Name = "Updated", Slug = "updated" };
        var result = await _sut.Update(location.Id, request, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<LocationDetailDto>();
    }

    [Fact]
    public async Task GetQrCode_ShouldReturnOkWithConfiguredBaseUrlAndIntakePath()
    {
        var location = BuildLocation();
        _serviceMock.Setup(s => s.GetByIdAsync(TenantId, location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var result = await _sut.GetQrCode(location.Id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().NotBeNull().And.Subject;
        var intakeUrlProp = payload!.GetType().GetProperty("intakeUrl")!.GetValue(payload) as string;
        intakeUrlProp.Should().Be($"{IntakeBaseUrl}/{location.Slug}");
    }

    [Fact]
    public async Task GetQrCode_ShouldTrimTrailingSlashOnConfiguredBaseUrl()
    {
        var location = BuildLocation();
        _serviceMock.Setup(s => s.GetByIdAsync(TenantId, location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var optionsWithSlash = MsOptions.Create(new IntakeUrlOptions { BaseUrl = "https://rvintake.com/" });
        var sut = new LocationsController(_serviceMock.Object, _claimsService, optionsWithSlash);

        var result = await sut.GetQrCode(location.Id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value!;
        var intakeUrlProp = payload.GetType().GetProperty("intakeUrl")!.GetValue(payload) as string;
        intakeUrlProp.Should().Be($"https://rvintake.com/{location.Slug}");
    }

    private static Location BuildLocation(string id = "loc_test", string name = "Test Location") => new()
    {
        Id = id,
        TenantId = TenantId,
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        Address = new AddressEmbedded(),
        IntakeConfig = new IntakeFormConfigEmbedded(),
        CreatedByUserId = "usr_test"
    };

    private static ClaimsService BuildClaimsService(string tenantId)
    {
        var claims = new List<Claim>
        {
            new(ClaimsService.TenantIdClaimType, tenantId),
            new(ClaimTypes.NameIdentifier, "usr_test")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new ClaimsService(accessor.Object);
    }
}
