using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RVS.API.Integrations;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Tests for <see cref="AzureOpenAiPreliminaryAssessmentService"/> — one structured-output chat
/// completion per packet that produces a probable cause, possible fixes and likely parts, with
/// the rule-based table as the fallback (issue #507).
/// </summary>
public class AzureOpenAiPreliminaryAssessmentServiceTests
{
    private readonly RuleBasedPreliminaryAssessmentService _fallback = new();
    private string? _capturedRequestBody;
    private Uri? _capturedRequestUri;

    private static ServiceRequest Request() => new()
    {
        Id = "sr_1",
        TenantId = "ten_1",
        IssueCategory = "Slides",
        IssueDescription = "Living room slide won't come in.",
        AssetInfo = new AssetInfoEmbedded { AssetId = "1HGBH41JXMN109186", Manufacturer = "Jayco", Model = "Eagle", Year = 2021 },
        DiagnosticResponses =
        [
            new DiagnosticResponseEmbedded
            {
                QuestionText = "Does the slide move at all when you operate the switch?",
                SelectedOptions = ["Motor hums or clicks but nothing moves"],
                FreeTextResponse = "Started after a storm",
            },
        ],
    };

    private static HttpResponseMessage ChatResponse(object assessmentPayload) =>
        ChatResponseWithContent(JsonSerializer.Serialize(assessmentPayload));

    private static HttpResponseMessage ChatResponseWithContent(string? content)
    {
        var body = new { choices = new[] { new { message = new { content } } } };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
    }

