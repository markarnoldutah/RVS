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

public class AzureOpenAiCategorizationServiceTests
{
    private readonly Mock<ILogger<AzureOpenAiCategorizationService>> _loggerMock = new();
    private readonly RuleBasedCategorizationService _fallback = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CategorizeAsync_WhenDescriptionIsNullOrWhiteSpace_ShouldThrowArgumentException(string? description)
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.OK));
        var act = () => sut.CategorizeAsync(description!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CategorizeAsync_WhenApiSucceeds_ShouldReturnAiCategory()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Electrical", Encoding.UTF8, new MediaTypeHeaderValue("text/plain"))
        };

        var sut = CreateService(response);
        var result = await sut.CategorizeAsync("The battery is dead");

        result.Should().Be("Electrical");
    }

    [Fact]
    public async Task CategorizeAsync_WhenApiFails_ShouldFallBackToRuleBased()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        var sut = new AzureOpenAiCategorizationService(httpClient, _fallback, _loggerMock.Object);

        var result = await sut.CategorizeAsync("The battery is dead");

        result.Should().Be("Electrical");
    }

    [Fact]
    public async Task CategorizeAsync_WhenApiTimesOut_ShouldFallBackToRuleBased()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        var sut = new AzureOpenAiCategorizationService(httpClient, _fallback, _loggerMock.Object);

        var result = await sut.CategorizeAsync("Water leak under the sink");

        result.Should().Be("Plumbing");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SuggestDiagnosticQuestionsAsync_WhenCategoryIsNullOrWhiteSpace_ShouldThrowArgumentException(string? category)
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.OK));
        var act = () => sut.SuggestDiagnosticQuestionsAsync(category!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenApiSucceeds_ShouldReturnQuestions()
    {
        var questions = new List<string> { "Q1?", "Q2?", "Q3?" };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(questions), Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var sut = CreateService(response);
        var result = await sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.Should().HaveCount(3);
        result[0].Should().Be("Q1?");
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenApiFails_ShouldFallBackToRuleBased()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        var sut = new AzureOpenAiCategorizationService(httpClient, _fallback, _loggerMock.Object);

        var result = await sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.Should().HaveCount(3);
        result[0].Should().Contain("12V DC or 120V AC");
    }

    private AzureOpenAiCategorizationService CreateService(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        return new AzureOpenAiCategorizationService(httpClient, _fallback, _loggerMock.Object);
    }
}
