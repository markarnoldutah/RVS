using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class AttachmentServiceTests
{
    private readonly Mock<IServiceRequestRepository> _repoMock = new();
    private readonly Mock<IBlobStorageService> _blobMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly Mock<IImageTranscoder> _transcoderMock = new();
    private readonly AttachmentService _sut;

    public AttachmentServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _transcoderMock.Setup(t => t.CanTranscode(It.IsAny<string>())).Returns(false);
        _sut = new AttachmentService(
            _repoMock.Object,
            _blobMock.Object,
            _userContextMock.Object,
            _transcoderMock.Object,
            Mock.Of<ILogger<AttachmentService>>());
    }

    // ── GenerateUploadSasAsync ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateUploadSasAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GenerateUploadSasAsync(tenantId!, "sr_1", "photo.jpg", "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateUploadSasAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var act = () => _sut.GenerateUploadSasAsync("ten_1", srId!, "photo.jpg", "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateUploadSasAsync_WhenFileNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? fileName)
    {
        var act = () => _sut.GenerateUploadSasAsync("ten_1", "sr_1", fileName!, "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateUploadSasAsync_WhenContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        var act = () => _sut.GenerateUploadSasAsync("ten_1", "sr_1", "photo.jpg", contentType!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateUploadSasAsync_WhenServiceRequestNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.GenerateUploadSasAsync("ten_1", "sr_missing", "photo.jpg", "image/jpeg");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GenerateUploadSasAsync_WhenMaxAttachmentsExceeded_ShouldThrowArgumentException()
    {
        var sr = BuildServiceRequest();
        for (var i = 0; i < 10; i++)
        {
            sr.Attachments.Add(new ServiceRequestAttachmentEmbedded
            {
                FileName = $"file{i}.jpg",
                ContentType = "image/jpeg"
            });
        }
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var act = () => _sut.GenerateUploadSasAsync("ten_1", sr.Id, "photo.jpg", "image/jpeg", maxAttachments: 10);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Maximum of 10*");
    }

    [Fact]
    public async Task GenerateUploadSasAsync_WhenContentTypeNotAllowed_ShouldThrowArgumentException()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var act = () => _sut.GenerateUploadSasAsync("ten_1", sr.Id, "malware.exe", "application/exe");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not an allowed*");
    }

    [Fact]
    public async Task GenerateUploadSasAsync_ShouldReturnSasUrlBlobNameAndExpiry()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);
        _blobMock.Setup(b => b.GenerateUploadSasUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://blob.test/sas-url?sig=abc");

        var result = await _sut.GenerateUploadSasAsync("ten_1", sr.Id, "photo.jpg", "image/jpeg");

        result.SasUrl.Should().Contain("sig=abc");
        result.BlobName.Should().StartWith("ten_1/" + sr.Id + "/");
        result.BlobName.Should().EndWith("_photo.jpg");
        result.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(30));
    }

    // ── GenerateReadSasAsync ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateReadSasAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GenerateReadSasAsync(tenantId!, "sr_1", "att_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateReadSasAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var act = () => _sut.GenerateReadSasAsync("ten_1", srId!, "att_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateReadSasAsync_WhenAttachmentIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? attId)
    {
        var act = () => _sut.GenerateReadSasAsync("ten_1", "sr_1", attId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateReadSasAsync_WhenServiceRequestNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.GenerateReadSasAsync("ten_1", "sr_missing", "att_1");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GenerateReadSasAsync_WhenAttachmentNotFound_ShouldThrowKeyNotFoundException()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var act = () => _sut.GenerateReadSasAsync("ten_1", sr.Id, "att_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GenerateReadSasAsync_WhenAttachmentExists_ShouldReturnSasUrlWithOneHourExpiry()
    {
        var sr = BuildServiceRequest();
        sr.Attachments.Add(new ServiceRequestAttachmentEmbedded
        {
            AttachmentId = "att_1",
            BlobUri = "ten_1/sr_1/att_1_photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024
        });
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);
        _blobMock.Setup(b => b.GenerateReadSasUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://blob.test/sas-url?sig=read");

        var result = await _sut.GenerateReadSasAsync("ten_1", sr.Id, "att_1");

        result.SasUrl.Should().Contain("sig=read");
        result.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromSeconds(30));
    }

    // ── ConfirmAttachmentAsync ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ConfirmAttachmentAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var request = BuildConfirmRequest();
        var act = () => _sut.ConfirmAttachmentAsync(tenantId!, "sr_1", request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ConfirmAttachmentAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var request = BuildConfirmRequest();
        var act = () => _sut.ConfirmAttachmentAsync("ten_1", srId!, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConfirmAttachmentAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ConfirmAttachmentAsync("ten_1", "sr_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ConfirmAttachmentAsync_WhenServiceRequestNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var request = BuildConfirmRequest();
        var act = () => _sut.ConfirmAttachmentAsync("ten_1", "sr_missing", request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ConfirmAttachmentAsync_WhenMaxAttachmentsExceeded_ShouldThrowArgumentException()
    {
        var sr = BuildServiceRequest();
        for (var i = 0; i < 10; i++)
        {
            sr.Attachments.Add(new ServiceRequestAttachmentEmbedded
            {
                FileName = $"file{i}.jpg",
                ContentType = "image/jpeg"
            });
        }
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var request = BuildConfirmRequest();
        var act = () => _sut.ConfirmAttachmentAsync("ten_1", sr.Id, request, maxAttachments: 10);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Maximum of 10*");
    }

    [Fact]
    public async Task ConfirmAttachmentAsync_WhenContentTypeNotAllowed_ShouldThrowArgumentException()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var request = BuildConfirmRequest() with { ContentType = "application/exe" };
        var act = () => _sut.ConfirmAttachmentAsync("ten_1", sr.Id, request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not an allowed*");
    }

    [Fact]
    public async Task ConfirmAttachmentAsync_WhenBlobDoesNotExist_ShouldThrowArgumentException()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);
        _blobMock.Setup(b => b.BlobExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = BuildConfirmRequest();
        var act = () => _sut.ConfirmAttachmentAsync("ten_1", sr.Id, request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not been uploaded*");
    }

    [Fact]
    public async Task ConfirmAttachmentAsync_WhenValid_ShouldAddAttachmentAndReturnDto()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);
        _blobMock.Setup(b => b.BlobExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var request = BuildConfirmRequest();
        var result = await _sut.ConfirmAttachmentAsync("ten_1", sr.Id, request);

        result.FileName.Should().Be("photo.jpg");
        result.ContentType.Should().Be("image/jpeg");
        result.SizeBytes.Should().Be(2048);
        result.BlobUri.Should().Be("ten_1/sr_1/att_photo.jpg");
        sr.Attachments.Should().HaveCount(1);
        sr.UpdatedByUserId.Should().Be("usr_test");
    }

    [Fact]
    public async Task ConfirmAttachmentAsync_WhenNonHeicImage_ShouldNotTouchBlobBytesOrTranscode()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);
        _blobMock.Setup(b => b.BlobExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var result = await _sut.ConfirmAttachmentAsync("ten_1", sr.Id, BuildConfirmRequest());

        result.ContentType.Should().Be("image/jpeg");
        result.BlobUri.Should().Be("ten_1/sr_1/att_photo.jpg");
        _transcoderMock.Verify(t => t.TranscodeToJpeg(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _blobMock.Verify(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _blobMock.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _blobMock.Verify(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAttachmentAsync_WhenHeicUpload_ShouldTranscodeStoreJpegAndDeleteOriginal()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);
        _blobMock.Setup(b => b.BlobExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        var heicBytes = new byte[] { 1, 2, 3, 4 };
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x10, 0x20, 0x30 };
        _transcoderMock.Setup(t => t.CanTranscode("image/heic")).Returns(true);
        _blobMock.Setup(b => b.DownloadAsync("rvs-attachments", "ten_1/sr_1/att_photo.heic", It.IsAny<CancellationToken>()))
            .ReturnsAsync(heicBytes);
        _transcoderMock.Setup(t => t.TranscodeToJpeg(heicBytes, It.IsAny<CancellationToken>()))
            .Returns(new ImageTranscodeResult(jpegBytes, 4032, 3024));

        var request = BuildConfirmRequest() with
        {
            BlobName = "ten_1/sr_1/att_photo.heic",
            FileName = "photo.heic",
            ContentType = "image/heic",
            SizeBytes = 900_000
        };

        var result = await _sut.ConfirmAttachmentAsync("ten_1", sr.Id, request);

        result.ContentType.Should().Be("image/jpeg");
        result.FileName.Should().Be("photo.jpg");
        result.BlobUri.Should().Be("ten_1/sr_1/att_photo.jpg");
        result.SizeBytes.Should().Be(jpegBytes.Length);

        _blobMock.Verify(b => b.UploadAsync(
            "rvs-attachments", "ten_1/sr_1/att_photo.jpg", It.IsAny<Stream>(), "image/jpeg", It.IsAny<CancellationToken>()),
            Times.Once);
        _blobMock.Verify(b => b.DeleteAsync(
            "rvs-attachments", "ten_1/sr_1/att_photo.heic", It.IsAny<CancellationToken>()),
            Times.Once);

        sr.Attachments.Should().ContainSingle()
            .Which.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task ConfirmAttachmentAsync_WhenHeicTranscodeFails_ShouldKeepOriginalUpload()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);
        _blobMock.Setup(b => b.BlobExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        _transcoderMock.Setup(t => t.CanTranscode("image/heic")).Returns(true);
        _blobMock.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3, 4 });
        _transcoderMock.Setup(t => t.TranscodeToJpeg(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns((ImageTranscodeResult?)null);

        var request = BuildConfirmRequest() with
        {
            BlobName = "ten_1/sr_1/att_photo.heic",
            FileName = "photo.heic",
            ContentType = "image/heic",
            SizeBytes = 900_000
        };

        var result = await _sut.ConfirmAttachmentAsync("ten_1", sr.Id, request);

        result.ContentType.Should().Be("image/heic");
        result.FileName.Should().Be("photo.heic");
        result.BlobUri.Should().Be("ten_1/sr_1/att_photo.heic");
        result.SizeBytes.Should().Be(900_000);

        _blobMock.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _blobMock.Verify(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── DeleteAttachmentAsync ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DeleteAttachmentAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.DeleteAttachmentAsync(tenantId!, "sr_1", "att_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAttachmentAsync_WhenServiceRequestNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var act = () => _sut.DeleteAttachmentAsync("ten_1", "sr_missing", "att_1");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAttachmentAsync_WhenAttachmentNotFound_ShouldThrowKeyNotFoundException()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var act = () => _sut.DeleteAttachmentAsync("ten_1", sr.Id, "att_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AttachmentConfirmRequestDto BuildConfirmRequest() => new()
    {
        BlobName = "ten_1/sr_1/att_photo.jpg",
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        SizeBytes = 2048
    };

    private static ServiceRequest BuildServiceRequest() => new()
    {
        Id = "sr_test_1",
        TenantId = "ten_1",
        Status = "New",
        LocationId = "loc_slc",
        CustomerProfileId = "cp_1",
        IssueDescription = "Test issue",
        IssueCategory = "Test",
        Priority = "High"
    };
}