    private AzureOpenAiPreliminaryAssessmentService CreateService(HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage message, CancellationToken _) =>
            {
                _capturedRequestUri = message.RequestUri;
                _capturedRequestBody = message.Content is null ? null : await message.Content.ReadAsStringAsync();
                return response;
            });

        return CreateService(handler);
    }

    private AzureOpenAiPreliminaryAssessmentService CreateService(Mock<HttpMessageHandler> handler)
    {
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://openai.example.com/openai/deployments/gpt-4o/") };
        return new AzureOpenAiPreliminaryAssessmentService(
            httpClient, _fallback, Mock.Of<ILogger<AzureOpenAiPreliminaryAssessmentService>>());
    }

    private AzureOpenAiPreliminaryAssessmentService CreateThrowingService(Exception exception)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);
        return CreateService(handler);
    }

    // ── Guard clause ───────────────────────────────────────────────────────

    [Fact]
    public async Task AssessAsync_WhenServiceRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.OK));

        var act = () => sut.AssessAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AssessAsync_WhenApiSucceeds_ShouldReturnTheStructuredAssessment()
    {
        var sut = CreateService(ChatResponse(new
        {
            probable_cause = "Slide motor stalled or its 12V supply is dropping under load.",
            possible_fixes = new[] { "Check battery voltage and the slide fuse", "Replace the slide motor" },
            likely_parts = new[] { "Slide-out motor", "Slide fuse" },
            confidence = "medium",
        }));

        var result = await sut.AssessAsync(Request());

        result.ProbableCause.Should().Be("Slide motor stalled or its 12V supply is dropping under load.");
        result.PossibleFixes.Should().Equal("Check battery voltage and the slide fuse", "Replace the slide motor");
        result.LikelyParts.Should().Equal("Slide-out motor", "Slide fuse");
        result.Confidence.Should().Be(AssessmentConfidence.Medium);
        result.Provider.Should().Be(nameof(AzureOpenAiPreliminaryAssessmentService));
        result.GeneratedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AssessAsync_ShouldPostAJsonModeChatCompletion_GroundedOnTheIntake()
    {
        var sut = CreateService(ChatResponse(new { probable_cause = "x", possible_fixes = new[] { "y" }, likely_parts = Array.Empty<string>(), confidence = "low" }));

        await sut.AssessAsync(Request());

        _capturedRequestUri!.AbsoluteUri.Should().Contain("/chat/completions?api-version=");
        var body = JsonNode.Parse(_capturedRequestBody!)!;
        body["response_format"]!["type"]!.GetValue<string>().Should().Be("json_object");
        body["max_tokens"]!.GetValue<int>().Should().BeLessThanOrEqualTo(600);

        var userMessage = body["messages"]!.AsArray()
            .Single(m => m!["role"]!.GetValue<string>() == "user")!["content"]!.GetValue<string>();
        userMessage.Should().Contain("Slides");
        userMessage.Should().Contain("2021 Jayco Eagle");
        userMessage.Should().Contain("Living room slide won't come in.");
        userMessage.Should().Contain("Does the slide move at all when you operate the switch?");
        userMessage.Should().Contain("Motor hums or clicks but nothing moves");
        userMessage.Should().Contain("Started after a storm");
    }

    [Fact]
    public async Task AssessAsync_ShouldAskForPossibleFixes_NeverARecommendedFix()
    {
        var sut = CreateService(ChatResponse(new { probable_cause = "x", possible_fixes = new[] { "y" }, likely_parts = Array.Empty<string>(), confidence = "low" }));

        await sut.AssessAsync(Request());

        var systemPrompt = JsonNode.Parse(_capturedRequestBody!)!["messages"]!.AsArray()
            .Single(m => m!["role"]!.GetValue<string>() == "system")!["content"]!.GetValue<string>();
        systemPrompt.Should().Contain("possible_fixes");
        systemPrompt.ToLowerInvariant().Should().NotContain("recommended_fix");
    }

    [Fact]
    public async Task AssessAsync_ShouldIncludeCategorySpecificFailurePatterns_InTheSystemPrompt()
    {
        var sut = CreateService(ChatResponse(new { probable_cause = "x", possible_fixes = new[] { "y" }, likely_parts = Array.Empty<string>(), confidence = "low" }));

        await sut.AssessAsync(Request());

        var systemPrompt = JsonNode.Parse(_capturedRequestBody!)!["messages"]!.AsArray()
            .Single(m => m!["role"]!.GetValue<string>() == "system")!["content"]!.GetValue<string>();
        systemPrompt.Should().Contain("Slides:");
        systemPrompt.Should().Contain("Electrical:");
        systemPrompt.Should().Contain("Plumbing & Water:");
        systemPrompt.Should().Contain("HVAC:");
        systemPrompt.Should().Contain("Generator:");
        systemPrompt.Should().Contain("LP / Propane:");
        systemPrompt.Should().Contain("Appliances & Refrigerator:");
        systemPrompt.Should().Contain("Roof & Seals:");
        systemPrompt.Should().Contain("Awning:");
        systemPrompt.Should().Contain("Chassis & Running Gear:");
        systemPrompt.Should().Contain("Body & Exterior:");
        systemPrompt.Should().Contain("Interior & Cabinetry:");
    }

    [Fact]
    public async Task AssessAsync_ShouldTrimValues_DropBlanks_AndCapTheLists()
    {
        var sut = CreateService(ChatResponse(new
        {
            probable_cause = "  Cause  ",
            possible_fixes = new[] { " Fix 1 ", "", "Fix 2", "Fix 3", "Fix 4" },
            likely_parts = new[] { "P1", " ", "P2", "P3", "P4", "P5", "P6" },
            confidence = " HIGH ",
        }));

        var result = await sut.AssessAsync(Request());

        result.ProbableCause.Should().Be("Cause");
        result.PossibleFixes.Should().Equal("Fix 1", "Fix 2", "Fix 3");
        result.LikelyParts.Should().Equal("P1", "P2", "P3", "P4", "P5");
        result.Confidence.Should().Be(AssessmentConfidence.High);
    }

    // ── Abstention ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AssessAsync_WhenModelAbstains_ShouldReturnAnEmptyAbstention_NotTheFallback()
    {
        var sut = CreateService(ChatResponse(new
        {
            probable_cause = "Could be anything",
            possible_fixes = new[] { "Inspect it" },
            likely_parts = new[] { "Unknown" },
            confidence = "abstain",
        }));

        var result = await sut.AssessAsync(Request());

        result.Confidence.Should().Be(AssessmentConfidence.Abstain);
        result.ProbableCause.Should().BeNull();
        result.PossibleFixes.Should().BeEmpty();
        result.LikelyParts.Should().BeEmpty();
        result.Provider.Should().Be(nameof(AzureOpenAiPreliminaryAssessmentService));
    }

    [Fact]
    public async Task AssessAsync_WhenConfidenceIsOutsideTheVocabulary_ShouldAbstain()
    {
        var sut = CreateService(ChatResponse(new
        {
            probable_cause = "Cause",
            possible_fixes = new[] { "Fix" },
            likely_parts = new[] { "Part" },
            confidence = "very sure",
        }));

        var result = await sut.AssessAsync(Request());

        result.Confidence.Should().Be(AssessmentConfidence.Abstain);
        result.PossibleFixes.Should().BeEmpty();
    }

    [Fact]
    public async Task AssessAsync_WhenModelOffersAConfidenceButNoContent_ShouldAbstain()
    {
        var sut = CreateService(ChatResponse(new
        {
            probable_cause = " ",
            possible_fixes = Array.Empty<string>(),
            likely_parts = Array.Empty<string>(),
            confidence = "high",
        }));

        var result = await sut.AssessAsync(Request());

        result.Confidence.Should().Be(AssessmentConfidence.Abstain);
    }

    // ── Fallback ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AssessAsync_WhenApiReturnsAnErrorStatus_ShouldFallBackToRuleBased()
    {
        var sut = CreateService(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"error\":\"rate limited\"}"),
        });

        var result = await sut.AssessAsync(Request());

        result.Provider.Should().Be(nameof(RuleBasedPreliminaryAssessmentService));
        result.Confidence.Should().Be(AssessmentConfidence.Low);
    }

    [Fact]
    public async Task AssessAsync_WhenApiThrows_ShouldFallBackToRuleBased()
    {
        var sut = CreateThrowingService(new HttpRequestException("Service unavailable"));

        var result = await sut.AssessAsync(Request());

        result.Provider.Should().Be(nameof(RuleBasedPreliminaryAssessmentService));
    }

    [Fact]
    public async Task AssessAsync_WhenApiTimesOut_ShouldFallBackToRuleBased()
    {
        var sut = CreateThrowingService(new TaskCanceledException("Request timed out"));

        var result = await sut.AssessAsync(Request());

        result.Provider.Should().Be(nameof(RuleBasedPreliminaryAssessmentService));
    }

    [Fact]
    public async Task AssessAsync_WhenCallerCancels_ShouldPropagateTheCancellation()
    {
        var sut = CreateThrowingService(new TaskCanceledException("cancelled"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => sut.AssessAsync(Request(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    public async Task AssessAsync_WhenContentIsEmptyOrUnparseable_ShouldFallBackToRuleBased(string? content)
    {
        var sut = CreateService(ChatResponseWithContent(content));

        var result = await sut.AssessAsync(Request());

        result.Provider.Should().Be(nameof(RuleBasedPreliminaryAssessmentService));
    }
}
