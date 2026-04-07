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
    public async Task SuggestDiagnosticQuestionsAsync_WhenApiSucceeds_ShouldReturnStructuredQuestions()
    {
        var chatResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(new
                        {
                            questions = new[]
                            {
                                new { question_text = "Is the battery new?", options = new[] { "Yes", "No" }, allow_free_text = true, help_text = (string?)null },
                                new { question_text = "When did it start?", options = new[] { "Today", "This week" }, allow_free_text = true, help_text = "Approximate timing helps." }
                            },
                            smart_suggestion = "Check the battery terminals for corrosion."
                        })
                    }
                }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(chatResponse), Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var sut = CreateService(response);
        var result = await sut.SuggestDiagnosticQuestionsAsync("Electrical", "Battery won't charge");

        result.Questions.Should().HaveCount(2);
        result.Questions[0].QuestionText.Should().Be("Is the battery new?");
        result.Questions[0].Options.Should().Contain("Yes");
        result.Questions[1].HelpText.Should().Be("Approximate timing helps.");
        result.SmartSuggestion.Should().Be("Check the battery terminals for corrosion.");
        result.Provider.Should().Be(nameof(AzureOpenAiCategorizationService));
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

        result.Questions.Should().HaveCount(3);
        result.Questions[0].QuestionText.Should().Contain("12V DC or 120V AC");
        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenApiReturnsEmptyContent_ShouldFallBackToRuleBased()
    {
        var chatResponse = new
        {
            choices = new[]
            {
                new { message = new { content = "" } }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(chatResponse), Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var sut = CreateService(response);
        var result = await sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.Questions.Should().HaveCount(3);
        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenApiReturnsNonSuccessStatus_ShouldFallBackToRuleBased()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error")
        };

        var sut = CreateService(response);
        var result = await sut.SuggestDiagnosticQuestionsAsync("Plumbing");

        result.Questions.Should().HaveCount(3);
        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenApiReturnsInvalidJson_ShouldFallBackToRuleBased()
    {
        var chatResponse = new
        {
            choices = new[]
            {
                new { message = new { content = "not valid json {{{" } }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(chatResponse), Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var sut = CreateService(response);
        var result = await sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.Questions.Should().HaveCount(3);
        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
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
