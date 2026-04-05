using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class AzureOpenAiVinExtractionServiceTests
{
    private readonly Mock<ILogger<AzureOpenAiVinExtractionService>> _loggerMock = new();

    private static readonly byte[] SampleJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenImageDataIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, new { }));

        var act = () => sut.ExtractVinFromImageAsync(null!, "image/jpeg");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ExtractVinFromImageAsync_WhenContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, new { }));

        var act = () => sut.ExtractVinFromImageAsync(SampleJpeg, contentType!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenApiReturnsValidVin_ShouldReturnExtractionResult()
    {
        var payload = BuildChatResponse("{\"vin\": \"1RGDE4428R1000001\", \"confidence\": 0.95}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.ExtractVinFromImageAsync(SampleJpeg, "image/jpeg");

        result.Should().NotBeNull();
        result!.Vin.Should().Be("1RGDE4428R1000001");
        result.Confidence.Should().Be(0.95);
        result.Provider.Should().Be("AzureOpenAiVinExtractionService");
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenApiReturnsNullVin_ShouldReturnNull()
    {
        var payload = BuildChatResponse("{\"vin\": null, \"confidence\": 0.0}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.ExtractVinFromImageAsync(SampleJpeg, "image/jpeg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenApiReturnsInvalidVinFormat_ShouldReturnNull()
    {
        var payload = BuildChatResponse("{\"vin\": \"INVALID\", \"confidence\": 0.7}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.ExtractVinFromImageAsync(SampleJpeg, "image/jpeg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenApiReturnsEmptyContent_ShouldReturnNull()
    {
        var payload = BuildChatResponse(string.Empty);
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.ExtractVinFromImageAsync(SampleJpeg, "image/jpeg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenApiFails_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        var sut = new AzureOpenAiVinExtractionService(httpClient, _loggerMock.Object);

        var result = await sut.ExtractVinFromImageAsync(SampleJpeg, "image/jpeg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenApiTimesOut_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        var sut = new AzureOpenAiVinExtractionService(httpClient, _loggerMock.Object);

        var result = await sut.ExtractVinFromImageAsync(SampleJpeg, "image/jpeg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenResponseJsonIsMalformed_ShouldReturnNull()
    {
        var payload = BuildChatResponse("not valid json {{{{");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.ExtractVinFromImageAsync(SampleJpeg, "image/jpeg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenVinContainsForbiddenChars_ShouldReturnNull()
    {
        // VIN with 'I', 'O', 'Q' are invalid per VinValidator
        var payload = BuildChatResponse("{\"vin\": \"1RGDE4428R100000I\", \"confidence\": 0.9}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.ExtractVinFromImageAsync(SampleJpeg, "image/jpeg");

        result.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private AzureOpenAiVinExtractionService CreateService(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        return new AzureOpenAiVinExtractionService(httpClient, _loggerMock.Object);
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

    /// <summary>Wraps <paramref name="content"/> in an Azure OpenAI chat completion response envelope.</summary>
    private static object BuildChatResponse(string content)
    {
        return new
        {
            choices = new[]
            {
                new
                {
                    message = new { role = "assistant", content }
                }
            }
        };
    }
}
