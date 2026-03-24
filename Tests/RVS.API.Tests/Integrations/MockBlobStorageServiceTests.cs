using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class MockBlobStorageServiceTests
{
    private readonly MockBlobStorageService _sut = new(Mock.Of<ILogger<MockBlobStorageService>>());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateSasUrlAsync_WhenContainerNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? containerName)
    {
        var act = () => _sut.GenerateSasUrlAsync(containerName!, "blob.jpg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateSasUrlAsync_WhenBlobNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? blobName)
    {
        var act = () => _sut.GenerateSasUrlAsync("attachments", blobName!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateSasUrlAsync_ShouldReturnFakeSasUrl()
    {
        var result = await _sut.GenerateSasUrlAsync("attachments", "ten_1/loc_1/sr_001/att_1_photo.jpg");

        result.Should().StartWith("https://mockblob.blob.core.windows.net/attachments/");
        result.Should().Contain("ten_1/loc_1/sr_001/att_1_photo.jpg");
        result.Should().Contain("sig=fakesig");
    }

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
}
