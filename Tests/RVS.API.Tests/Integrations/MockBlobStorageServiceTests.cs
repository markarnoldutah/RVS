using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.Infra.AzBlobRepository;

namespace RVS.API.Tests.Integrations;

public class MockBlobStorageServiceTests
{
    private readonly MockBlobStorageService _sut = new(Mock.Of<ILogger<MockBlobStorageService>>());

    // ── GenerateUploadSasUrlAsync ────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateUploadSasUrlAsync_WhenContainerNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? containerName)
    {
        var act = () => _sut.GenerateUploadSasUrlAsync(containerName!, "blob.jpg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateUploadSasUrlAsync_WhenBlobNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? blobName)
    {
        var act = () => _sut.GenerateUploadSasUrlAsync("attachments", blobName!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateUploadSasUrlAsync_ShouldReturnFakeUploadSasUrl()
    {
        var result = await _sut.GenerateUploadSasUrlAsync("attachments", "ten_1/loc_1/sr_001/att_1_photo.jpg");

        result.Should().StartWith("https://mockblob.blob.core.windows.net/attachments/");
        result.Should().Contain("ten_1/loc_1/sr_001/att_1_photo.jpg");
        result.Should().Contain("sp=wc");
        result.Should().Contain("sig=fakesig");
    }

    // ── GenerateReadSasUrlAsync ──────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateReadSasUrlAsync_WhenContainerNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? containerName)
    {
        var act = () => _sut.GenerateReadSasUrlAsync(containerName!, "blob.jpg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateReadSasUrlAsync_WhenBlobNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? blobName)
    {
        var act = () => _sut.GenerateReadSasUrlAsync("attachments", blobName!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateReadSasUrlAsync_ShouldReturnFakeReadSasUrl()
    {
        var result = await _sut.GenerateReadSasUrlAsync("attachments", "ten_1/loc_1/sr_001/att_1_photo.jpg");

        result.Should().StartWith("https://mockblob.blob.core.windows.net/attachments/");
        result.Should().Contain("ten_1/loc_1/sr_001/att_1_photo.jpg");
        result.Should().Contain("sp=r");
        result.Should().Contain("sig=fakesig");
    }

    // ── GenerateReadSasUrlAsync (explicit lifetime) ─────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateReadSasUrlAsync_WithLifetime_WhenContainerNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? containerName)
    {
        var act = () => _sut.GenerateReadSasUrlAsync(containerName!, "blob.jpg", TimeSpan.FromDays(7));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateReadSasUrlAsync_WithLifetime_WhenBlobNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? blobName)
    {
        var act = () => _sut.GenerateReadSasUrlAsync("attachments", blobName!, TimeSpan.FromDays(7));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateReadSasUrlAsync_WithLifetime_ShouldReturnFakeReadSasUrl()
    {
        var result = await _sut.GenerateReadSasUrlAsync(
            "attachments", "ten_1/loc_1/sr_001/att_1_photo.jpg", TimeSpan.FromDays(7));

        result.Should().StartWith("https://mockblob.blob.core.windows.net/attachments/");
        result.Should().Contain("ten_1/loc_1/sr_001/att_1_photo.jpg");
        result.Should().Contain("sp=r");
        result.Should().Contain("sig=fakesig");
    }

    // ── UploadAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_WhenContentIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.UploadAsync("attachments", "blob.jpg", null!, "image/jpeg");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UploadAsync_WhenContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        using var stream = new MemoryStream();
        var act = () => _sut.UploadAsync("attachments", "blob.jpg", stream, contentType!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadAsync_ShouldReturnFakeUri()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var result = await _sut.UploadAsync("attachments", "ten_1/loc_1/sr_001/att_1_photo.jpg", stream, "image/jpeg");

        result.Should().Be("https://mockblob.blob.core.windows.net/attachments/ten_1/loc_1/sr_001/att_1_photo.jpg");
    }

    // ── DownloadAsync ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DownloadAsync_WhenContainerNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? containerName)
    {
        var act = () => _sut.DownloadAsync(containerName!, "blob.jpg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DownloadAsync_WhenBlobNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? blobName)
    {
        var act = () => _sut.DownloadAsync("attachments", blobName!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DownloadAsync_ShouldReturnNonEmptyPngBytes()
    {
        var result = await _sut.DownloadAsync("attachments", "ten_1/loc_1/sr_001/att_1_photo.jpg");

        result.Should().NotBeNullOrEmpty();
        // PNG magic number
        result.Take(8).Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    }
}
