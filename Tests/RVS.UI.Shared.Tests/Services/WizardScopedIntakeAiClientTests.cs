using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RVS.Blazor.Intake.State;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;
using RVS.UI.Shared.Services;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests the anonymous, slug-scoped implementation of <see cref="IIntakeAiClient"/>
/// used by the Intake WASM wizard. URLs must route through <c>api/intake/{slug}/ai/*</c>
/// (or <c>api/intake/{slug}/decode-vin/...</c>), with the slug resolved from
/// <see cref="IIntakeWizardState.Slug"/> at call time — not baked into the client.
/// </summary>
public class WizardScopedIntakeAiClientTests
{
    private const string TestSlug = "camping-world-slc";

    // ── Constructor guards ───────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrow()
    {
        var state = BuildState(TestSlug);

        var act = () => new WizardScopedIntakeAiClient(null!, state);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WhenStateIsNull_ShouldThrow()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://test.local") };

        var act = () => new WizardScopedIntakeAiClient(http, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── DecodeVinAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DecodeVinAsync_ShouldRouteThroughSlugBasedIntakePath()
    {
        var body = new VinDecodeResponseDto
        {
            Vin = "1RGDE4428R1000001",
            Manufacturer = "Grand Design",
            Model = "Momentum 395MS",
            Year = 2024
        };
        var (sut, handler) = BuildSut(HttpStatusCode.OK, body);

        var result = await sut.DecodeVinAsync("1RGDE4428R1000001");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be($"/api/intake/{TestSlug}/decode-vin/1RGDE4428R1000001");
        result!.Manufacturer.Should().Be("Grand Design");
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturns404_ShouldReturnNull()
    {
        var (sut, _) = BuildSut(HttpStatusCode.NotFound, new { });

        var result = await sut.DecodeVinAsync("INVALIDVIN1234567");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecodeVinAsync_ShouldReadSlugFromStateAtCallTime()
    {
        var state = BuildState("initial-slug");
        var handler = new RecordingHandler(HttpStatusCode.OK, new VinDecodeResponseDto
        {
            Vin = "1RGDE4428R1000001",
            Manufacturer = "Grand Design",
            Model = "Momentum",
            Year = 2024
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = new WizardScopedIntakeAiClient(http, state);

        state.Slug = "updated-slug";
        await sut.DecodeVinAsync("1RGDE4428R1000001");

        handler.LastRequest!.RequestUri!.AbsolutePath
            .Should().StartWith("/api/intake/updated-slug/");
    }

    // ── ExtractVinFromImageAsync ─────────────────────────────────────────

    [Fact]
    public async Task ExtractVinFromImageAsync_ShouldPostToSlugAiPath()
    {
        var body = new AiOperationResponseDto<VinExtractionResultDto>
        {
            Success = true,
            Result = new VinExtractionResultDto { Vin = "1RGDE4428R1000001" },
            Confidence = 0.9,
            Warnings = [],
            Provider = "Test",
            CorrelationId = "corr-1"
        };
        var (sut, handler) = BuildSut(HttpStatusCode.OK, body);

        var result = await sut.ExtractVinFromImageAsync(new VinExtractionRequestDto
        {
            ImageBase64 = "abc",
            ContentType = "image/jpeg"
        });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be($"/api/intake/{TestSlug}/ai/extract-vin");
        result!.Result!.Vin.Should().Be("1RGDE4428R1000001");
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenRequestIsNull_ShouldThrow()
    {
        var (sut, _) = BuildSut(HttpStatusCode.OK, new { });

        var act = () => sut.ExtractVinFromImageAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── TranscribeIssueAsync ─────────────────────────────────────────────

    [Fact]
    public async Task TranscribeIssueAsync_ShouldPostToSlugAiPath()
    {
        var body = new AiOperationResponseDto<IssueTranscriptionResultDto>
        {
            Success = true,
            Result = new IssueTranscriptionResultDto { RawTranscript = "raw", CleanedDescription = "clean" },
            Confidence = 0.8,
            Warnings = [],
            Provider = "Test",
            CorrelationId = "corr-1"
        };
        var (sut, handler) = BuildSut(HttpStatusCode.OK, body);

        var result = await sut.TranscribeIssueAsync(new IssueTranscriptionRequestDto
        {
            AudioBase64 = "abc",
            ContentType = "audio/webm"
        });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be($"/api/intake/{TestSlug}/ai/transcribe-issue");
        result!.Result!.CleanedDescription.Should().Be("clean");
    }

    // ── RefineIssueTextAsync ─────────────────────────────────────────────

    [Fact]
    public async Task RefineIssueTextAsync_ShouldPostToSlugAiPath()
    {
        var body = new AiOperationResponseDto<IssueTextRefinementResultDto>
        {
            Success = true,
            Result = new IssueTextRefinementResultDto { CleanedDescription = "cleaned" },
            Confidence = 0.8,
            Warnings = [],
            Provider = "Test",
            CorrelationId = "corr-1"
        };
        var (sut, handler) = BuildSut(HttpStatusCode.OK, body);

        var result = await sut.RefineIssueTextAsync(new IssueTextRefinementRequestDto { RawTranscript = "raw" });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be($"/api/intake/{TestSlug}/ai/refine-issue-text");
        result!.Result!.CleanedDescription.Should().Be("cleaned");
    }

    // ── SuggestIssueCategoryAsync ────────────────────────────────────────

    [Fact]
    public async Task SuggestIssueCategoryAsync_ShouldPostToSlugAiPath()
    {
        var body = new AiOperationResponseDto<IssueCategorySuggestionResultDto>
        {
            Success = true,
            Result = new IssueCategorySuggestionResultDto { IssueCategory = "Electrical" },
            Confidence = 0.85,
            Warnings = [],
            Provider = "Test",
            CorrelationId = "corr-1"
        };
        var (sut, handler) = BuildSut(HttpStatusCode.OK, body);

        var result = await sut.SuggestIssueCategoryAsync(new IssueCategorySuggestionRequestDto
        {
            IssueDescription = "lights flickering"
        });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be($"/api/intake/{TestSlug}/ai/suggest-category");
        result!.Result!.IssueCategory.Should().Be("Electrical");
    }

    // ── SuggestIssueInsightsAsync ────────────────────────────────────────

    [Fact]
    public async Task SuggestIssueInsightsAsync_ShouldPostToSlugAiPath()
    {
        var body = new AiOperationResponseDto<IssueInsightsSuggestionResultDto>
        {
            Success = true,
            Result = new IssueInsightsSuggestionResultDto { Urgency = "High", RvUsage = "Full-Time" },
            Confidence = 0.75,
            Warnings = [],
            Provider = "Test",
            CorrelationId = "corr-1"
        };
        var (sut, handler) = BuildSut(HttpStatusCode.OK, body);

        var result = await sut.SuggestIssueInsightsAsync(new IssueInsightsSuggestionRequestDto
        {
            IssueDescription = "stuck in the driveway"
        });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be($"/api/intake/{TestSlug}/ai/suggest-insights");
        result!.Result!.Urgency.Should().Be("High");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static (WizardScopedIntakeAiClient sut, RecordingHandler handler) BuildSut(
        HttpStatusCode statusCode, object responseBody)
    {
        var state = BuildState(TestSlug);
        var handler = new RecordingHandler(statusCode, responseBody);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        return (new WizardScopedIntakeAiClient(http, state), handler);
    }

    private static IntakeWizardState BuildState(string slug)
    {
        var state = new IntakeWizardState(new NullJSRuntime()) { Slug = slug };
        return state;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object _responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }

        public RecordingHandler(HttpStatusCode statusCode, object responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = JsonContent.Create(_responseBody, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };
            return Task.FromResult(response);
        }
    }
}
