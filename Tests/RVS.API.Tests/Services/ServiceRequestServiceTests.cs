using FluentAssertions;
using Moq;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class ServiceRequestServiceTests
{
    private readonly Mock<IServiceRequestRepository> _repoMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly ServiceRequestService _sut;

    public ServiceRequestServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _sut = new ServiceRequestService(_repoMock.Object, _userContextMock.Object);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetByIdAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetByIdAsync(tenantId!, "sr_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetByIdAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.GetByIdAsync("ten_1", id!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.GetByIdAsync("ten_1", "sr_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnServiceRequest()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var result = await _sut.GetByIdAsync("ten_1", sr.Id);

        result.Should().BeSameAs(sr);
    }

    // ── SearchAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SearchAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.SearchAsync(tenantId!, new ServiceRequestSearchRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.SearchAsync("ten_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SearchAsync_ShouldDelegateToRepository()
    {
        var request = new ServiceRequestSearchRequestDto { Status = "New" };
        var expected = new PagedResult<ServiceRequest> { Items = [BuildServiceRequest()] };
        _repoMock.Setup(r => r.SearchAsync("ten_1", request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.SearchAsync("ten_1", request);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task SearchAsync_WithContinuationToken_ShouldPassTokenToRepository()
    {
        var request = new ServiceRequestSearchRequestDto();
        var expected = new PagedResult<ServiceRequest>();
        _repoMock.Setup(r => r.SearchAsync("ten_1", request, "token123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.SearchAsync("ten_1", request, "token123");

        result.Should().BeSameAs(expected);
        _repoMock.Verify(r => r.SearchAsync("ten_1", request, "token123", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.CreateAsync(tenantId!, BuildServiceRequest());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.CreateAsync("ten_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_ShouldDelegateToRepository()
    {
        var entity = BuildServiceRequest();
        _repoMock.Setup(r => r.CreateAsync(entity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.CreateAsync("ten_1", entity);

        result.Should().BeSameAs(entity);
        _repoMock.Verify(r => r.CreateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.UpdateAsync(tenantId!, "sr_1", BuildUpdateRequest());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.UpdateAsync("ten_1", id!, BuildUpdateRequest());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.UpdateAsync("ten_1", "sr_1", (ServiceRequestUpdateRequestDto)null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.UpdateAsync("ten_1", "sr_missing", BuildUpdateRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenStatusTransitionIsInvalid_ShouldThrowArgumentException()
    {
        var existing = BuildServiceRequest();
        existing.Status = "New";

        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = BuildUpdateRequest() with { Status = "InvalidStatus" };

        var act = () => _sut.UpdateAsync("ten_1", existing.Id, request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid status transition*");
    }

    [Fact]
    public async Task UpdateAsync_WhenOptimisticConcurrencyConflict_ShouldThrowArgumentException()
    {
        var existing = BuildServiceRequest();
        existing.MarkAsUpdated("usr_other");
        var staleTimestamp = existing.UpdatedAtUtc!.Value.AddMinutes(-5);

        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = BuildUpdateRequest() with
        {
            Status = existing.Status,
            UpdatedAtUtc = staleTimestamp
        };

        var act = () => _sut.UpdateAsync("ten_1", existing.Id, request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*concurrency conflict*");
    }

    [Fact]
    public async Task UpdateAsync_WithValidTransition_ShouldUpdateAndCallMarkAsUpdated()
    {
        var existing = BuildServiceRequest();
        existing.Status = "New";

        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var request = new ServiceRequestUpdateRequestDto
        {
            Status = "InProgress",
            IssueDescription = "Updated description",
            Priority = "Low"
        };

        var result = await _sut.UpdateAsync("ten_1", existing.Id, request);

        result.Status.Should().Be("InProgress");
        result.IssueDescription.Should().Be("Updated description");
        result.Priority.Should().Be("Low");
        result.UpdatedByUserId.Should().Be("usr_test");
        result.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_SameStatus_ShouldNotValidateTransition()
    {
        var existing = BuildServiceRequest();
        existing.Status = "New";

        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var request = BuildUpdateRequest() with { Status = "New" };

        var result = await _sut.UpdateAsync("ten_1", existing.Id, request);

        result.Status.Should().Be("New");
    }

    // ── UpdateStatusAsync ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateStatusAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.UpdateStatusAsync(tenantId!, "sr_1", "InProgress");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateStatusAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.UpdateStatusAsync("ten_1", id!, "InProgress");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateStatusAsync_WhenNewStatusIsNullOrWhiteSpace_ShouldThrowArgumentException(string? newStatus)
    {
        var act = () => _sut.UpdateStatusAsync("ten_1", "sr_1", newStatus!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.UpdateStatusAsync("ten_1", "sr_missing", "InProgress");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenInvalidTransition_ShouldThrowArgumentException()
    {
        var existing = BuildServiceRequest();
        existing.Status = "New";
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var act = () => _sut.UpdateStatusAsync("ten_1", existing.Id, "InvalidStatus");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid status transition*");
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenValidTransition_ShouldUpdateStatus()
    {
        var existing = BuildServiceRequest();
        existing.Status = "New";
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var result = await _sut.UpdateStatusAsync("ten_1", existing.Id, "InProgress");

        result.Status.Should().Be("InProgress");
        result.UpdatedByUserId.Should().Be("usr_test");
    }

    // ── BatchOutcomeAsync ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task BatchOutcomeAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var request = new BatchOutcomeRequestDto { ServiceRequestIds = ["sr_1"] };

        var act = () => _sut.BatchOutcomeAsync(tenantId!, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task BatchOutcomeAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.BatchOutcomeAsync("ten_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BatchOutcomeAsync_WhenBatchExceeds25_ShouldThrowArgumentException()
    {
        var ids = Enumerable.Range(1, 26).Select(i => $"sr_{i}").ToList();
        var request = new BatchOutcomeRequestDto { ServiceRequestIds = ids };

        var act = () => _sut.BatchOutcomeAsync("ten_1", request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds maximum of 25*");
    }

    [Fact]
    public async Task BatchOutcomeAsync_WhenBatchIsEmpty_ShouldThrowArgumentException()
    {
        var request = new BatchOutcomeRequestDto { ServiceRequestIds = [] };

        var act = () => _sut.BatchOutcomeAsync("ten_1", request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least one*");
    }

    [Fact]
    public async Task BatchOutcomeAsync_WhenAllExist_ShouldSucceed()
    {
        var sr1 = BuildServiceRequest("sr_1", "ten_1");
        var sr2 = BuildServiceRequest("sr_2", "ten_1");
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_1", It.IsAny<CancellationToken>())).ReturnsAsync(sr1);
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_2", It.IsAny<CancellationToken>())).ReturnsAsync(sr2);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var request = new BatchOutcomeRequestDto
        {
            ServiceRequestIds = ["sr_1", "sr_2"],
            FailureMode = "Leak",
            RepairAction = "Sealed",
            LaborHours = 2.5m
        };

        var result = await _sut.BatchOutcomeAsync("ten_1", request);

        result.Succeeded.Should().HaveCount(2);
        result.Failed.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchOutcomeAsync_WhenSomeNotFound_ShouldReportFailures()
    {
        var sr1 = BuildServiceRequest("sr_1", "ten_1");
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_1", It.IsAny<CancellationToken>())).ReturnsAsync(sr1);
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>())).ReturnsAsync((ServiceRequest?)null);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var request = new BatchOutcomeRequestDto
        {
            ServiceRequestIds = ["sr_1", "sr_missing"],
            RepairAction = "Replaced"
        };

        var result = await _sut.BatchOutcomeAsync("ten_1", request);

        result.Succeeded.Should().HaveCount(1).And.Contain("sr_1");
        result.Failed.Should().HaveCount(1);
        result.Failed[0].ServiceRequestId.Should().Be("sr_missing");
    }

    [Fact]
    public async Task BatchOutcomeAsync_ShouldApplyOutcomeFieldsToServiceEvent()
    {
        var sr = BuildServiceRequest("sr_1", "ten_1");
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_1", It.IsAny<CancellationToken>())).ReturnsAsync(sr);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var request = new BatchOutcomeRequestDto
        {
            ServiceRequestIds = ["sr_1"],
            FailureMode = "Corrosion",
            RepairAction = "Replaced pipe",
            PartsUsed = ["Pipe", "Sealant"],
            LaborHours = 3.0m
        };

        await _sut.BatchOutcomeAsync("ten_1", request);

        _repoMock.Verify(r => r.UpdateAsync(
            It.Is<ServiceRequest>(e =>
                e.ServiceEvent != null &&
                e.ServiceEvent.FailureMode == "Corrosion" &&
                e.ServiceEvent.RepairAction == "Replaced pipe" &&
                e.ServiceEvent.PartsUsed.Contains("Pipe") &&
                e.ServiceEvent.LaborHours == 3.0m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DeleteAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.DeleteAsync(tenantId!, "sr_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DeleteAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.DeleteAsync("ten_1", id!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.DeleteAsync("ten_1", "sr_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_ShouldDelegateToRepository()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        await _sut.DeleteAsync("ten_1", sr.Id);

        _repoMock.Verify(r => r.DeleteAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ServiceRequest BuildServiceRequest(string? id = null, string tenantId = "ten_1") => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        TenantId = tenantId,
        Status = "New",
        LocationId = "loc_slc",
        CustomerProfileId = "cp_1",
        IssueDescription = "Water heater not working",
        IssueCategory = "Plumbing",
        Priority = "High"
    };

    private static ServiceRequestUpdateRequestDto BuildUpdateRequest() => new()
    {
        Status = "New",
        IssueDescription = "Water heater not working",
        IssueCategory = "Plumbing",
        Priority = "High"
    };
}
