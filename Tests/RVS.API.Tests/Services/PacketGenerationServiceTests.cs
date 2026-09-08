using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RVS.API.Options;
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
    private readonly Mock<INotificationService> _notificationMock = new();
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
            _notificationMock.Object,
            // Zero backoff so retry tests do not actually wait.
            Microsoft.Extensions.Options.Options.Create(new PacketEmailOptions { RetryBaseDelay = TimeSpan.Zero }),
            Mock.Of<ILogger<PacketGenerationService>>());
    }

    /// <summary>A location wired for packet email: enabled, with recipients configured.</summary>
    private static Location LocationWithRecipients(
        bool attachPdf = true, bool includePhotos = true, bool enabled = true) => new()
    {
        Id = "loc_1",
        TenantId = TenantId,
        Name = "Salt Lake Service",
        Phone = "801-555-0100",
        PacketConfig = new PacketConfigEmbedded
        {
            Enabled = enabled,
            Recipients = ["service@dealer.example", "advisor@dealer.example"],
            AttachPdf = attachPdf,
            IncludePhotos = includePhotos,
        },
    };

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

    // ── Packet email delivery (Spec B-4, issue #437) ──────────────────────

    [Fact]
    public async Task GenerateAsync_OnSuccess_WhenLocationHasRecipients_ShouldSendThePacketEmail()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients());

        await _sut.GenerateAsync(TenantId, SrId);

        _notificationMock.Verify(n => n.SendPacketEmailAsync(
            It.Is<PacketEmailMessage>(m =>
                m.Subject == "[RVS] Slide System — 2021 Jayco Eagle — Doe" &&
                m.Recipients.SequenceEqual(new[] { "service@dealer.example", "advisor@dealer.example" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_OnSuccess_WhenDeliveryIsDisabled_ShouldNotSendAnEmail()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients(enabled: false));

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        _notificationMock.Verify(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_OnSuccess_WhenNoRecipientsAreConfigured_ShouldNotSendAnEmail()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        // Default location mock has an enabled PacketConfig with an empty recipient list.

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        _notificationMock.Verify(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_OnSuccess_WhenLocationIsMissing_ShouldNotSendAnEmail()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        _notificationMock.Verify(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_OnSuccess_ShouldAttachThePdfWhenTheLocationAsksForIt()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients(attachPdf: true, includePhotos: false));

        PacketEmailMessage? sent = null;
        _notificationMock.Setup(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<PacketEmailMessage, CancellationToken>((m, _) => sent = m)
            .Returns(Task.CompletedTask);

        await _sut.GenerateAsync(TenantId, SrId);

        sent.Should().NotBeNull();
        sent!.Attachments.Should().ContainSingle(a => a.ContentType == "application/pdf");
    }

    [Fact]
    public async Task GenerateAsync_OnSuccess_ShouldNotAttachThePdfWhenTheLocationOptsOut()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients(attachPdf: false, includePhotos: false));

        PacketEmailMessage? sent = null;
        _notificationMock.Setup(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<PacketEmailMessage, CancellationToken>((m, _) => sent = m)
            .Returns(Task.CompletedTask);

        await _sut.GenerateAsync(TenantId, SrId);

        sent.Should().NotBeNull();
        sent!.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_OnSuccess_ShouldAttachTheOriginalPhotosWhenTheLocationAsksForThem()
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
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients(attachPdf: false, includePhotos: true));

        PacketEmailMessage? sent = null;
        _notificationMock.Setup(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<PacketEmailMessage, CancellationToken>((m, _) => sent = m)
            .Returns(Task.CompletedTask);

        await _sut.GenerateAsync(TenantId, SrId);

        sent.Should().NotBeNull();
        sent!.Attachments.Should().HaveCount(2);
        sent.Attachments.Should().OnlyContain(a => a.ContentType == "image/jpeg");
    }

    [Fact]
    public async Task GenerateAsync_OnSuccess_ShouldNotAttachPhotosWhenTheLocationOptsOut()
    {
        var sr = BuildRequest(Image("att_1", "ten_acme/sr/one.jpg"));
        SetupRequest(sr);
        _photoResolverMock.Setup(r => r.ResolveAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["att_1"] = "https://blob/one.jpg?sig=a" });
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients(attachPdf: true, includePhotos: false));

        PacketEmailMessage? sent = null;
        _notificationMock.Setup(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<PacketEmailMessage, CancellationToken>((m, _) => sent = m)
            .Returns(Task.CompletedTask);

        await _sut.GenerateAsync(TenantId, SrId);

        sent.Should().NotBeNull();
        sent!.Attachments.Should().OnlyContain(a => a.ContentType == "application/pdf");
    }

    [Fact]
    public async Task GenerateAsync_WhenTheEmailSendThrows_ShouldStillReportGenerationSucceeded()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients());
        _notificationMock.Setup(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ACS rejected the message"));

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        sr.PacketGeneration.Status.Should().Be("Succeeded");
        sr.PacketGeneration.PacketVersion.Should().Be(1);
    }

    [Fact]
    public async Task GenerateAsync_WhenGenerationItselfFails_ShouldNotSendAnEmail()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients());
        _blobMock.Setup(b => b.UploadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob upload rejected"));

        await _sut.GenerateAsync(TenantId, SrId);

        _notificationMock.Verify(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Packet email idempotency + retry (Spec B-4, issue #438) ───────────

    [Fact]
    public async Task GenerateAsync_WhenEmailDelivers_ShouldRecordDeliveredForThePacketVersion()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients());

        await _sut.GenerateAsync(TenantId, SrId);

        sr.PacketEmailDelivery.Status.Should().Be("Delivered");
        sr.PacketEmailDelivery.DeliveredPacketVersion.Should().Be(1);
        sr.PacketEmailDelivery.AttemptCount.Should().Be(1);
        sr.PacketEmailDelivery.DeliveredAtUtc.Should().NotBeNull();
        sr.PacketEmailDelivery.LastError.Should().BeNull();
        sr.PacketEmailDelivery.AlertRaised.Should().BeFalse();
        _notificationMock.Verify(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_WhenFirstEmailAttemptFails_ShouldRetryAndDeliverOnTheSecond()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients());
        _notificationMock.SetupSequence(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ACS 503"))
            .Returns(Task.CompletedTask);

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        _notificationMock.Verify(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        sr.PacketEmailDelivery.Status.Should().Be("Delivered");
        sr.PacketEmailDelivery.AttemptCount.Should().Be(2);
        sr.PacketEmailDelivery.DeliveredPacketVersion.Should().Be(1);
    }

    [Fact]
    public async Task GenerateAsync_WhenAllThreeEmailAttemptsFail_ShouldRecordFailedAndRaiseAlert()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients());
        _notificationMock.Setup(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ACS rejected the message"));

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        _notificationMock.Verify(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(PacketEmailDeliveryEmbedded.MaxAttempts));
        sr.PacketEmailDelivery.Status.Should().Be("Failed");
        sr.PacketEmailDelivery.AttemptCount.Should().Be(3);
        sr.PacketEmailDelivery.AlertRaised.Should().BeTrue();
        sr.PacketEmailDelivery.DeliveredPacketVersion.Should().Be(0);
        sr.PacketEmailDelivery.LastError.Should().Contain("InvalidOperationException");
        // Generation itself is untouched by a delivery failure.
        sr.PacketGeneration.Status.Should().Be("Succeeded");
        sr.PacketGeneration.PacketVersion.Should().Be(1);
    }

    [Fact]
    public async Task GenerateAsync_WhenThePacketVersionWasAlreadyDelivered_ShouldNotSendAgain()
    {
        var sr = BuildRequest();
        // A prior successful generation + delivery of v1; this run regenerates to v2.
        sr.PacketGeneration.MarkSucceeded("packets/x/v1.pdf", DateTime.UtcNow);
        sr.PacketEmailDelivery.MarkAttempt();
        sr.PacketEmailDelivery.MarkDelivered(2, DateTime.UtcNow);
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients());

        var outcome = await _sut.GenerateAsync(TenantId, SrId);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        sr.PacketGeneration.PacketVersion.Should().Be(2);
        _notificationMock.Verify(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        sr.PacketEmailDelivery.AttemptCount.Should().Be(1, "the already-delivered run is left untouched");
        sr.PacketEmailDelivery.DeliveredPacketVersion.Should().Be(2);
    }

    [Fact]
    public async Task GenerateAsync_WhenEmailDelivers_ShouldPersistTheDeliveryStateOnTheRequest()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients());
        var deliveryStatusesAtEachSave = new List<string>();
        _srRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest s, CancellationToken _) =>
            {
                deliveryStatusesAtEachSave.Add(s.PacketEmailDelivery.Status);
                return s;
            });

        await _sut.GenerateAsync(TenantId, SrId);

        deliveryStatusesAtEachSave.Should().EndWith("Delivered", "the delivery outcome is persisted after the send");
    }

    [Fact]
    public async Task GenerateAsync_WhenAnEmailRetryIsCancelled_ShouldPropagateOperationCanceled()
    {
        var sr = BuildRequest();
        SetupRequest(sr);
        _locationRepoMock.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationWithRecipients());
        _notificationMock.Setup(n => n.SendPacketEmailAsync(It.IsAny<PacketEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.GenerateAsync(TenantId, SrId);

        await act.Should().ThrowAsync<OperationCanceledException>();
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
