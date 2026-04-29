using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

public class IntakeApiClientTests
{
    private static IntakeApiClient CreateClient(HttpClient httpClient) => new(httpClient);

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new IntakeApiClient(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── GetUploadSasAsync ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUploadSasAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.GetUploadSasAsync(slug!, "sr-1", "photo.jpg", "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUploadSasAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.GetUploadSasAsync("slug", srId!, "photo.jpg", "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUploadSasAsync_WhenFileNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? fileName)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.GetUploadSasAsync("slug", "sr-1", fileName!, "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUploadSasAsync_WhenContentTypeIsNullOrWhiteSpace_ShouldThrowArgumentException(string? contentType)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.GetUploadSasAsync("slug", "sr-1", "photo.jpg", contentType!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetUploadSasAsync_WhenApiReturns200_ShouldDeserializeResponse()
    {
        var expected = new AttachmentUploadSasResponseDto
        {
            SasUrl = "https://blob.test/container/blob?sig=abc",
            BlobName = "ten_1/sr_1/guid_photo.jpg",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };

        var handler = new FakeHttpHandler(HttpStatusCode.OK, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var result = await sut.GetUploadSasAsync("my-slug", "sr-1", "photo.jpg", "image/jpeg");

        result.SasUrl.Should().Be(expected.SasUrl);
        result.BlobName.Should().Be(expected.BlobName);
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("sr-1");
        handler.LastRequest.RequestUri.ToString().Should().Contain("fileName=photo.jpg");
        handler.LastRequest.RequestUri.ToString().Should().Contain("contentType=image%2Fjpeg");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    // ── ConfirmUploadAsync ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConfirmUploadAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.ConfirmUploadAsync(slug!, "sr-1", new AttachmentConfirmRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConfirmUploadAsync_WhenServiceRequestIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? srId)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.ConfirmUploadAsync("slug", srId!, new AttachmentConfirmRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.ConfirmUploadAsync("slug", "sr-1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenApiReturns201_ShouldDeserializeAttachmentDto()
    {
        var expected = new AttachmentDto
        {
            AttachmentId = "att-1",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 12345,
            BlobUri = "ten_1/sr_1/guid_photo.jpg",
            CreatedAtUtc = DateTime.UtcNow
        };

        var handler = new FakeHttpHandler(HttpStatusCode.Created, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var confirmRequest = new AttachmentConfirmRequestDto
        {
            BlobName = "ten_1/sr_1/guid_photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 12345
        };

        var result = await sut.ConfirmUploadAsync("my-slug", "sr-1", confirmRequest);

        result.AttachmentId.Should().Be(expected.AttachmentId);
        result.FileName.Should().Be(expected.FileName);
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("sr-1");
        handler.LastRequest.RequestUri.ToString().Should().Contain("attachments/confirm");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task GetUploadSasAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest, new { message = "Bad request" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var act = () => sut.GetUploadSasAsync("slug", "sr-1", "photo.jpg", "image/jpeg");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.NotFound, new { message = "Not found" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var act = () => sut.ConfirmUploadAsync("slug", "sr-1", new AttachmentConfirmRequestDto());

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── DecodeVinAsync ───────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DecodeVinAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.DecodeVinAsync(slug!, "1RGDE4428R1000001");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DecodeVinAsync_WhenVinIsNullOrWhiteSpace_ShouldThrowArgumentException(string? vin)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.DecodeVinAsync("slug", vin!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturns200_ShouldReturnDecodedVehicleInfo()
    {
        var expected = new VinDecodeResponseDto
        {
            Vin = "1RGDE4428R1000001",
            Manufacturer = "Grand Design",
            Model = "Momentum 395MS",
            Year = 2024
        };

        var handler = new FakeHttpHandler(HttpStatusCode.OK, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var result = await sut.DecodeVinAsync("my-slug", "1RGDE4428R1000001");

        result.Should().NotBeNull();
        result!.Vin.Should().Be("1RGDE4428R1000001");
        result.Manufacturer.Should().Be("Grand Design");
        result.Model.Should().Be("Momentum 395MS");
        result.Year.Should().Be(2024);
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("decode-vin");
        handler.LastRequest.RequestUri.ToString().Should().Contain("1RGDE4428R1000001");
        handler.LastRequest.Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturns404_ShouldReturnNull()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.NotFound, new { });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var result = await sut.DecodeVinAsync("slug", "1RGDE4428R1000001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturnsServerError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, new { message = "Server error" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var act = () => sut.DecodeVinAsync("slug", "1RGDE4428R1000001");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── ExtractVinFromImageAsync ─────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExtractVinFromImageAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });
        var request = new VinExtractionRequestDto { ImageBase64 = "abc==", ContentType = "image/jpeg" };

        var act = () => sut.ExtractVinFromImageAsync(slug!, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.ExtractVinFromImageAsync("slug", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenApiReturns200_ShouldDeserializeResponse()
    {
        var expected = new AiOperationResponseDto<VinExtractionResultDto>
        {
            Success = true,
            Result = new VinExtractionResultDto { Vin = "1RGDE4428R1000001" },
            Confidence = 0.95,
            Warnings = [],
            Provider = "MockVinExtractionService",
            CorrelationId = "corr-001"
        };

        var handler = new FakeHttpHandler(HttpStatusCode.OK, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var request = new VinExtractionRequestDto { ImageBase64 = "abc==", ContentType = "image/jpeg" };
        var result = await sut.ExtractVinFromImageAsync("my-slug", request);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Result!.Vin.Should().Be("1RGDE4428R1000001");
        result.Confidence.Should().Be(0.95);
        result.Provider.Should().Be("MockVinExtractionService");
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("ai/extract-vin");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task ExtractVinFromImageAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest, new { message = "Bad request" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var request = new VinExtractionRequestDto { ImageBase64 = "abc==", ContentType = "image/jpeg" };
        var act = () => sut.ExtractVinFromImageAsync("slug", request);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── TranscribeIssueAsync ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TranscribeIssueAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });
        var request = new IssueTranscriptionRequestDto { AudioBase64 = "abc==", ContentType = "audio/webm" };

        var act = () => sut.TranscribeIssueAsync(slug!, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TranscribeIssueAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.TranscribeIssueAsync("slug", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TranscribeIssueAsync_WhenApiReturns200_ShouldDeserializeResponse()
    {
        var expected = new AiOperationResponseDto<IssueTranscriptionResultDto>
        {
            Success = true,
            Result = new IssueTranscriptionResultDto
            {
                RawTranscript = "My water heater broke",
                CleanedDescription = "Water heater is broken."
            },
            Confidence = 0.92,
            Warnings = [],
            Provider = "MockSpeechToTextService",
            CorrelationId = "corr-001"
        };

        var handler = new FakeHttpHandler(HttpStatusCode.OK, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var request = new IssueTranscriptionRequestDto { AudioBase64 = "abc==", ContentType = "audio/webm" };
        var result = await sut.TranscribeIssueAsync("my-slug", request);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Result!.RawTranscript.Should().Be("My water heater broke");
        result.Result.CleanedDescription.Should().Be("Water heater is broken.");
        result.Confidence.Should().Be(0.92);
        result.Provider.Should().Be("MockSpeechToTextService");
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("ai/transcribe-issue");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task TranscribeIssueAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest, new { message = "Bad request" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var request = new IssueTranscriptionRequestDto { AudioBase64 = "abc==", ContentType = "audio/webm" };
        var act = () => sut.TranscribeIssueAsync("slug", request);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── RefineIssueTextAsync ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefineIssueTextAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });
        var request = new IssueTextRefinementRequestDto { RawTranscript = "some text" };

        var act = () => sut.RefineIssueTextAsync(slug!, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RefineIssueTextAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.RefineIssueTextAsync("slug", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RefineIssueTextAsync_WhenApiReturns200_ShouldDeserializeResponse()
    {
        var expected = new AiOperationResponseDto<IssueTextRefinementResultDto>
        {
            Success = true,
            Result = new IssueTextRefinementResultDto { CleanedDescription = "Water heater stopped working." },
            Confidence = 0.88,
            Warnings = [],
            Provider = "RuleBasedIssueTextRefinementService",
            CorrelationId = "corr-002"
        };

        var handler = new FakeHttpHandler(HttpStatusCode.OK, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var request = new IssueTextRefinementRequestDto { RawTranscript = "um my water heater stopped working" };
        var result = await sut.RefineIssueTextAsync("my-slug", request);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Result!.CleanedDescription.Should().Be("Water heater stopped working.");
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("ai/refine-issue-text");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task RefineIssueTextAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest, new { message = "Bad request" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var request = new IssueTextRefinementRequestDto { RawTranscript = "some text" };
        var act = () => sut.RefineIssueTextAsync("slug", request);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── SuggestIssueCategoryAsync ────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SuggestIssueCategoryAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });
        var request = new IssueCategorySuggestionRequestDto { IssueDescription = "battery issue" };

        var act = () => sut.SuggestIssueCategoryAsync(slug!, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SuggestIssueCategoryAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.SuggestIssueCategoryAsync("slug", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SuggestIssueCategoryAsync_WhenApiReturns200_ShouldDeserializeResponse()
    {
        var expected = new AiOperationResponseDto<IssueCategorySuggestionResultDto>
        {
            Success = true,
            Result = new IssueCategorySuggestionResultDto { IssueCategory = "Electrical" },
            Confidence = 0.85,
            Warnings = [],
            Provider = "RuleBasedIssueTextRefinementService",
            CorrelationId = "corr-003"
        };

        var handler = new FakeHttpHandler(HttpStatusCode.OK, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var request = new IssueCategorySuggestionRequestDto { IssueDescription = "The battery is dead" };
        var result = await sut.SuggestIssueCategoryAsync("my-slug", request);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Result!.IssueCategory.Should().Be("Electrical");
        result.Confidence.Should().Be(0.85);
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("ai/suggest-category");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task SuggestIssueCategoryAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest, new { message = "Bad request" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var request = new IssueCategorySuggestionRequestDto { IssueDescription = "something" };
        var act = () => sut.SuggestIssueCategoryAsync("slug", request);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── AssessCapabilitiesAsync ──────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssessCapabilitiesAsync_WhenLocationSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.AssessCapabilitiesAsync(slug!, new CapabilityAssessmentRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateClient(new HttpClient { BaseAddress = new Uri("https://test.local") });

        var act = () => sut.AssessCapabilitiesAsync("slug", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenApiReturns200_ShouldDeserializeResponseAndPostToCorrectUrl()
    {
        var expected = new CapabilityAssessmentResponseDto
        {
            Matched = false,
            IssueCategory = "Electrical",
            RequiredCapabilities = ["electrical"],
            MissingCapabilities = ["electrical"],
            LocationPhone = "(555) 123-4567"
        };
        var handler = new FakeHttpHandler(HttpStatusCode.OK, expected);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var request = new CapabilityAssessmentRequestDto { IssueDescription = "battery is dead" };
        var result = await sut.AssessCapabilitiesAsync("my-slug", request);

        result.Matched.Should().BeFalse();
        result.IssueCategory.Should().Be("Electrical");
        result.MissingCapabilities.Should().BeEquivalentTo(["electrical"]);
        result.LocationPhone.Should().Be("(555) 123-4567");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Contain("my-slug");
        handler.LastRequest.RequestUri.ToString().Should().Contain("assess-capabilities");
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WhenApiReturnsError_ShouldThrowHttpRequestException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest, new { message = "Bad request" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var sut = CreateClient(httpClient);

        var act = () => sut.AssessCapabilitiesAsync("slug",
            new CapabilityAssessmentRequestDto { IssueDescription = "x" });

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── Test helper ──────────────────────────────────────────────────────

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object _responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpHandler(HttpStatusCode statusCode, object responseBody)
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
