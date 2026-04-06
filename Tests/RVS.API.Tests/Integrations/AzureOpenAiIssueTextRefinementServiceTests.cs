using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class AzureOpenAiIssueTextRefinementServiceTests
{
    private readonly Mock<ILogger<AzureOpenAiIssueTextRefinementService>> _loggerMock = new();

    // ── RefineTranscriptAsync ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task RefineTranscriptAsync_WhenRawTranscriptIsNullOrWhiteSpace_ShouldThrowArgumentException(string? rawTranscript)
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, new { }));

        var act = () => sut.RefineTranscriptAsync(rawTranscript!, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RefineTranscriptAsync_WhenApiReturnsCleanedDescription_ShouldReturnRefinementResult()
    {
        var payload = BuildChatResponse("{\"cleaned_description\": \"My water heater stopped working.\", \"confidence\": 0.92}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.RefineTranscriptAsync("um my water heater stopped working", null);

        result.Should().NotBeNull();
        result!.CleanedDescription.Should().Be("My water heater stopped working.");
        result.Confidence.Should().Be(0.92);
        result.Provider.Should().Be("AzureOpenAiIssueTextRefinementService");
    }

    [Fact]
    public async Task RefineTranscriptAsync_WhenIssueCategoryProvided_ShouldReturnRefinementResult()
    {
        var payload = BuildChatResponse("{\"cleaned_description\": \"The water heater is not heating.\", \"confidence\": 0.95}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.RefineTranscriptAsync("uh the water heater is not heating", "Plumbing");

        result.Should().NotBeNull();
        result!.CleanedDescription.Should().Be("The water heater is not heating.");
    }

    [Fact]
    public async Task RefineTranscriptAsync_WhenApiReturnsEmptyContent_ShouldReturnNull()
    {
        var payload = BuildChatResponse(string.Empty);
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.RefineTranscriptAsync("um my fridge is broken", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefineTranscriptAsync_WhenApiReturnsNullCleanedDescription_ShouldReturnNull()
    {
        var payload = BuildChatResponse("{\"cleaned_description\": null, \"confidence\": 0.0}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.RefineTranscriptAsync("um my fridge is broken", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefineTranscriptAsync_WhenApiReturnsErrorStatusCode_ShouldReturnNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\": \"Service unavailable\"}", Encoding.UTF8, "application/json")
        };
        var sut = CreateService(response);

        var result = await sut.RefineTranscriptAsync("my slide won't extend", null);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task RefineTranscriptAsync_WhenApiReturnsNonSuccessStatusCode_ShouldReturnNull(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                """{"error":{"code":"DeploymentNotFound","message":"The API deployment does not exist."}}""",
                Encoding.UTF8,
                "application/json")
        };
        var sut = CreateService(response);

        var result = await sut.RefineTranscriptAsync("my slide won't extend", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefineTranscriptAsync_WhenApiFails_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        var sut = new AzureOpenAiIssueTextRefinementService(httpClient, _loggerMock.Object);

        var result = await sut.RefineTranscriptAsync("my fridge is broken", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefineTranscriptAsync_WhenApiTimesOut_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        var sut = new AzureOpenAiIssueTextRefinementService(httpClient, _loggerMock.Object);

        var result = await sut.RefineTranscriptAsync("my fridge is broken", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefineTranscriptAsync_WhenResponseJsonIsMalformed_ShouldReturnNull()
    {
        var payload = BuildChatResponse("not valid json {{{{");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.RefineTranscriptAsync("my fridge is broken", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefineTranscriptAsync_ShouldTrimCleanedDescription()
    {
        var payload = BuildChatResponse("{\"cleaned_description\": \"  My water heater stopped working.  \", \"confidence\": 0.9}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.RefineTranscriptAsync("my water heater stopped working", null);

        result.Should().NotBeNull();
        result!.CleanedDescription.Should().Be("My water heater stopped working.");
    }

    // ── SuggestCategoryAsync ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SuggestCategoryAsync_WhenDescriptionIsNullOrWhiteSpace_ShouldThrowArgumentException(string? description)
    {
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, new { }));

        var act = () => sut.SuggestCategoryAsync(description!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenApiReturnsValidCategory_ShouldReturnCategorySuggestion()
    {
        var payload = BuildChatResponse("{\"category\": \"Electrical\", \"confidence\": 0.88}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.SuggestCategoryAsync("The battery is dead and the lights don't work");

        result.Should().NotBeNull();
        result!.IssueCategory.Should().Be("Electrical");
        result.Confidence.Should().Be(0.88);
        result.Provider.Should().Be("AzureOpenAiIssueTextRefinementService");
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenApiReturnsNullCategory_ShouldReturnResultWithNullCategory()
    {
        var payload = BuildChatResponse("{\"category\": null, \"confidence\": 0.0}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.SuggestCategoryAsync("Something is generally wrong");

        result.Should().NotBeNull();
        result!.IssueCategory.Should().BeNull();
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenApiReturnsUnknownCategory_ShouldReturnNullCategory()
    {
        var payload = BuildChatResponse("{\"category\": \"UnknownCategory\", \"confidence\": 0.7}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.SuggestCategoryAsync("The battery is dead");

        result.Should().NotBeNull();
        result!.IssueCategory.Should().BeNull(because: "unknown categories should be rejected");
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenApiReturnsCategoryWithWrongCase_ShouldNormalizeToCanonicalCase()
    {
        var payload = BuildChatResponse("{\"category\": \"electrical\", \"confidence\": 0.85}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.SuggestCategoryAsync("The battery is dead");

        result.Should().NotBeNull();
        result!.IssueCategory.Should().Be("Electrical", because: "category casing should be normalized to the canonical value");
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenApiReturnsEmptyContent_ShouldReturnNull()
    {
        var payload = BuildChatResponse(string.Empty);
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.SuggestCategoryAsync("My fridge is not cooling");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task SuggestCategoryAsync_WhenApiReturnsNonSuccessStatusCode_ShouldReturnNull(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                """{"error":{"code":"DeploymentNotFound","message":"The API deployment does not exist."}}""",
                Encoding.UTF8,
                "application/json")
        };
        var sut = CreateService(response);

        var result = await sut.SuggestCategoryAsync("The battery is dead");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenApiFails_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        var sut = new AzureOpenAiIssueTextRefinementService(httpClient, _loggerMock.Object);

        var result = await sut.SuggestCategoryAsync("The battery is dead");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenApiTimesOut_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        var sut = new AzureOpenAiIssueTextRefinementService(httpClient, _loggerMock.Object);

        var result = await sut.SuggestCategoryAsync("Water leak under the sink");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestCategoryAsync_WhenResponseJsonIsMalformed_ShouldReturnNull()
    {
        var payload = BuildChatResponse("not valid json {{{{");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.SuggestCategoryAsync("Water leak under the sink");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Electrical")]
    [InlineData("Plumbing")]
    [InlineData("HVAC")]
    [InlineData("Appliance")]
    [InlineData("Structural")]
    [InlineData("Slide-Out")]
    [InlineData("Awning")]
    public async Task SuggestCategoryAsync_WhenApiReturnsKnownCategory_ShouldReturnThatCategory(string category)
    {
        var payload = BuildChatResponse($"{{\"category\": \"{category}\", \"confidence\": 0.85}}");
        var sut = CreateService(BuildResponse(HttpStatusCode.OK, payload));

        var result = await sut.SuggestCategoryAsync("Some issue description");

        result.Should().NotBeNull();
        result!.IssueCategory.Should().Be(category);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private AzureOpenAiIssueTextRefinementService CreateService(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://openai.example.com/") };
        return new AzureOpenAiIssueTextRefinementService(httpClient, _loggerMock.Object);
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
