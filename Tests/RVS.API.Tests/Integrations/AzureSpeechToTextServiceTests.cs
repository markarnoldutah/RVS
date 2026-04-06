using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class AzureSpeechToTextServiceTests
{
    private readonly Mock<ILogger<AzureSpeechToTextService>> _loggerMock = new();

    private static readonly byte[] SampleAudio = [0x52, 0x49, 0x46, 0x46, 0x00, 0x01];

    [Fact]
    public async Task TranscribeAudioAsync_WhenAudioDataIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, BuildSpeechResponse("Success", "Hello.")));

        var act = () => sut.TranscribeAudioAsync(null!, "audio/wav", "en-US");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task TranscribeAudioAsync_WhenContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, BuildSpeechResponse("Success", "Hello.")));

        var act = () => sut.TranscribeAudioAsync(SampleAudio, contentType!, "en-US");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task TranscribeAudioAsync_WhenLocaleIsNullOrWhiteSpace_ShouldThrowArgumentException(string? locale)
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, BuildSpeechResponse("Success", "Hello.")));

        var act = () => sut.TranscribeAudioAsync(SampleAudio, "audio/wav", locale!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenApiReturnsSuccessWithTranscript_ShouldReturnResult()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            BuildSpeechResponse("Success", "My air conditioner stopped working.", 0.93)));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().NotBeNull();
        result!.RawTranscript.Should().Be("My air conditioner stopped working.");
        result.Confidence.Should().Be(0.93);
        result.Provider.Should().Be("AzureSpeechToTextService");
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenApiReturnsSuccessWithTranscript_CleanedDescriptionShouldBeNull()
    {
        // CleanedDescription is left null — IIssueTextRefinementService handles the cleanup step.
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            BuildSpeechResponse("Success", "Water heater is broken.", 0.88)));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/webm", "en-US");

        result.Should().NotBeNull();
        result!.CleanedDescription.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenRecognitionStatusIsNoMatch_ShouldReturnNull()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            BuildSpeechResponse("NoMatch", null)));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenRecognitionStatusIsInitialSilenceTimeout_ShouldReturnNull()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            BuildSpeechResponse("InitialSilenceTimeout", null)));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenDisplayTextIsEmpty_ShouldReturnNull()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            BuildSpeechResponse("Success", string.Empty)));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task TranscribeAudioAsync_WhenApiReturnsErrorStatusCode_ShouldReturnNull(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("""{"error":"bad request"}""", Encoding.UTF8, "application/json")
        };
        var sut = CreateService(response);

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_WhenNetworkFails_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://eastus.stt.speech.microsoft.com/") };
        var sut = new AzureSpeechToTextService(httpClient, _loggerMock.Object);

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

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://eastus.stt.speech.microsoft.com/") };
        var sut = new AzureSpeechToTextService(httpClient, _loggerMock.Object);

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
    public async Task TranscribeAudioAsync_WhenNBestIsAbsent_ShouldReturnZeroConfidence()
    {
        // Confidence defaults to 0.0 when NBest array is missing.
        var body = new { RecognitionStatus = "Success", DisplayText = "Hello world." };
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, body));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "en-US");

        result.Should().NotBeNull();
        result!.Confidence.Should().Be(0.0);
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldAcceptWebmContentType()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            BuildSpeechResponse("Success", "Slide-out won't retract.", 0.85)));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/webm", "en-US");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TranscribeAudioAsync_ShouldAcceptNonEnglishLocale()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK,
            BuildSpeechResponse("Success", "El calentador no funciona.", 0.80)));

        var result = await sut.TranscribeAudioAsync(SampleAudio, "audio/wav", "es-MX");

        result.Should().NotBeNull();
        result!.RawTranscript.Should().Be("El calentador no funciona.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private AzureSpeechToTextService CreateService(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://eastus.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1")
        };
        return new AzureSpeechToTextService(httpClient, _loggerMock.Object);
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

    /// <summary>Builds an Azure Cognitive Services Speech recognition response envelope.</summary>
    private static object BuildSpeechResponse(string status, string? displayText, double confidence = 0.9)
    {
        if (displayText is null)
        {
            return new { RecognitionStatus = status };
        }

        var lexical = displayText.ToLowerInvariant();
        return new
        {
            RecognitionStatus = status,
            DisplayText = displayText,
            Offset = 1000000,
            Duration = 55000000,
            NBest = new[]
            {
                new
                {
                    Confidence = confidence,
                    Lexical = lexical,
                    ITN = lexical,
                    MaskedITN = lexical,
                    Display = displayText
                }
            }
        };
    }
}
