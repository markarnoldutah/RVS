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
using RVS.Domain.Integrations;
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
    private readonly List<string> _capturedBodies = [];

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

    private AzureOpenAiPreliminaryAssessmentService CreateService(
        HttpResponseMessage response, bool reasoningModel = false, int maxImages = AzureOpenAiAssessmentOptions.DefaultMaxImages)
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

        return CreateService(handler, reasoningModel, maxImages);
    }

    private AzureOpenAiPreliminaryAssessmentService CreateService(
        Mock<HttpMessageHandler> handler, bool reasoningModel = false, int maxImages = AzureOpenAiAssessmentOptions.DefaultMaxImages)
    {
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://openai.example.com/openai/deployments/gpt-4o/") };
        var options = Microsoft.Extensions.Options.Options.Create(
            new AzureOpenAiAssessmentOptions { UseReasoningModelRequest = reasoningModel, MaxImages = maxImages });
        return new AzureOpenAiPreliminaryAssessmentService(
            httpClient, _fallback, options, Mock.Of<ILogger<AzureOpenAiPreliminaryAssessmentService>>());
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
    public async Task AssessAsync_WhenReasoningModel_ShouldSendMaxCompletionTokensAndReasoningEffort_NotTemperatureOrMaxTokens()
    {
        // The dedicated assessment deployment (gpt-5, #584) is a reasoning model: it rejects
        // "max_tokens" and any non-default "temperature" with a 400, which this service treats as
        // an ordinary failure and silently answers with the rule-based fallback.
        var sut = CreateService(ChatResponse(new { probable_cause = "x", possible_fixes = new[] { "y" }, likely_parts = Array.Empty<string>(), confidence = "low" }), reasoningModel: true);

        await sut.AssessAsync(Request());

        var body = JsonNode.Parse(_capturedRequestBody!)!.AsObject();
        body.ContainsKey("temperature").Should().BeFalse();
        body.ContainsKey("max_tokens").Should().BeFalse();
        body["max_completion_tokens"]!.GetValue<int>().Should().BeGreaterThan(600);
        body["reasoning_effort"]!.GetValue<string>().Should().Be("low");
        _capturedRequestUri!.Query.Should().Contain("api-version=2025-04-01-preview");
    }

    [Fact]
    public async Task AssessAsync_WhenNotReasoningModel_ShouldKeepTheGpt4oRequestShape()
    {
        // Dev, and the documented revert path (blank assessmentModelName), send the assessment to
        // the gpt-4o text deployment, which rejects "reasoning_effort".
        var sut = CreateService(ChatResponse(new { probable_cause = "x", possible_fixes = new[] { "y" }, likely_parts = Array.Empty<string>(), confidence = "low" }));

        await sut.AssessAsync(Request());

        var body = JsonNode.Parse(_capturedRequestBody!)!.AsObject();
        body.ContainsKey("reasoning_effort").Should().BeFalse();
        body.ContainsKey("max_completion_tokens").Should().BeFalse();
        body["max_tokens"]!.GetValue<int>().Should().BeLessThanOrEqualTo(600);
        body["temperature"]!.GetValue<double>().Should().Be(0.2);
        _capturedRequestUri!.Query.Should().Contain("api-version=2024-10-21");
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

        var act = () => sut.AssessAsync(Request(), cancellationToken: cts.Token);

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

    // ── Photos (issue #772) ────────────────────────────────────────────────

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 4, 5, 6];

    private static AssessmentPhoto Photo(string id, string contentType = "image/jpeg", byte[]? bytes = null) =>
        new(id, contentType, bytes ?? Jpeg);

    private static readonly object PhotoFindingsPayload = new
    {
        data_plates = new object[]
        {
            new { component = " Refrigerator ", manufacturer = "Dometic", model_number = "RM2652", serial_number = "12345678", attachment_id = "att_fridge" },
            new { component = "Furnace", manufacturer = "Suburban", model_number = "SF-35", serial_number = (string?)null, attachment_id = "att_not_sent" },
        },
        fault_codes = new object[] { new { component = "Thermostat", code = "E1", meaning = (string?)null, attachment_id = "att_thermostat" } },
        observations = new object[] { new { text = "Water staining on the ceiling panel", attachment_id = "att_thermostat" } },
    };

    /// <summary>Answers each call in turn with the next response, capturing every request body.</summary>
    private AzureOpenAiPreliminaryAssessmentService CreateSequenceService(params Func<HttpResponseMessage>[] responses)
    {
        var call = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage message, CancellationToken _) =>
            {
                _capturedBodies.Add(await message.Content!.ReadAsStringAsync());
                return responses[Math.Min(call++, responses.Length - 1)]();
            });
        return CreateService(handler);
    }

    private static JsonArray UserParts(string body) =>
        JsonNode.Parse(body)!["messages"]!.AsArray()
            .Single(m => m!["role"]!.GetValue<string>() == "user")!["content"]!.AsArray();

    private static string SystemPromptOf(string body) =>
        JsonNode.Parse(body)!["messages"]!.AsArray()
            .Single(m => m!["role"]!.GetValue<string>() == "system")!["content"]!.GetValue<string>();

    private static List<JsonNode> ImageParts(string body) =>
        [.. UserParts(body).Where(p => p!["type"]!.GetValue<string>() == "image_url").Select(p => p!)];

    private static HttpResponseMessage Assessed(object? photoFindings = null, string confidence = "medium") => ChatResponse(new
    {
        probable_cause = "Cooling unit failure.",
        possible_fixes = new[] { "Check the cooling unit" },
        likely_parts = new[] { "Cooling unit" },
        confidence,
        photo_findings = photoFindings,
    });

    [Fact]
    public async Task AssessAsync_WithPhotos_ShouldSendImagesOnly_AsHighDetailDataUris_EachAfterItsAttachmentId()
    {
        var sut = CreateService(Assessed());
        AssessmentPhoto[] photos =
        [
            Photo("att_video", "video/mp4"),
            Photo("att_fridge"),
            Photo("att_voice", "audio/mp4"),
            Photo("att_panel", "image/png", Png),
        ];

        await sut.AssessAsync(Request(), photos);

        var parts = UserParts(_capturedRequestBody!);
        parts[0]!["type"]!.GetValue<string>().Should().Be("text");
        parts[0]!["text"]!.GetValue<string>().Should().Contain("Living room slide won't come in.");

        var images = ImageParts(_capturedRequestBody!);
        images.Should().HaveCount(2);
        images[0]["image_url"]!["url"]!.GetValue<string>().Should().Be($"data:image/jpeg;base64,{Convert.ToBase64String(Jpeg)}");
        images[1]["image_url"]!["url"]!.GetValue<string>().Should().Be($"data:image/png;base64,{Convert.ToBase64String(Png)}");
        images.Should().OnlyContain(i => i["image_url"]!["detail"]!.GetValue<string>() == "high");

        var types = parts.Select(p => p!["type"]!.GetValue<string>()).ToList();
        types.Should().Equal("text", "text", "image_url", "text", "image_url");
        parts[1]!["text"]!.GetValue<string>().Should().Be("attachment_id: att_fridge");
        parts[3]!["text"]!.GetValue<string>().Should().Be("attachment_id: att_panel");
        _capturedRequestBody.Should().NotContain("att_video").And.NotContain("att_voice");
    }

    [Fact]
    public async Task AssessAsync_ByDefault_ShouldSendTheFirstFivePhotos_InOrder()
    {
        var sut = CreateService(Assessed());
        var photos = Enumerable.Range(1, 7).Select(i => Photo($"att_{i}")).ToList();

        await sut.AssessAsync(Request(), photos);

        AzureOpenAiAssessmentOptions.DefaultMaxImages.Should().Be(5);
        ImageParts(_capturedRequestBody!).Should().HaveCount(5);
        _capturedRequestBody.Should().Contain("attachment_id: att_5").And.NotContain("attachment_id: att_6");
    }

    [Fact]
    public async Task AssessAsync_ShouldHonourAConfiguredPhotoCap()
    {
        var sut = CreateService(Assessed(), maxImages: 2);
        var photos = Enumerable.Range(1, 4).Select(i => Photo($"att_{i}")).ToList();

        await sut.AssessAsync(Request(), photos);

        ImageParts(_capturedRequestBody!).Should().HaveCount(2);
        _capturedRequestBody.Should().Contain("attachment_id: att_2").And.NotContain("attachment_id: att_3");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(AzureOpenAiAssessmentOptions.MaxImagesCeiling + 1)]
    public async Task AssessAsync_WhenThePhotoCapIsOutOfRange_ShouldUseTheDefault(int configured)
    {
        var sut = CreateService(Assessed(), maxImages: configured);
        var photos = Enumerable.Range(1, 12).Select(i => Photo($"att_{i}")).ToList();

        await sut.AssessAsync(Request(), photos);

        ImageParts(_capturedRequestBody!).Should().HaveCount(AzureOpenAiAssessmentOptions.DefaultMaxImages);
    }

    [Fact]
    public async Task AssessAsync_ShouldSkipHeicEmptyAndOversizedImages()
    {
        var sut = CreateService(Assessed());
        AssessmentPhoto[] photos =
        [
            Photo("att_heic", "image/heic"),
            Photo("att_empty", bytes: []),
            Photo("att_huge", bytes: new byte[AzureOpenAiPreliminaryAssessmentService.MaxImageBytes + 1]),
            Photo("att_ok"),
        ];

        await sut.AssessAsync(Request(), photos);

        ImageParts(_capturedRequestBody!).Should().ContainSingle();
        _capturedRequestBody.Should().Contain("attachment_id: att_ok");
    }

    [Fact]
    public async Task AssessAsync_WithPhotos_ShouldAskForPhotoFindings_UnderThePrivacyAndNoGuessingRules()
    {
        var sut = CreateService(Assessed());

        await sut.AssessAsync(Request(), [Photo("att_fridge")]);

        var prompt = SystemPromptOf(_capturedRequestBody!);
        prompt.Should().Contain("photo_findings").And.Contain("data_plates").And.Contain("fault_codes").And.Contain("observations");
        prompt.Should().Contain("Report only what is visible");
        prompt.Should().Contain("leave that field out");
        prompt.Should().Contain("licence plates");
        prompt.Should().Contain("not a suggested part");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssessAsync_WithNoUsablePhotos_ShouldSendExactlyTheTextOnlyRequest(bool reasoningModel)
    {
        var baseline = CreateService(Assessed(), reasoningModel);
        await baseline.AssessAsync(Request());
        var textOnly = _capturedRequestBody;

        var withNone = CreateService(Assessed(), reasoningModel);
        await withNone.AssessAsync(Request(), [Photo("att_video", "video/mp4")]);

        _capturedRequestBody.Should().Be(textOnly);
        JsonNode.Parse(textOnly!)!["messages"]![1]!["content"]!.GetValueKind().Should().Be(JsonValueKind.String);
        SystemPromptOf(textOnly!).Should().NotContain("photo_findings");
    }

    [Fact]
    public async Task AssessAsync_WithPhotos_ShouldParseTheFindings_DroppingAnyCitingAPhotoNotSent()
    {
        var sut = CreateService(Assessed(PhotoFindingsPayload));

        var result = await sut.AssessAsync(Request(), [Photo("att_fridge"), Photo("att_thermostat")]);

        result.Provider.Should().Be(nameof(AzureOpenAiPreliminaryAssessmentService));
        var findings = result.PhotoFindings!;
        var plate = findings.DataPlates.Should().ContainSingle().Subject;
        plate.Component.Should().Be("Refrigerator");
        plate.ModelNumber.Should().Be("RM2652");
        plate.SerialNumber.Should().Be("12345678");
        plate.AttachmentId.Should().Be("att_fridge");
        findings.FaultCodes.Should().ContainSingle().Which.Code.Should().Be("E1");
        findings.Observations.Should().ContainSingle().Which.AttachmentId.Should().Be("att_thermostat");
    }

    [Fact]
    public async Task AssessAsync_WhenModelAbstainsWithPhotoFindings_ShouldKeepTheFindings()
    {
        var sut = CreateService(Assessed(PhotoFindingsPayload, confidence: "abstain"));

        var result = await sut.AssessAsync(Request(), [Photo("att_fridge"), Photo("att_thermostat")]);

        result.Confidence.Should().Be(AssessmentConfidence.Abstain);
        result.ProbableCause.Should().BeNull();
        result.PossibleFixes.Should().BeEmpty();
        result.LikelyParts.Should().BeEmpty();
        result.PhotoFindings.Should().NotBeNull();
        result.PhotoFindings!.DataPlates.Should().ContainSingle();
    }

    [Fact]
    public async Task AssessAsync_WithoutPhotos_ShouldIgnoreAnyPhotoFindingsTheModelInvents()
    {
        var sut = CreateService(Assessed(PhotoFindingsPayload));

        var result = await sut.AssessAsync(Request());

        result.PhotoFindings.Should().BeNull();
    }

    [Fact]
    public async Task AssessAsync_WhenThePhotoCallIsRejected_ShouldRetryOnceTextOnly()
    {
        var sut = CreateSequenceService(
            () => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"invalid image\"}") },
            () => Assessed());

        var result = await sut.AssessAsync(Request(), [Photo("att_fridge")]);

        _capturedBodies.Should().HaveCount(2);
        ImageParts(_capturedBodies[0]).Should().ContainSingle();
        JsonNode.Parse(_capturedBodies[1])!["messages"]![1]!["content"]!.GetValueKind().Should().Be(JsonValueKind.String);
        result.Provider.Should().Be(nameof(AzureOpenAiPreliminaryAssessmentService));
        result.ProbableCause.Should().Be("Cooling unit failure.");
        result.PhotoFindings.Should().BeNull();
    }

    [Fact]
    public async Task AssessAsync_WhenThePhotoReplyIsUnparseable_ShouldRetryTextOnly()
    {
        var sut = CreateSequenceService(() => ChatResponseWithContent("{ truncated"), () => Assessed());

        var result = await sut.AssessAsync(Request(), [Photo("att_fridge")]);

        _capturedBodies.Should().HaveCount(2);
        result.Provider.Should().Be(nameof(AzureOpenAiPreliminaryAssessmentService));
    }

    [Fact]
    public async Task AssessAsync_WhenPhotoAndTextOnlyCallsBothFail_ShouldFallBackToRuleBased()
    {
        var sut = CreateSequenceService(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await sut.AssessAsync(Request(), [Photo("att_fridge")]);

        _capturedBodies.Should().HaveCount(2);
        result.Provider.Should().Be(nameof(RuleBasedPreliminaryAssessmentService));
        result.PhotoFindings.Should().BeNull();
    }

    [Fact]
    public async Task AssessAsync_WithoutPhotos_WhenTheCallFails_ShouldNotRetry()
    {
        var sut = CreateSequenceService(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await sut.AssessAsync(Request());

        _capturedBodies.Should().ContainSingle();
    }

    [Fact]
    public async Task AssessAsync_WithPhotos_OnTheGpt4oShape_ShouldRaiseMaxTokensForTheFindings()
    {
        var sut = CreateService(Assessed());

        await sut.AssessAsync(Request(), [Photo("att_fridge")]);

        JsonNode.Parse(_capturedRequestBody!)!["max_tokens"]!.GetValue<int>().Should().BeGreaterThan(600);
    }
}
