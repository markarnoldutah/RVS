using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class MockSpeechToTextServiceTests
{
    private readonly MockSpeechToTextService _sut = new(Mock.Of<ILogger<MockSpeechToTextService>>());

    [Fact]
    public async Task TranscribeAudioAsync_WhenAudioDataIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.TranscribeAudioAsync(null!, "audio/webm", "en-US");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task TranscribeAudioAsync_WhenContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        var audioData = new byte[] { 0x00, 0x01, 0x02 };

        var act = () => _sut.TranscribeAudioAsync(audioData, contentType!, "en-US");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task TranscribeAudioAsync_WhenLocaleIsNullOrWhiteSpace_ShouldThrowArgumentException(string? locale)
    {
        var audioData = new byte[] { 0x00, 0x01, 0x02 };

        var act = () => _sut.TranscribeAudioAsync(audioData, "audio/webm", locale!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldReturnHardcodedTranscript()
    {
        var audioData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var result = await _sut.TranscribeAudioAsync(audioData, "audio/webm", "en-US");

        result.Should().NotBeNull();
        result!.RawTranscript.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldReturnCleanedDescription()
    {
        var audioData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var result = await _sut.TranscribeAudioAsync(audioData, "audio/webm", "en-US");

        result.Should().NotBeNull();
        result!.CleanedDescription.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldReturnHighConfidence()
    {
        var audioData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var result = await _sut.TranscribeAudioAsync(audioData, "audio/webm", "en-US");

        result.Should().NotBeNull();
        result!.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldReturnMockProviderName()
    {
        var audioData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var result = await _sut.TranscribeAudioAsync(audioData, "audio/webm", "en-US");

        result.Should().NotBeNull();
        result!.Provider.Should().Be("MockSpeechToTextService");
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldAcceptWavContentType()
    {
        var audioData = new byte[] { 0x52, 0x49, 0x46, 0x46 };

        var result = await _sut.TranscribeAudioAsync(audioData, "audio/wav", "en-US");

        result.Should().NotBeNull();
        result!.RawTranscript.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldAcceptMp4ContentType()
    {
        var audioData = new byte[] { 0x00, 0x00, 0x00 };

        var result = await _sut.TranscribeAudioAsync(audioData, "audio/mp4", "es-MX");

        result.Should().NotBeNull();
        result!.RawTranscript.Should().NotBeNullOrWhiteSpace();
    }
}
