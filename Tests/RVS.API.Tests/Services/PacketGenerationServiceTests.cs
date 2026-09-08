using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Packets;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Packets;

namespace RVS.API.Tests.Services;

/// <summary>
/// Tests for <see cref="PacketGenerationService"/> — the orchestrator that composes, renders,
/// and stores a service packet off the intake request thread (<c>Spec B-1</c>, issue #434).
///
/// Contract under test: generation is attempt-tracked on the request; a failure records state
/// and returns <see cref="PacketGenerationOutcome.Retry"/>/<see cref="PacketGenerationOutcome.Exhausted"/>
/// rather than throwing; three exhausted attempts raise an alert flag; the service request is
/// never rolled back or deleted by a packet failure.
/// </summary>
public class PacketGenerationServiceTests
{
    private const string TenantId = "ten_acme";
    private const string SrId = "a1b2c3d4-1111-2222-3333-444455556666";
    private const string AttachmentsContainer = "rvs-attachments";

    private readonly Mock<IServiceRequestRepository> _srRepoMock = new();
    private readonly Mock<ILocationRepository> _locationRepoMock = new();
    private readonly Mock<IPacketPhotoUrlResolver> _photoResolverMock = new();
    private readonly Mock<IBlobStorageService> _blobMock = new();
    private readonly Mock<IPacketGenerationQueue> _queueMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly PacketGenerationService _sut;

