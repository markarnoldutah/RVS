using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    public async Task CategorizeAsync_WhenApiReturnsOutOfVocabularyText_ShouldFallBackToRuleBased()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Transmission", Encoding.UTF8, new MediaTypeHeaderValue("text/plain"))
        };

        var sut = CreateService(response);
        var result = await sut.CategorizeAsync("The battery is dead");

        // "Transmission" is not in the controlled vocabulary → keyword fallback wins.
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
        var sut = new AzureOpenAiCategorizationService(httpClient, httpClient, _fallback, GptFourOptions(), _loggerMock.Object);

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
        var sut = new AzureOpenAiCategorizationService(httpClient, httpClient, _fallback, GptFourOptions(), _loggerMock.Object);

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
        var sut = new AzureOpenAiCategorizationService(httpClient, httpClient, _fallback, GptFourOptions(), _loggerMock.Object);

        var result = await sut.SuggestDiagnosticQuestionsAsync("Electrical");

        result.Questions.Should().HaveCountGreaterThanOrEqualTo(2).And.HaveCountLessThanOrEqualTo(4);
        result.Questions[0].QuestionText.Should().Contain("volt");
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

        result.Questions.Should().HaveCountGreaterThanOrEqualTo(2).And.HaveCountLessThanOrEqualTo(4);
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

        result.Questions.Should().HaveCountGreaterThanOrEqualTo(2).And.HaveCountLessThanOrEqualTo(4);
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

        result.Questions.Should().HaveCountGreaterThanOrEqualTo(2).And.HaveCountLessThanOrEqualTo(4);
        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
    }

    // ── Question generation on its own deployment (issue #783) ─────────────

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenReasoningModel_ShouldSendReasoningRequestShape()
    {
        // gpt-5 rejects "max_tokens" and a non-default "temperature" with a 400, which this
        // service would treat as an ordinary failure and answer from the question bank.
        var sut = CreateQuestionsService(QuestionsResponse(), reasoningModel: true);

        await sut.SuggestDiagnosticQuestionsAsync("Slides", "Living room slide won't come in");

        var body = JsonNode.Parse(_capturedQuestionsBody!)!.AsObject();
        body.ContainsKey("max_tokens").Should().BeFalse();
        body.ContainsKey("temperature").Should().BeFalse();
        body["max_completion_tokens"]!.GetValue<int>().Should().BeGreaterThan(800);
        body["reasoning_effort"]!.GetValue<string>().Should().Be("minimal");
        body["response_format"]!["type"]!.GetValue<string>().Should().Be("json_object");
        _capturedQuestionsUri!.Query.Should().Contain("api-version=2025-04-01-preview");
    }

    [Theory]
    [InlineData("low", "low")]
    [InlineData("LOW", "low")]
    [InlineData("minimal", "minimal")]
    [InlineData("medium", "minimal")]
    [InlineData("", "minimal")]
    [InlineData(null, "minimal")]
    public async Task SuggestDiagnosticQuestionsAsync_WhenReasoningModel_ShouldSendConfiguredEffortOrMinimal(
        string? configured, string expected)
    {
        // Only minimal and low are allowed: anything slower keeps the customer waiting at step 6.
        var sut = CreateQuestionsService(QuestionsResponse(), reasoningModel: true, reasoningEffort: configured);

        await sut.SuggestDiagnosticQuestionsAsync("Slides");

        var body = JsonNode.Parse(_capturedQuestionsBody!)!.AsObject();
        body["reasoning_effort"]!.GetValue<string>().Should().Be(expected);
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenReasoningModel_ShouldCallTheQuestionsClientOnly()
    {
        var sut = CreateQuestionsService(QuestionsResponse(), reasoningModel: true);

        var result = await sut.SuggestDiagnosticQuestionsAsync("Slides");

        _capturedQuestionsUri!.AbsolutePath.Should().StartWith("/openai/deployments/gpt-5/");
        _textClientCalls.Should().Be(0);
        result.Provider.Should().Be(nameof(AzureOpenAiCategorizationService));
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenNotReasoningModel_ShouldKeepTheGpt4oRequestShape()
    {
        // No questions deployment configured: exactly the request step 6 sends today.
        var sut = CreateQuestionsService(QuestionsResponse(), reasoningModel: false);

        await sut.SuggestDiagnosticQuestionsAsync("Slides");

        var body = JsonNode.Parse(_capturedQuestionsBody!)!.AsObject();
        body["max_tokens"]!.GetValue<int>().Should().Be(800);
        body.ContainsKey("max_completion_tokens").Should().BeFalse();
        body.ContainsKey("reasoning_effort").Should().BeFalse();
        _capturedQuestionsUri!.Query.Should().Contain("api-version=2024-10-21");
    }

    [Fact]
    public async Task CategorizeAsync_WhenQuestionsUseReasoningModel_ShouldStillCallTheTextClient()
    {
        // Only question generation moves; category suggestion stays on gpt-4o.
        var sut = CreateQuestionsService(QuestionsResponse(), reasoningModel: true);

        await sut.CategorizeAsync("The battery is dead");

        _textClientCalls.Should().Be(1);
        _capturedQuestionsUri.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task SuggestDiagnosticQuestionsAsync_WhenReasoningModelRefuses_ShouldFallBackToQuestionBank(HttpStatusCode status)
    {
        var sut = CreateQuestionsService(new HttpResponseMessage(status) { Content = new StringContent("{}") }, reasoningModel: true);

        var result = await sut.SuggestDiagnosticQuestionsAsync("Slides");

        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
        result.Questions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenReasoningModelTimesOut_ShouldFallBackToQuestionBank()
    {
        var questionsHandler = new Mock<HttpMessageHandler>();
        questionsHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Attempt timed out"));
        var sut = CreateQuestionsService(questionsHandler, reasoningModel: true);

        var result = await sut.SuggestDiagnosticQuestionsAsync("Slides");

        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
        result.Questions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SuggestDiagnosticQuestionsAsync_WhenReasoningModelTruncatesReply_ShouldFallBackToQuestionBank()
    {
        // Reasoning tokens count against max_completion_tokens; running out leaves empty content.
        var body = new { choices = new[] { new { message = new { content = "" }, finish_reason = "length" } } };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        var sut = CreateQuestionsService(response, reasoningModel: true);

        var result = await sut.SuggestDiagnosticQuestionsAsync("Slides");

        result.Provider.Should().Be(nameof(RuleBasedCategorizationService));
    }

    private string? _capturedQuestionsBody;
    private Uri? _capturedQuestionsUri;
    private int _textClientCalls;

    private static HttpResponseMessage QuestionsResponse()
    {
        var content = JsonSerializer.Serialize(new
        {
            questions = new[]
            {
                new { question_text = "Does the motor hum, click, or stay silent?", options = new[] { "Hums", "Clicks", "Silent" }, allow_free_text = true, help_text = (string?)null },
            },
            smart_suggestion = (string?)null,
        });
        var body = new { choices = new[] { new { message = new { content } } } };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
    }

    private AzureOpenAiCategorizationService CreateQuestionsService(
        HttpResponseMessage response, bool reasoningModel, string? reasoningEffort = null)
    {
        var questionsHandler = new Mock<HttpMessageHandler>();
        questionsHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage message, CancellationToken _) =>
            {
                _capturedQuestionsUri = message.RequestUri;
                _capturedQuestionsBody = message.Content is null ? null : await message.Content.ReadAsStringAsync();
                return response;
            });

        return CreateQuestionsService(questionsHandler, reasoningModel, reasoningEffort);
    }

    private AzureOpenAiCategorizationService CreateQuestionsService(
        Mock<HttpMessageHandler> questionsHandler, bool reasoningModel, string? reasoningEffort = null)
    {
        var textHandler = new Mock<HttpMessageHandler>();
        textHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                _textClientCalls++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Electrical", Encoding.UTF8, new MediaTypeHeaderValue("text/plain")),
                };
            });

        var textClient = new HttpClient(textHandler.Object) { BaseAddress = new Uri("https://openai.example.com/openai/deployments/gpt-4o/") };
        var questionsClient = new HttpClient(questionsHandler.Object) { BaseAddress = new Uri("https://openai.example.com/openai/deployments/gpt-5/") };
        var options = Microsoft.Extensions.Options.Options.Create(new AzureOpenAiQuestionsOptions
        {
            UseReasoningModelRequest = reasoningModel,
            ReasoningEffort = reasoningEffort,
        });
        return new AzureOpenAiCategorizationService(textClient, questionsClient, _fallback, options, _loggerMock.Object);
    }

    private static IOptions<AzureOpenAiQuestionsOptions> GptFourOptions() =>
        Microsoft.Extensions.Options.Options.Create(new AzureOpenAiQuestionsOptions());

    private AzureOpenAiCategorizationService CreateService(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        return new AzureOpenAiCategorizationService(httpClient, httpClient, _fallback, GptFourOptions(), _loggerMock.Object);
    }
}
