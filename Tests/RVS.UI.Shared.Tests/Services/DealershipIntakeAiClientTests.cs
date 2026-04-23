using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests the authenticated, dealership-scoped implementation of <c>IIntakeAiClient</c>
/// used by the Manager WASM walk-in flow. URLs must route through
/// <c>api/dealerships/{dealershipId}/intake/*</c> so the authenticated controller
/// handles them (no rate-limit hit, audit trail intact).
/// </summary>
public class DealershipIntakeAiClientTests
{
    private const string TestDealershipId = "dlr_test";

    // ── Constructor guards ───────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrow()
    {
        var act = () => new DealershipIntakeAiClient(null!, TestDealershipId);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenDealershipIdIsNullOrWhiteSpace_ShouldThrow(string? id)
    {
        var http = new HttpClient { BaseAddress = new Uri("https://test.local") };

        var act = () => new DealershipIntakeAiClient(http, id!);

        act.Should().Throw<ArgumentException>();
    }

    // ── DecodeVinAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DecodeVinAsync_ShouldRouteThroughDealershipIntakeAiPath()
    {
        var body = new VinDecodeResponseDto
        {
            Vin = "1RGDE4428R1000001",
            Manufacturer = "Grand Design",
            Model = "Momentum",
            Year = 2024
        };
        var (sut, handler) = BuildSut(HttpStatusCode.OK, body);

        var result = await sut.DecodeVinAsync("1RGDE4428R1000001");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be($"/api/dealerships/{TestDealershipId}/intake/decode-vin/1RGDE4428R1000001");
        result!.Manufacturer.Should().Be("Grand Design");
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturns404_ShouldReturnNull()
    {
        var (sut, _) = BuildSut(HttpStatusCode.NotFound, new { });

        var result = await sut.DecodeVinAsync("INVALIDVIN1234567");

        result.Should().BeNull();
    }

    // ── ExtractVinFromImageAsync ─────────────────────────────────────────

    [Fact]
    public async Task ExtractVinFromImageAsync_ShouldPostToDealershipAiPath()
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
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be($"/api/dealerships/{TestDealershipId}/intake/extract-vin");
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
    public async Task TranscribeIssueAsync_ShouldPostToDealershipAiPath()
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

        handler.LastRequest!.RequestUri!.AbsolutePath
            .Should().Be($"/api/dealerships/{TestDealershipId}/intake/transcribe-issue");
        result!.Result!.CleanedDescription.Should().Be("clean");
    }

    // ── RefineIssueTextAsync ─────────────────────────────────────────────

    [Fact]
    public async Task RefineIssueTextAsync_ShouldPostToDealershipAiPath()
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

        handler.LastRequest!.RequestUri!.AbsolutePath
            .Should().Be($"/api/dealerships/{TestDealershipId}/intake/refine-issue-text");
        result!.Result!.CleanedDescription.Should().Be("cleaned");
    }

    // ── SuggestIssueCategoryAsync ────────────────────────────────────────

    [Fact]
    public async Task SuggestIssueCategoryAsync_ShouldPostToDealershipAiPath()
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

        handler.LastRequest!.RequestUri!.AbsolutePath
            .Should().Be($"/api/dealerships/{TestDealershipId}/intake/suggest-category");
        result!.Result!.IssueCategory.Should().Be("Electrical");
    }

    // ── SuggestIssueInsightsAsync ────────────────────────────────────────

    [Fact]
    public async Task SuggestIssueInsightsAsync_ShouldPostToDealershipAiPath()
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

        handler.LastRequest!.RequestUri!.AbsolutePath
            .Should().Be($"/api/dealerships/{TestDealershipId}/intake/suggest-insights");
        result!.Result!.Urgency.Should().Be("High");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static (DealershipIntakeAiClient sut, RecordingHandler handler) BuildSut(
        HttpStatusCode statusCode, object responseBody)
    {
        var handler = new RecordingHandler(statusCode, responseBody);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        return (new DealershipIntakeAiClient(http, TestDealershipId), handler);
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
