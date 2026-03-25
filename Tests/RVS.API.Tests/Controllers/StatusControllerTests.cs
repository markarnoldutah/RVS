using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

public class StatusControllerTests
{
    private readonly Mock<IGlobalCustomerAcctService> _globalAcctServiceMock = new();
    private readonly Mock<ICustomerProfileService> _profileServiceMock = new();
    private readonly Mock<IServiceRequestService> _srServiceMock = new();
    private readonly StatusController _sut;

    public StatusControllerTests()
    {
        _sut = new StatusController(
            _globalAcctServiceMock.Object,
            _profileServiceMock.Object,
            _srServiceMock.Object);
    }

    [Fact]
    public async Task GetStatus_WithValidToken_ShouldReturnOkWithCustomerStatus()
    {
        var acct = BuildGlobalCustomerAcct();
        _globalAcctServiceMock.Setup(s => s.ValidateMagicLinkTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(acct);

        var profile = BuildCustomerProfile();
        _profileServiceMock.Setup(s => s.GetByIdAsync("ten_1", "prof_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var sr = BuildServiceRequest();
        _srServiceMock.Setup(s => s.GetByIdAsync("ten_1", "sr_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var result = await _sut.GetStatus("valid-token", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CustomerStatusResponseDto>().Subject;
        dto.FirstName.Should().Be("Jane");
        dto.ServiceRequests.Should().HaveCount(1);
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
        dto.FirstName.Should().Be("Jane");
        dto.ServiceRequests.Should().BeEmpty();
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
}
