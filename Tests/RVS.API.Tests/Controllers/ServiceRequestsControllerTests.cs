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
        var wrapper = okResult.Value.Should().BeOfType<ServiceRequestSearchResultResponseDto>().Subject;
        wrapper.Results.TotalCount.Should().Be(1);
        wrapper.Results.Items.Should().ContainSingle();
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
    public async Task Delete_ShouldReturnNoContent()
    {
        _serviceMock.Setup(s => s.DeleteAsync(TenantId, "sr_1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Delete("dlr_1", "sr_1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RegeneratePacket_ShouldReturnAcceptedAndDelegateWithTenantFromClaims()
    {
        _serviceMock.Setup(s => s.RegeneratePacketAsync(TenantId, "sr_1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.RegeneratePacket("dlr_1", "sr_1", CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
        _serviceMock.Verify(s => s.RegeneratePacketAsync(TenantId, "sr_1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetStatusNote_WithValidNote_ShouldReturnOkAndDelegateWithTenantFromClaims()
    {
        var sr = BuildServiceRequest();
        sr.SetCustomerStatusNote("Waiting on a back-ordered slide motor, ETA Friday.", "usr_mgr");
        _serviceMock.Setup(s => s.SetCustomerStatusNoteAsync(
                TenantId, sr.Id, "Waiting on a back-ordered slide motor, ETA Friday.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var request = new ServiceRequestStatusNoteRequestDto { Note = "Waiting on a back-ordered slide motor, ETA Friday." };
        var result = await _sut.SetStatusNote("dlr_1", sr.Id, request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ServiceRequestDetailResponseDto>().Subject;
        dto.CustomerStatusNote.Should().NotBeNull();
        dto.CustomerStatusNote!.Text.Should().Be("Waiting on a back-ordered slide motor, ETA Friday.");
        _serviceMock.Verify(s => s.SetCustomerStatusNoteAsync(
            TenantId, sr.Id, "Waiting on a back-ordered slide motor, ETA Friday.", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetStatusNote_WithNullNote_ShouldReturnOkAndClear()
    {
        var sr = BuildServiceRequest();
        _serviceMock.Setup(s => s.SetCustomerStatusNoteAsync(TenantId, sr.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var result = await _sut.SetStatusNote("dlr_1", sr.Id, new ServiceRequestStatusNoteRequestDto { Note = null }, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ServiceRequestDetailResponseDto>()
            .Which.CustomerStatusNote.Should().BeNull();
    }

    [Fact]
    public async Task SetStatusNote_WhenNoteTooLong_ShouldReturn422AndNotCallService()
    {
        var request = new ServiceRequestStatusNoteRequestDto { Note = new string('a', 281) };

        var result = await _sut.SetStatusNote("dlr_1", "sr_1", request, CancellationToken.None);

        result.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        _serviceMock.Verify(s => s.SetCustomerStatusNoteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetStatusNote_WhenNoteHasBlockedCharacter_ShouldReturn422()
    {
        var request = new ServiceRequestStatusNoteRequestDto { Note = "waiting on <part>" };

        var result = await _sut.SetStatusNote("dlr_1", "sr_1", request, CancellationToken.None);

        result.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    // ── SetDisposition (Spec C-4) ────────────────────────────────────────────

    [Fact]
    public async Task SetDisposition_WithKnownReason_ShouldReturnOkAndDelegateWithTenantFromClaims()
    {
        var sr = BuildServiceRequest();
        sr.CloseWithDisposition("Duplicate", "usr_mgr");
        _serviceMock.Setup(s => s.CloseWithDispositionAsync(TenantId, sr.Id, "Duplicate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var request = new ServiceRequestDispositionRequestDto { ReasonCode = "Duplicate" };
        var result = await _sut.SetDisposition("dlr_1", sr.Id, request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ServiceRequestDetailResponseDto>().Subject;
        dto.Status.Should().Be("Cancelled");
        dto.Disposition.Should().NotBeNull();
        dto.Disposition!.ReasonCode.Should().Be("Duplicate");
        _serviceMock.Verify(s => s.CloseWithDispositionAsync(TenantId, sr.Id, "Duplicate", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Other")]
    [InlineData("spam")]
    public async Task SetDisposition_WhenReasonUnknownOrBlank_ShouldReturn422AndNotCallService(string? reasonCode)
    {
        var request = new ServiceRequestDispositionRequestDto { ReasonCode = reasonCode! };

        var result = await _sut.SetDisposition("dlr_1", "sr_1", request, CancellationToken.None);

        result.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        _serviceMock.Verify(s => s.CloseWithDispositionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
            AssetId = "1FTFW1ET5EKE12345",
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
