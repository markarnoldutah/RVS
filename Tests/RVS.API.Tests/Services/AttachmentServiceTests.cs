using FluentAssertions;
using Moq;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class AttachmentServiceTests
{
    private readonly Mock<IServiceRequestRepository> _repoMock = new();
    private readonly Mock<IBlobStorageService> _blobMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly AttachmentService _sut;

    public AttachmentServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _sut = new AttachmentService(_repoMock.Object, _blobMock.Object, _userContextMock.Object);
    }

    // ── GenerateUploadSasAsync ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateUploadSasAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GenerateUploadSasAsync(tenantId!, "sr_1", "photo.jpg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateUploadSasAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var act = () => _sut.GenerateUploadSasAsync("ten_1", srId!, "photo.jpg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateUploadSasAsync_WhenFileNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? fileName)
    {
        var act = () => _sut.GenerateUploadSasAsync("ten_1", "sr_1", fileName!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateUploadSasAsync_ShouldReturnSasUrlAndExpiry()
    {
        _blobMock.Setup(b => b.GenerateUploadSasUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://blob.test/sas-url?sig=abc");

        var result = await _sut.GenerateUploadSasAsync("ten_1", "sr_1", "photo.jpg");

        result.SasUrl.Should().Contain("sig=abc");
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

    // ── CreateAttachmentAsync ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateAttachmentAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        using var stream = new MemoryStream(JpegMagicBytes);
        var act = () => _sut.CreateAttachmentAsync(tenantId!, "sr_1", "photo.jpg", "image/jpeg", stream);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateAttachmentAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        using var stream = new MemoryStream(JpegMagicBytes);
        var act = () => _sut.CreateAttachmentAsync("ten_1", srId!, "photo.jpg", "image/jpeg", stream);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAttachmentAsync_WhenServiceRequestNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "sr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        using var stream = new MemoryStream(JpegMagicBytes);
        var act = () => _sut.CreateAttachmentAsync("ten_1", "sr_missing", "photo.jpg", "image/jpeg", stream);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateAttachmentAsync_WhenMaxAttachmentsExceeded_ShouldThrowArgumentException()
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

        using var stream = new MemoryStream(JpegMagicBytes);
        var act = () => _sut.CreateAttachmentAsync("ten_1", sr.Id, "photo.jpg", "image/jpeg", stream, maxAttachments: 10);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Maximum of 10*");
    }

    [Fact]
    public async Task CreateAttachmentAsync_WhenFileTooLarge_ShouldThrowArgumentException()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var largeContent = new byte[2 * 1024 * 1024 + 1];
        largeContent[0] = 0xFF;
        largeContent[1] = 0xD8;
        largeContent[2] = 0xFF;
        using var stream = new MemoryStream(largeContent);

        var act = () => _sut.CreateAttachmentAsync("ten_1", sr.Id, "photo.jpg", "image/jpeg", stream, maxFileSizeMb: 1);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds the maximum*");
    }

    [Fact]
    public async Task CreateAttachmentAsync_WhenValid_ShouldUploadAndAddAttachment()
    {
        var sr = BuildServiceRequest();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", sr.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);
        _blobMock.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://blob.test/uploaded");
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest e, CancellationToken _) => e);

        using var stream = new MemoryStream(JpegMagicBytes);

        var result = await _sut.CreateAttachmentAsync("ten_1", sr.Id, "photo.jpg", "image/jpeg", stream);

        result.Attachments.Should().HaveCount(1);
        result.Attachments[0].FileName.Should().Be("photo.jpg");
        result.Attachments[0].ContentType.Should().Be("image/jpeg");
        result.Attachments[0].BlobUri.Should().Be("https://blob.test/uploaded");
        result.UpdatedByUserId.Should().Be("usr_test");
    }

    // ── ValidateMimeTypeAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMimeTypeAsync_WhenStreamIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ValidateMimeTypeAsync(null!, "image/jpeg");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ValidateMimeTypeAsync_WhenDeclaredContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        using var stream = new MemoryStream(JpegMagicBytes);
        var act = () => _sut.ValidateMimeTypeAsync(stream, contentType!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateMimeTypeAsync_WhenMimeMatches_ShouldNotThrow()
    {
        using var stream = new MemoryStream(JpegMagicBytes);

        var act = () => _sut.ValidateMimeTypeAsync(stream, "image/jpeg");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateMimeTypeAsync_WhenMimeMismatch_ShouldThrowArgumentException()
    {
        using var stream = new MemoryStream(PngMagicBytes);

        var act = () => _sut.ValidateMimeTypeAsync(stream, "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*MIME type mismatch*");
    }

    [Fact]
    public async Task ValidateMimeTypeAsync_WhenJpeg_ShouldDetectCorrectly()
    {
        using var stream = new MemoryStream(JpegMagicBytes);

        var act = () => _sut.ValidateMimeTypeAsync(stream, "image/jpeg");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateMimeTypeAsync_WhenPng_ShouldDetectCorrectly()
    {
        using var stream = new MemoryStream(PngMagicBytes);

        var act = () => _sut.ValidateMimeTypeAsync(stream, "image/png");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateMimeTypeAsync_WhenPdf_ShouldDetectCorrectly()
    {
        using var stream = new MemoryStream(PdfMagicBytes);

        var act = () => _sut.ValidateMimeTypeAsync(stream, "application/pdf");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateMimeTypeAsync_WhenWav_ShouldDetectCorrectly()
    {
        using var stream = new MemoryStream(WavMagicBytes);

        var act = () => _sut.ValidateMimeTypeAsync(stream, "audio/wav");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateMimeTypeAsync_WhenStreamPositionIsReset()
    {
        using var stream = new MemoryStream(JpegMagicBytes);

        await _sut.ValidateMimeTypeAsync(stream, "image/jpeg");

        stream.Position.Should().Be(0);
    }

    // ── DetectMimeType (internal) ────────────────────────────────────────────

    [Fact]
    public void DetectMimeType_Jpeg_ShouldReturnImageJpeg()
    {
        var result = AttachmentService.DetectMimeType(JpegMagicBytes, JpegMagicBytes.Length);

        result.Should().Be("image/jpeg");
    }

    [Fact]
    public void DetectMimeType_Png_ShouldReturnImagePng()
    {
        var result = AttachmentService.DetectMimeType(PngMagicBytes, PngMagicBytes.Length);

        result.Should().Be("image/png");
    }

    [Fact]
    public void DetectMimeType_Pdf_ShouldReturnApplicationPdf()
    {
        var result = AttachmentService.DetectMimeType(PdfMagicBytes, PdfMagicBytes.Length);

        result.Should().Be("application/pdf");
    }

    [Fact]
    public void DetectMimeType_Wav_ShouldReturnAudioWav()
    {
        var result = AttachmentService.DetectMimeType(WavMagicBytes, WavMagicBytes.Length);

        result.Should().Be("audio/wav");
    }

    [Fact]
    public void DetectMimeType_Mp4_ShouldReturnVideoMp4()
    {
        var result = AttachmentService.DetectMimeType(Mp4MagicBytes, Mp4MagicBytes.Length);

        result.Should().Be("video/mp4");
    }

    [Fact]
    public void DetectMimeType_UnknownBytes_ShouldReturnNull()
    {
        var unknown = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };

        var result = AttachmentService.DetectMimeType(unknown, unknown.Length);

        result.Should().BeNull();
    }

    [Fact]
    public void DetectMimeType_TooFewBytes_ShouldReturnNull()
    {
        var result = AttachmentService.DetectMimeType([0x00, 0x01], 2);

        result.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly byte[] JpegMagicBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] PngMagicBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] PdfMagicBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35];
    private static readonly byte[] WavMagicBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] Mp4MagicBytes = [0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70];

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
