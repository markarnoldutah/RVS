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
    private readonly Mock<IPacketGenerationService> _packetGenerationMock = new();
    private readonly ServiceRequestService _sut;

    public ServiceRequestServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _sut = new ServiceRequestService(_repoMock.Object, _userContextMock.Object, _packetGenerationMock.Object);
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

    // ── GetSubmissionMembersAsync ────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetSubmissionMembersAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetSubmissionMembersAsync(tenantId!, "sr_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetSubmissionMembersAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.GetSubmissionMembersAsync("ten_1", id!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetSubmissionMembersAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.GetSubmissionMembersAsync("ten_1", "sr_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetSubmissionMembersAsync_WhenReportedOnItsOwn_ShouldReturnOnlyTheRequest()
    {
        var sr = BuildServiceRequest("sr_1");
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var result = await _sut.GetSubmissionMembersAsync("ten_1", "sr_1");

        result.Should().ContainSingle().Which.Should().BeSameAs(sr);
        _repoMock.Verify(r => r.GetBySubmissionIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSubmissionMembersAsync_WhenInMultiIssueSubmission_ShouldReturnEveryMemberInOrder()
    {
        var lead = BuildSubmissionMember("sr_lead", 1);
        var second = BuildSubmissionMember("sr_2", 2);
        var third = BuildSubmissionMember("sr_3", 3);
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(second);
        _repoMock.Setup(r => r.GetBySubmissionIdAsync("ten_1", "sr_lead", It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead, second, third]);

        var result = await _sut.GetSubmissionMembersAsync("ten_1", "sr_2");

        result.Select(r => r.Id).Should().Equal("sr_lead", "sr_2", "sr_3");
    }

    [Fact]
    public async Task GetSubmissionMembersAsync_WhenSubmissionReadsBackEmpty_ShouldReturnOnlyTheRequest()
    {
        var second = BuildSubmissionMember("sr_2", 2);
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(second);
        _repoMock.Setup(r => r.GetBySubmissionIdAsync("ten_1", "sr_lead", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetSubmissionMembersAsync("ten_1", "sr_2");

        result.Should().ContainSingle().Which.Should().BeSameAs(second);
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

    // ── RegeneratePacketAsync ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task RegeneratePacketAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.RegeneratePacketAsync(tenantId!, "sr_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task RegeneratePacketAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.RegeneratePacketAsync("ten_1", id!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RegeneratePacketAsync_ShouldDelegateToPacketGenerationService()
    {
        await _sut.RegeneratePacketAsync("ten_1", "sr_42");

        _packetGenerationMock.Verify(
            p => p.RequestRegenerationAsync("ten_1", "sr_42", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetPacketPdfLinkAsync (Spec C-2, issue #443) ─────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetPacketPdfLinkAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetPacketPdfLinkAsync(tenantId!, "sr_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetPacketPdfLinkAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.GetPacketPdfLinkAsync("ten_1", id!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPacketPdfLinkAsync_ShouldDelegateToPacketGenerationService()
    {
        var link = new PacketPdfLinkDto { SasUrl = "https://blob/p.pdf", PacketVersion = 1 };
        _packetGenerationMock.Setup(p => p.GetPdfLinkAsync("ten_1", "sr_42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var result = await _sut.GetPacketPdfLinkAsync("ten_1", "sr_42");

        result.Should().Be(link);
    }

    // ── SetCustomerStatusNoteAsync (Spec C-9) ────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SetCustomerStatusNoteAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.SetCustomerStatusNoteAsync(tenantId!, "sr_1", "note");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SetCustomerStatusNoteAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.SetCustomerStatusNoteAsync("ten_1", id!, "note");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetCustomerStatusNoteAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.SetCustomerStatusNoteAsync("ten_1", "sr_missing", "note");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SetCustomerStatusNoteAsync_WhenNoteExceedsMaxLength_ShouldThrowArgumentException()
    {
        var existing = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var act = () => _sut.SetCustomerStatusNoteAsync("ten_1", existing.Id, new string('a', 281));

        await act.Should().ThrowAsync<ArgumentException>();
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetCustomerStatusNoteAsync_WhenNoteHasBlockedCharacter_ShouldThrowArgumentException()
    {
        var existing = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var act = () => _sut.SetCustomerStatusNoteAsync("ten_1", existing.Id, "waiting on <part>");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetCustomerStatusNoteAsync_WhenNoteIsInvalid_ShouldNotLeakNoteTextInExceptionMessage()
    {
        var existing = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var secret = "SECRET-CUSTOMER-DETAIL-" + new string('a', 280);

        var thrown = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.SetCustomerStatusNoteAsync("ten_1", existing.Id, secret));

        thrown.Message.Should().NotContain("SECRET-CUSTOMER-DETAIL");
    }

    [Fact]
    public async Task SetCustomerStatusNoteAsync_WithValidNote_ShouldPersistTrimmedNoteWithAuditIdentity()
    {
        var existing = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var result = await _sut.SetCustomerStatusNoteAsync(
            "ten_1", existing.Id, "  Waiting on a back-ordered slide motor, ETA Friday.  ");

        result.CustomerStatusNote.Should().NotBeNull();
        result.CustomerStatusNote!.Text.Should().Be("Waiting on a back-ordered slide motor, ETA Friday.");
        result.CustomerStatusNote.UpdatedByUserId.Should().Be("usr_test");
        result.UpdatedByUserId.Should().Be("usr_test");
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetCustomerStatusNoteAsync_WithBlankNote_ShouldClearExistingNoteAndPersist(string? note)
    {
        var existing = BuildServiceRequest();
        existing.SetCustomerStatusNote("An earlier note.", "usr_prev");
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var result = await _sut.SetCustomerStatusNoteAsync("ten_1", existing.Id, note);

        result.CustomerStatusNote.Should().BeNull();
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── CloseWithDispositionAsync (Spec C-4) ─────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CloseWithDispositionAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.CloseWithDispositionAsync(tenantId!, "sr_1", "Duplicate");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CloseWithDispositionAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.CloseWithDispositionAsync("ten_1", id!, "Duplicate");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Other")]
    [InlineData("duplicate")]
    public async Task CloseWithDispositionAsync_WhenReasonCodeIsUnknownOrBlank_ShouldThrowArgumentExceptionWithoutWriting(string? reasonCode)
    {
        var act = () => _sut.CloseWithDispositionAsync("ten_1", "sr_1", reasonCode!);

        await act.Should().ThrowAsync<ArgumentException>();
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloseWithDispositionAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.CloseWithDispositionAsync("ten_1", "sr_missing", "Spam");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("New")]
    [InlineData("InProgress")]
    [InlineData("WaitingOnParts")]
    [InlineData("WaitingOnCustomer")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task CloseWithDispositionAsync_FromAnyStatus_ShouldCancelAndStoreReasonWithAuditIdentity(string fromStatus)
    {
        var existing = BuildServiceRequest();
        existing.Status = fromStatus;
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var result = await _sut.CloseWithDispositionAsync("ten_1", existing.Id, "WrongLocation");

        result.Status.Should().Be("Cancelled");
        result.Disposition.Should().NotBeNull();
        result.Disposition!.ReasonCode.Should().Be("WrongLocation");
        result.Disposition.DisposedByUserId.Should().Be("usr_test");
        result.UpdatedByUserId.Should().Be("usr_test");
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenReopeningADisposedRequest_ShouldClearDisposition()
    {
        var existing = BuildServiceRequest();
        existing.CloseWithDisposition("Duplicate", "usr_prev");
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var result = await _sut.UpdateAsync("ten_1", existing.Id, BuildUpdateRequest() with { Status = "InProgress" });

        result.Status.Should().Be("InProgress");
        result.Disposition.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenDisposedRequestStaysCancelled_ShouldKeepDisposition()
    {
        var existing = BuildServiceRequest();
        existing.CloseWithDisposition("Spam", "usr_prev");
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var result = await _sut.UpdateAsync("ten_1", existing.Id, BuildUpdateRequest() with { Status = "Cancelled" });

        result.Disposition!.ReasonCode.Should().Be("Spam");
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenReopeningADisposedRequest_ShouldClearDisposition()
    {
        var existing = BuildServiceRequest();
        existing.CloseWithDisposition("CustomerWithdrew", "usr_prev");
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var result = await _sut.UpdateStatusAsync("ten_1", existing.Id, "New");

        result.Disposition.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // ── Job type (Spec C-11, issue #843) ─────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Remote")]
    [InlineData("OnSite")]
    [InlineData("InShop")]
    [InlineData("Inspection")]
    [InlineData("Install")]
    [InlineData("Other")]
    public async Task UpdateAsync_WhenJobTypeNullOrValid_ShouldSaveIt(string? jobType)
    {
        var existing = BuildServiceRequest();
        existing.JobType = "Remote";
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var result = await _sut.UpdateAsync("ten_1", existing.Id, BuildUpdateRequest() with { JobType = jobType });

        result.JobType.Should().Be(string.IsNullOrEmpty(jobType) ? null : jobType);
    }

    [Theory]
    [InlineData("Solar")]
    [InlineData("remote")]
    [InlineData("NotTriaged")]
    public async Task UpdateAsync_WhenJobTypeInvalid_ShouldThrowArgumentExceptionAndNotSave(string jobType)
    {
        var existing = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var act = () => _sut.UpdateAsync("ten_1", existing.Id, BuildUpdateRequest() with { JobType = jobType });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*job type*");
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("OnSite")]
    public async Task CreateAsync_WhenJobTypeNullOrValid_ShouldSaveIt(string? jobType)
    {
        var entity = BuildServiceRequest();
        entity.JobType = jobType;
        _repoMock.Setup(r => r.CreateAsync(entity, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var result = await _sut.CreateAsync("ten_1", entity);

        result.JobType.Should().Be(jobType);
    }

    [Theory]
    [InlineData("Solar")]
    [InlineData("NotTriaged")]
    public async Task CreateAsync_WhenJobTypeInvalid_ShouldThrowArgumentExceptionAndNotSave(string jobType)
    {
        var entity = BuildServiceRequest();
        entity.JobType = jobType;

        var act = () => _sut.CreateAsync("ten_1", entity);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*job type*");
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Remote")]
    [InlineData("NotTriaged")]
    public async Task SearchAsync_WhenJobTypeFilterValid_ShouldDelegateToRepository(string filter)
    {
        var request = new ServiceRequestSearchRequestDto { JobType = filter };
        var expected = new PagedResult<ServiceRequest> { Items = [] };
        _repoMock.Setup(r => r.SearchAsync("ten_1", request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.SearchAsync("ten_1", request);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task SearchAsync_WhenJobTypeFilterInvalid_ShouldThrowArgumentException()
    {
        var act = () => _sut.SearchAsync("ten_1", new ServiceRequestSearchRequestDto { JobType = "Solar" });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*job type*");
    }

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

    private static ServiceRequest BuildSubmissionMember(string id, int position) => new()
    {
        Id = id,
        TenantId = "ten_1",
        Status = "New",
        LocationId = "loc_slc",
        CustomerProfileId = "cp_1",
        IssueDescription = $"Issue {position}",
        IssueCategory = "Plumbing",
        SubmissionId = "sr_lead",
        SubmissionPosition = position,
        SubmissionCount = 3
    };

    private static ServiceRequestUpdateRequestDto BuildUpdateRequest() => new()
    {
        Status = "New",
        IssueDescription = "Water heater not working",
        IssueCategory = "Plumbing",
        Priority = "High"
    };
}
