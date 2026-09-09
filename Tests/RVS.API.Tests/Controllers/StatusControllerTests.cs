using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

public class StatusControllerTests
{
    private readonly Mock<IGlobalCustomerAcctService> _globalAcctServiceMock = new();
    private readonly Mock<ICustomerProfileService> _profileServiceMock = new();
    private readonly Mock<IServiceRequestService> _srServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();
    private readonly StatusController _sut;

    public StatusControllerTests()
    {
        _sut = new StatusController(
            _globalAcctServiceMock.Object,
            _profileServiceMock.Object,
            _srServiceMock.Object,
            _locationServiceMock.Object);
    }

    [Fact]
    public async Task GetStatus_WithValidToken_ShouldReturnOnlyUnitDateStatusAndLocationPhone()
    {
        ArrangeHappyPath();

        var result = await _sut.GetStatus("valid-token", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CustomerStatusResponseDto>().Subject;

        dto.ServiceRequests.Should().HaveCount(1);
        var item = dto.ServiceRequests[0];
        item.Unit.Should().Be("2023 Thor Ace");
        item.SubmittedAtUtc.Should().Be(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        item.Status.Should().Be("New");
        item.LocationPhone.Should().Be("555-0100");
    }

    [Fact]
    public async Task GetStatus_ShouldNotExposeCustomerIdentityOnTheResponse()
    {
        ArrangeHappyPath();

        var result = await _sut.GetStatus("valid-token", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CustomerStatusResponseDto>().Subject;

        // Spec X-1: nothing beyond the four permitted fields — no name, no issue text.
        typeof(CustomerStatusResponseDto).GetProperty("FirstName").Should().BeNull();
        var serialized = System.Text.Json.JsonSerializer.Serialize(dto);
        serialized.Should().NotContain("Jane");
        serialized.Should().NotContain("Battery not charging");
        serialized.Should().NotContain("Electrical");
    }

    [Fact]
    public async Task GetStatus_WhenServiceRequestHasStatusNote_ShouldIncludeItOnTheItem()
    {
        _globalAcctServiceMock.Setup(s => s.ValidateMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGlobalCustomerAcct());
        _profileServiceMock.Setup(s => s.GetByIdAsync("ten_1", "prof_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCustomerProfile());
        _locationServiceMock.Setup(s => s.GetByIdAsync("ten_1", "loc_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildLocation());
        var sr = BuildServiceRequest();
        sr.SetCustomerStatusNote("Slide motor on back order, ETA Friday.", "usr_mgr");
        _srServiceMock.Setup(s => s.GetByIdAsync("ten_1", "sr_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var result = await _sut.GetStatus("valid-token", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CustomerStatusResponseDto>().Subject;
        dto.ServiceRequests[0].StatusNote.Should().Be("Slide motor on back order, ETA Friday.");
    }

    [Fact]
    public async Task GetStatus_WhenServiceRequestHasNoStatusNote_ShouldReturnNullStatusNote()
    {
        ArrangeHappyPath();

        var result = await _sut.GetStatus("valid-token", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CustomerStatusResponseDto>().Subject;
        dto.ServiceRequests[0].StatusNote.Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_WhenLocationNotFound_ShouldReturnNullLocationPhone()
    {
        ArrangeHappyPath();
        _locationServiceMock.Setup(s => s.GetByIdAsync("ten_1", "loc_1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.GetStatus("valid-token", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CustomerStatusResponseDto>().Subject;
        dto.ServiceRequests[0].LocationPhone.Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_WhenLocationHasNoPhone_ShouldReturnNullLocationPhone()
    {
        ArrangeHappyPath();
        _locationServiceMock.Setup(s => s.GetByIdAsync("ten_1", "loc_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location { Id = "loc_1", TenantId = "ten_1", Slug = "slc", CreatedByUserId = "system", Phone = null });

        var result = await _sut.GetStatus("valid-token", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CustomerStatusResponseDto>().Subject;
        dto.ServiceRequests[0].LocationPhone.Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_WithNoLinkedProfiles_ShouldReturnEmptyServiceRequests()
    {
        var acct = new GlobalCustomerAcct
        {
            Id = "gca_1",
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            LinkedProfiles = [],
            CreatedByUserId = "system"
        };
        _globalAcctServiceMock.Setup(s => s.ValidateMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        var result = await _sut.GetStatus("valid-token", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CustomerStatusResponseDto>().Subject;
        dto.ServiceRequests.Should().BeEmpty();
    }

    private void ArrangeHappyPath()
    {
        _globalAcctServiceMock.Setup(s => s.ValidateMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGlobalCustomerAcct());
        _profileServiceMock.Setup(s => s.GetByIdAsync("ten_1", "prof_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCustomerProfile());
        _srServiceMock.Setup(s => s.GetByIdAsync("ten_1", "sr_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildServiceRequest());
        _locationServiceMock.Setup(s => s.GetByIdAsync("ten_1", "loc_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildLocation());
    }

    private static GlobalCustomerAcct BuildGlobalCustomerAcct() => new()
    {
        Id = "gca_1",
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        MagicLinkToken = "valid-token",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        CreatedByUserId = "system",
        LinkedProfiles =
        [
            new LinkedProfileEmbedded
            {
                TenantId = "ten_1",
                ProfileId = "prof_1",
                DealershipName = "Test Dealer",
                FirstSeenAtUtc = DateTime.UtcNow,
                RequestCount = 1
            }
        ]
    };

    private static CustomerProfile BuildCustomerProfile() => new()
    {
        Id = "prof_1",
        TenantId = "ten_1",
        Email = "jane@example.com",
        FirstName = "Jane",
        LastName = "Doe",
        GlobalCustomerAcctId = "gca_1",
        CreatedByUserId = "intake",
        ServiceRequestIds = ["sr_1"],
        TotalRequestCount = 1
    };

    private static ServiceRequest BuildServiceRequest() => new()
    {
        Id = "sr_1",
        TenantId = "ten_1",
        LocationId = "loc_1",
        Status = "New",
        IssueCategory = "Electrical",
        IssueDescription = "Battery not charging",
        CreatedByUserId = "intake",
        CreatedAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
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

    private static Location BuildLocation() => new()
    {
        Id = "loc_1",
        TenantId = "ten_1",
        Slug = "salt-lake-service-center",
        CreatedByUserId = "system",
        Phone = "555-0100",
        Address = new AddressEmbedded
        {
            City = "Salt Lake City",
            State = "UT"
        }
    };
}
