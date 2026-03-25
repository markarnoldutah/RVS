using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class NhtsaVinDecoderClientTests
{
    private readonly Mock<ILogger<NhtsaVinDecoderClient>> _loggerMock = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DecodeVinAsync_WhenVinIsNullOrWhiteSpace_ShouldThrowArgumentException(string? vin)
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK));
        var act = () => client.DecodeVinAsync(vin!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturnsValidData_ShouldReturnVinDecoderResult()
    {
        var json = """
        {
            "Results": [{
                "Make": "Grand Design",
                "Model": "Momentum 395MS",
                "ModelYear": "2024",
                "Manufacturer": "Grand Design RV"
            }]
        }
        """;

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var client = CreateClient(httpResponse);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().NotBeNull();
        result!.Vin.Should().Be("1RGDE4428R1000001");
        result.Manufacturer.Should().Be("Grand Design");
        result.Model.Should().Be("Momentum 395MS");
        result.Year.Should().Be(2024);
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturnsNoResults_ShouldReturnNull()
    {
        var json = """{"Results": []}""";
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var client = CreateClient(httpResponse);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturnsEmptyMake_ShouldReturnNull()
    {
        var json = """{"Results": [{"Make": "", "Model": "", "ModelYear": ""}]}""";
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var client = CreateClient(httpResponse);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenHttpRequestFails_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/api/")
        };

        var client = new NhtsaVinDecoderClient(httpClient, _loggerMock.Object);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenTaskCanceled_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/api/")
        };

        var client = new NhtsaVinDecoderClient(httpClient, _loggerMock.Object);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().BeNull();
    }

    private NhtsaVinDecoderClient CreateClient(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/api/")
        };

        return new NhtsaVinDecoderClient(httpClient, _loggerMock.Object);
    }
}
