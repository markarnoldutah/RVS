using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class AzureWhisperSpeechToTextServiceTests
{
    private readonly Mock<ILogger<AzureWhisperSpeechToTextService>> _loggerMock = new();

    private static readonly byte[] SampleAudio = [0x52, 0x49, 0x46, 0x46, 0x00, 0x01];

    // ── Guard clause tests ────────────────────────────────────────────────

    [Fact]
    public async Task TranscribeAudioAsync_WhenAudioDataIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, new { text = "Hello." }));

        var act = () => sut.TranscribeAudioAsync(null!, "audio/wav", "en-US");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task TranscribeAudioAsync_WhenContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, new { text = "Hello." }));

        var act = () => sut.TranscribeAudioAsync(SampleAudio, contentType!, "en-US");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task TranscribeAudioAsync_WhenLocaleIsNullOrWhiteSpace_ShouldThrowArgumentException(string? locale)
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, new { text = "Hello." }));

        var act = () => sut.TranscribeAudioAsync(SampleAudio, "audio/wav", locale!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Happy path tests ──────────────────────────────────────────────────

    [Fact]
    public async Task TranscribeAudioAsync_WhenApiReturnsSuccessWithTranscript_ShouldReturnResult()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            new { text = "My air conditioner stopped working." }));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().NotBeNull();
        result!.RawTranscript.Should().Be("My air conditioner stopped working.");
        result.Provider.Should().Be("AzureWhisperSpeechToTextService");
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenApiReturnsSuccess_CleanedDescriptionShouldBeNull()
    {
        // CleanedDescription is left null — IIssueTextRefinementService handles the cleanup step.
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            new { text = "Water heater is broken." }));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/webm", "en-US");

        result.Should().NotBeNull();
        result!.CleanedDescription.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenApiReturnsSuccess_ConfidenceShouldBeZero()
    {
        // Whisper JSON response format doesn't include per-result confidence; defaults to 0.0.
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            new { text = "Slide-out won't retract." }));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/webm", "en-US");

        result.Should().NotBeNull();
        result!.Confidence.Should().Be(0.0);
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldAcceptWebmContentType()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            new { text = "Slide-out won't retract." }));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/webm", "en-US");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldAcceptNonEnglishLocale()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            new { text = "El calentador no funciona." }));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "es-MX");

        result.Should().NotBeNull();
        result!.RawTranscript.Should().Be("El calentador no funciona.");
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldTrimWhitespaceFromTranscript()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            new { text = "  My awning is stuck.  " }));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().NotBeNull();
        result!.RawTranscript.Should().Be("My awning is stuck.");
    }

    // ── Empty / null response tests ───────────────────────────────────────

    [Fact]
    public async Task TranscribeAudioAsync_WhenTextIsEmpty_ShouldReturnNull()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            new { text = "" }));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenTextIsWhitespace_ShouldReturnNull()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            new { text = "   " }));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenTextIsNull_ShouldReturnNull()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            new { text = (string?)null }));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    // ── HTTP error tests ──────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task TranscribeAudioAsync_WhenApiReturnsErrorStatusCode_ShouldReturnNull(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("""{"error":{"message":"bad request"}}""", Encoding.UTF8, "application/json")
        };
        var sut = CreateService(response);

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    // ── Network / timeout / parse error tests ─────────────────────────────

    [Fact]
    public async Task TranscribeAudioAsync_WhenNetworkFails_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://test.openai.azure.com/openai/deployments/whisper/") };
        var sut = new AzureWhisperSpeechToTextService(httpClient, _loggerMock.Object);

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenRequestTimesOut_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://test.openai.azure.com/openai/deployments/whisper/") };
        var sut = new AzureWhisperSpeechToTextService(httpClient, _loggerMock.Object);

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenResponseBodyIsMalformedJson_ShouldReturnNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json {{{", Encoding.UTF8, "application/json")
        };
        var sut = CreateService(response);

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenResponseBodyIsNull_ShouldReturnNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        };
        var sut = CreateService(response);

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    // ── Request format verification ───────────────────────────────────────

    [Fact]
    public async Task TranscribeAudioAsync_ShouldPostToCorrectRelativeUrl()
    {
        Uri? capturedUri = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUri = req.RequestUri)
            .ReturnsAsync(BuildResponse(HttpStatusCode.OK, new { text = "Test." }));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://test.openai.azure.com/openai/deployments/whisper/") };
        var sut = new AzureWhisperSpeechToTextService(httpClient, _loggerMock.Object);

        await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        capturedUri.Should().NotBeNull();
        capturedUri!.PathAndQuery.Should().Contain("audio/transcriptions");
        capturedUri.Query.Should().Contain("api-version=");
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldSendMultipartFormData()
    {
        HttpContent? capturedContent = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedContent = req.Content)
            .ReturnsAsync(BuildResponse(HttpStatusCode.OK, new { text = "Test." }));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://test.openai.azure.com/openai/deployments/whisper/") };
        var sut = new AzureWhisperSpeechToTextService(httpClient, _loggerMock.Object);

        await sut.TranscribeAudioAsync(SampleAudio, "audio/webm", "en-US");

        capturedContent.Should().BeOfType<MultipartFormDataContent>();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private AzureWhisperSpeechToTextService CreateService(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://test.openai.azure.com/openai/deployments/whisper/")
        };
        return new AzureWhisperSpeechToTextService(httpClient, _loggerMock.Object);
    }

    private static HttpResponseMessage BuildResponse(HttpStatusCode statusCode, object body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
    }
}
