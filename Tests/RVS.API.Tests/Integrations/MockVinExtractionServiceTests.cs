using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class MockVinExtractionServiceTests
{
    private readonly MockVinExtractionService _sut = new(Mock.Of<ILogger<MockVinExtractionService>>());

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenImageDataIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ExtractVinFromImageAsync(null!, "image/jpeg");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ExtractVinFromImageAsync_WhenContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF };

        var act = () => _sut.ExtractVinFromImageAsync(imageData, contentType!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_ShouldReturnHardcodedVin()
    {
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        var result = await _sut.ExtractVinFromImageAsync(imageData, "image/jpeg");

        result.Should().NotBeNull();
        result!.Vin.Should().Be("1RGDE4428R1000001");
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_ShouldReturnHighConfidence()
    {
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        var result = await _sut.ExtractVinFromImageAsync(imageData, "image/jpeg");

        result.Should().NotBeNull();
        result!.Confidence.Should().Be(0.95);
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_ShouldReturnMockProviderName()
    {
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        var result = await _sut.ExtractVinFromImageAsync(imageData, "image/jpeg");

        result.Should().NotBeNull();
        result!.Provider.Should().Be("MockVinExtractionService");
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_ShouldAcceptPngContentType()
    {
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var result = await _sut.ExtractVinFromImageAsync(imageData, "image/png");

        result.Should().NotBeNull();
        result!.Vin.Should().Be("1RGDE4428R1000001");
    }
}
