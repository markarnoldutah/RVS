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

public class ServiceRequestsControllerTests
{
    private readonly Mock<IServiceRequestService> _serviceMock = new();
    private readonly ClaimsService _claimsService;
    private readonly ServiceRequestsController _sut;

    private const string TenantId = "ten_test";

    public ServiceRequestsControllerTests()
    {
        _claimsService = BuildClaimsService(TenantId);
        _sut = new ServiceRequestsController(_serviceMock.Object, _claimsService);
    }

    [Fact]
    public async Task GetById_WhenExists_ShouldReturnOkWithDetailDto()
    {
        var sr = BuildServiceRequest();
        _serviceMock.Setup(s => s.GetByIdAsync(TenantId, sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var result = await _sut.GetById("dlr_1", sr.Id, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<ServiceRequestDetailResponseDto>().Subject;
        dto.Id.Should().Be(sr.Id);
    }

    [Fact]
    public async Task Search_ShouldReturnOkWithPagedResult()
    {
        var pagedResult = new PagedResult<ServiceRequest>
        {
            Page = 1,
            PageSize = 25,
            TotalCount = 1,
            Items = [BuildServiceRequest()]
        };

        _serviceMock.Setup(s => s.SearchAsync(TenantId, It.IsAny<ServiceRequestSearchRequestDto>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var request = new ServiceRequestSearchRequestDto();
        var result = await _sut.Search("dlr_1", request, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeAssignableTo<PagedResult<ServiceRequestSummaryResponseDto>>().Subject;
        dto.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Update_ShouldReturnOkWithDetailDto()
    {
        var sr = BuildServiceRequest();
        var request = new ServiceRequestUpdateRequestDto
        {
            Status = sr.Status,
            IssueDescription = sr.IssueDescription,
            Priority = sr.Priority
        };
        _serviceMock.Setup(s => s.UpdateAsync(TenantId, sr.Id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var result = await _sut.Update("dlr_1", sr.Id, request, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<ServiceRequestDetailResponseDto>();
    }

    [Fact]
    public async Task BatchOutcome_ShouldReturnOkWithResponse()
    {
        var request = new BatchOutcomeRequestDto { ServiceRequestIds = ["sr_1"], FailureMode = "Electrical" };
        var response = new BatchOutcomeResponseDto { Succeeded = ["sr_1"] };
        _serviceMock.Setup(s => s.BatchOutcomeAsync(TenantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.BatchOutcome("dlr_1", request, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<BatchOutcomeResponseDto>().Subject;
        dto.Succeeded.Should().Contain("sr_1");
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent()
    {
        _serviceMock.Setup(s => s.DeleteAsync(TenantId, "sr_1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Delete("dlr_1", "sr_1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    private static ServiceRequest BuildServiceRequest() => new()
    {
        Id = "sr_test_1",
        TenantId = TenantId,
        LocationId = "loc_1",
        Status = "New",
        IssueCategory = "Electrical",
        IssueDescription = "Battery not charging",
        CreatedByUserId = "intake",
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com"
        },
        AssetInfo = new AssetInfoEmbedded
        {
            AssetId = "RV:1FTFW1ET5EKE12345",
            Manufacturer = "Thor",
            Model = "Ace",
            Year = 2023
        }
    };

    private static ClaimsService BuildClaimsService(string tenantId)
    {
        var claims = new List<Claim>
        {
            new(ClaimsService.TenantIdClaimType, tenantId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        return new ClaimsService(accessor.Object);
    }
}