    public PacketGenerationServiceTests()
    {
        _srRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest sr, CancellationToken _) => sr);

        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location { Id = "loc_1", TenantId = TenantId, Name = "Salt Lake Service", Phone = "801-555-0100" });

        _photoResolverMock.Setup(r => r.ResolveAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        _blobMock.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnePixelPng);
        _blobMock.Setup(b => b.UploadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string c, string b, Stream _, string _, CancellationToken _) => $"https://blob/{c}/{b}");

        _userContextMock.SetupGet(u => u.UserId).Returns("user_manager_7");

        _sut = new PacketGenerationService(
            _srRepoMock.Object,
            _locationRepoMock.Object,
            _photoResolverMock.Object,
            _blobMock.Object,
            _queueMock.Object,
            _userContextMock.Object,
            Mock.Of<ILogger<PacketGenerationService>>());
    }

    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static ServiceRequest BuildRequest(params ServiceRequestAttachmentEmbedded[] attachments) => new()
    {
        Id = SrId,
        TenantId = TenantId,
        LocationId = "loc_1",
        Status = "New",
        IssueDescription = "Slide will not retract",
        IssueCategory = "Slide System",
        CustomerSnapshot = new CustomerSnapshotEmbedded { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com" },
        AssetInfo = new AssetInfoEmbedded { AssetId = "1HGBH41JXMN109186", Manufacturer = "Jayco", Model = "Eagle", Year = 2021 },
        Attachments = [.. attachments],
    };

    private static ServiceRequestAttachmentEmbedded Image(string id, string blobUri) => new()
    {
        AttachmentId = id,
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        BlobUri = blobUri,
    };

    private void SetupRequest(ServiceRequest sr) =>
        _srRepoMock.Setup(r => r.GetByIdAsync(TenantId, SrId, It.IsAny<CancellationToken>())).ReturnsAsync(sr);

    // ── Guard clauses ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GenerateAsync(tenantId!, SrId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.GenerateAsync(TenantId, id!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateAsync_WhenRequestNotFound_ShouldThrowKeyNotFoundException()
    {
        _srRepoMock.Setup(r => r.GetByIdAsync(TenantId, SrId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.GenerateAsync(TenantId, SrId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_OnSuccess_ShouldMarkSucceededWithVersionOneAndPdfPath()
    {
        var sr = BuildRequest();
        SetupRequest(sr);

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        sr.PacketGeneration.Status.Should().Be("Succeeded");
        sr.PacketGeneration.PacketVersion.Should().Be(1);
        sr.PacketGeneration.AttemptCount.Should().Be(1);
        sr.PacketGeneration.PdfBlobPath.Should().Be($"packets/{TenantId}/{SrId}/v1.pdf");
        sr.PacketGeneration.GeneratedAtUtc.Should().NotBeNull();
        sr.PacketGeneration.LastError.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_OnSuccess_ShouldUploadAPdfToTheAttachmentsContainer()
    {
        var sr = BuildRequest();
        SetupRequest(sr);

        await _sut.GenerateAsync(TenantId, SrId);

        _blobMock.Verify(b => b.UploadAsync(
            AttachmentsContainer,
            $"packets/{TenantId}/{SrId}/v1.pdf",
            It.IsAny<Stream>(),
            "application/pdf",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_ShouldPersistGeneratingStateBeforeItPersistsSuccess()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        var statusesAtEachSave = new List<string>();
        _srRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest s, CancellationToken _) =>
            {
                statusesAtEachSave.Add(s.PacketGeneration.Status);
                return s;
            });

        await _sut.GenerateAsync(TenantId, SrId);

        statusesAtEachSave.Should().StartWith("Generating");
        statusesAtEachSave.Should().EndWith("Succeeded");
    }

    [Fact]
    public async Task GenerateAsync_ShouldDownloadBytesForEachResolvedPhoto()
    {
        var sr = BuildRequest(
            Image("att_1", "ten_acme/sr/one.jpg"),
            Image("att_2", "ten_acme/sr/two.jpg"));
        SetupRequest(sr);
        _photoResolverMock.Setup(r => r.ResolveAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["att_1"] = "https://blob/one.jpg?sig=a",
                ["att_2"] = "https://blob/two.jpg?sig=b",
            });

        await _sut.GenerateAsync(TenantId, SrId);

        _blobMock.Verify(b => b.DownloadAsync(AttachmentsContainer, "ten_acme/sr/one.jpg", It.IsAny<CancellationToken>()), Times.Once);
        _blobMock.Verify(b => b.DownloadAsync(AttachmentsContainer, "ten_acme/sr/two.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_WhenOnePhotoDownloadFails_ShouldStillSucceed()
    {
        var sr = BuildRequest(Image("att_1", "ten_acme/sr/one.jpg"));
        SetupRequest(sr);
        _photoResolverMock.Setup(r => r.ResolveAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["att_1"] = "https://blob/one.jpg?sig=a" });
        _blobMock.Setup(b => b.DownloadAsync(AttachmentsContainer, "ten_acme/sr/one.jpg", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("blob read timeout"));

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        sr.PacketGeneration.Status.Should().Be("Succeeded");
    }

    // ── Paste block (Spec B-5, issue #436) ─────────────────────────────────

    [Fact]
    public async Task GenerateAsync_WithAVeryLargeDescriptionAndASmallLocationCap_ShouldStillSucceed()
    {
        var sr = BuildRequest();
        sr.IssueDescription = string.Join(" ", Enumerable.Repeat("wordword", 4000));
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location
            {
                Id = "loc_1",
                TenantId = TenantId,
                Name = "Salt Lake Service",
                PacketConfig = new PacketConfigEmbedded { PasteBlockCharacterCap = 120 },
            });

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        sr.PacketGeneration.Status.Should().Be("Succeeded");
    }

    [Fact]
    public async Task GenerateAsync_WhenLocationIsMissing_ShouldStillSucceedUsingTheDefaultPasteBlockCap()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
    }

    // ── Failure isolation ──────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_WhenRenderingFails_OnFirstAttempt_ShouldReturnRetryAndRecordFailure()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _blobMock.Setup(b => b.UploadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob upload rejected"));

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Retry);
        sr.PacketGeneration.Status.Should().Be("Failed");
        sr.PacketGeneration.AttemptCount.Should().Be(1);
        sr.PacketGeneration.LastError.Should().Contain("InvalidOperationException");
        sr.PacketGeneration.AlertRaised.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WhenThirdAttemptFails_ShouldReturnExhaustedAndRaiseAlert()
    {
        var sr = BuildRequest();
        sr.PacketGeneration.MarkGenerating();
        sr.PacketGeneration.MarkGenerating(); // two prior attempts → this call is the third
        SetupRequest(sr);
        _blobMock.Setup(b => b.UploadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("still failing"));

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Exhausted);
        sr.PacketGeneration.AttemptCount.Should().Be(3);
        sr.PacketGeneration.AlertRaised.Should().BeTrue();
        sr.PacketGeneration.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task GenerateAsync_WhenGenerationFails_ShouldNeverDeleteOrRollBackTheServiceRequest()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _blobMock.Setup(b => b.UploadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.GenerateAsync(TenantId, SrId);

        sr.Status.Should().Be("New", "the workflow status is untouched by a packet failure");
        sr.CustomerSnapshot.Email.Should().Be("jane@example.com");
        _srRepoMock.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_WhenCancelled_ShouldPropagateWithoutMarkingFailed()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _photoResolverMock.Setup(r => r.ResolveAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.GenerateAsync(TenantId, SrId);

        await act.Should().ThrowAsync<OperationCanceledException>();
        sr.PacketGeneration.Status.Should().Be("Generating", "a cancellation is not a generation failure");
    }

    // ── RequestRegenerationAsync ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestRegenerationAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.RequestRegenerationAsync(tenantId!, SrId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RequestRegenerationAsync_WhenRequestNotFound_ShouldThrowKeyNotFoundException()
    {
        _srRepoMock.Setup(r => r.GetByIdAsync(TenantId, SrId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.RequestRegenerationAsync(TenantId, SrId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RequestRegenerationAsync_ShouldResetStatePersistAndEnqueue()
    {
        var sr = BuildRequest();
        sr.PacketGeneration.MarkGenerating();
        sr.PacketGeneration.MarkGenerating();
        sr.PacketGeneration.MarkGenerating();
        sr.PacketGeneration.MarkFailed("boom");
        sr.PacketGeneration.MarkAlertRaised();
        SetupRequest(sr);

        await _sut.RequestRegenerationAsync(TenantId, SrId);

        sr.PacketGeneration.Status.Should().Be("Pending");
        sr.PacketGeneration.AttemptCount.Should().Be(0);
        sr.PacketGeneration.AlertRaised.Should().BeFalse();
        sr.UpdatedByUserId.Should().Be("user_manager_7");
        _srRepoMock.Verify(r => r.UpdateAsync(sr, It.IsAny<CancellationToken>()), Times.Once);
        _queueMock.Verify(q => q.TryEnqueue(
            It.Is<PacketGenerationJob>(j => j.TenantId == TenantId && j.ServiceRequestId == SrId && j.Trigger == "regeneration")),
            Times.Once);
    }
}
