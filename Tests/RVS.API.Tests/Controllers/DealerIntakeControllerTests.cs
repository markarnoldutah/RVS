using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;
using RVS.API.Controllers;
using RVS.API.Integrations;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

/// <summary>
/// Tests the authenticated, dealer-scoped counterpart of the anonymous <see cref="IntakeController"/>.
/// Logic mirrors the anonymous controller's AI actions; these tests focus on the wrapper concerns:
/// tenant guard, authorization, and route surface. Deep validation behaviour is already covered
/// by <c>IntakeControllerTests</c>.
/// </summary>
public class DealerIntakeControllerTests
{
    private const string TestTenantId = "ten_test";
    private const string TestDealershipId = "dlr_test";

    private readonly Mock<IVinDecoderService> _vinDecoderServiceMock = new();
    private readonly Mock<IVinExtractionService> _vinExtractionServiceMock = new();
    private readonly Mock<ISpeechToTextService> _speechToTextServiceMock = new();
    private readonly Mock<IIssueTextRefinementService> _issueTextRefinementServiceMock = new();
    private readonly Mock<ILogger<DealerIntakeController>> _loggerMock = new();

    private DealerIntakeController BuildSut(string? tenantId = TestTenantId)
    {
        var aiOptions = MsOptions.Create(new AiOptions { MaxImageBytes = 5 * 1024 * 1024, MaxAudioBytes = 10 * 1024 * 1024 });
        var sut = new DealerIntakeController(
            _vinDecoderServiceMock.Object,
            _vinExtractionServiceMock.Object,
            _speechToTextServiceMock.Object,
            _issueTextRefinementServiceMock.Object,
            aiOptions,
            BuildClaimsService(tenantId),
            _loggerMock.Object);

        sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return sut;
    }

    // ── Attribute surface ────────────────────────────────────────────────

    [Fact]
    public void Class_ShouldHaveAuthorizeAttribute()
    {
        var authorize = typeof(DealerIntakeController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);

        authorize.Should().NotBeEmpty("authenticated intake-AI helpers must require authorization");
    }

    [Fact]
    public void Class_ShouldNotHaveAllowAnonymousAttribute()
    {
        var allowAnonymous = typeof(DealerIntakeController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);

        allowAnonymous.Should().BeEmpty("this controller is the authenticated twin of the anonymous intake AI endpoints");
    }

    [Fact]
    public void Class_ShouldHaveDealershipScopedRoute()
    {
        var route = typeof(DealerIntakeController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .SingleOrDefault();

        route.Should().NotBeNull();
        route!.Template.Should().Be("api/dealerships/{dealershipId}/intake");
    }

    // ── DecodeVin ────────────────────────────────────────────────────────

    [Fact]
    public async Task DecodeVin_WhenVinResolved_ShouldReturnOkWithDecodeResponse()
    {
        _vinDecoderServiceMock.Setup(s => s.DecodeVinAsync("1RGDE4428R1000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VinDecoderResult("1RGDE4428R1000001", "Grand Design", "Momentum 395MS", 2024));
        var sut = BuildSut();

        var result = await sut.DecodeVin(TestDealershipId, "1RGDE4428R1000001");

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<VinDecodeResponseDto>().Subject;
        dto.Vin.Should().Be("1RGDE4428R1000001");
        dto.Manufacturer.Should().Be("Grand Design");
        dto.Year.Should().Be(2024);
    }

    [Fact]
    public async Task DecodeVin_WhenVinNotResolved_ShouldReturnNotFound()
    {
        _vinDecoderServiceMock.Setup(s => s.DecodeVinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinDecoderResult?)null);
        var sut = BuildSut();

        var result = await sut.DecodeVin(TestDealershipId, "INVALIDVIN1234567");

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DecodeVin_WhenTenantMissing_ShouldThrowUnauthorized()
    {
        var sut = BuildSut(tenantId: null);

        var act = async () => await sut.DecodeVin(TestDealershipId, "1RGDE4428R1000001");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── ExtractVin ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractVin_WhenExtractionSucceeds_ShouldReturnOkWithVin()
    {
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF };
        var request = new VinExtractionRequestDto
        {
            ImageBase64 = Convert.ToBase64String(imageBytes),
            ContentType = "image/jpeg"
        };
        _vinExtractionServiceMock.Setup(s => s.ExtractVinFromImageAsync(imageBytes, "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VinExtractionResult("1RGDE4428R1000001", 0.95, "MockVinExtractionService"));
        var sut = BuildSut();

        var result = await sut.ExtractVin(TestDealershipId, request);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<AiOperationResponseDto<VinExtractionResultDto>>().Subject;
        dto.Success.Should().BeTrue();
        dto.Result!.Vin.Should().Be("1RGDE4428R1000001");
        dto.Confidence.Should().Be(0.95);
    }

    [Fact]
    public async Task ExtractVin_WhenImageBase64IsEmpty_ShouldReturn400()
    {
        var request = new VinExtractionRequestDto { ImageBase64 = "", ContentType = "image/jpeg" };
        var sut = BuildSut();

        var result = await sut.ExtractVin(TestDealershipId, request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExtractVin_WhenTenantMissing_ShouldThrowUnauthorized()
    {
        var request = new VinExtractionRequestDto
        {
            ImageBase64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8 }),
            ContentType = "image/jpeg"
        };
        var sut = BuildSut(tenantId: null);

        var act = async () => await sut.ExtractVin(TestDealershipId, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── TranscribeIssue ──────────────────────────────────────────────────

    [Fact]
    public async Task TranscribeIssue_WhenTranscriptionSucceeds_ShouldReturnOkWithTranscript()
    {
        var audioBytes = new byte[] { 0x00, 0x01, 0x02 };
        var request = new IssueTranscriptionRequestDto
        {
            AudioBase64 = Convert.ToBase64String(audioBytes),
            ContentType = "audio/webm"
        };
        _speechToTextServiceMock.Setup(s => s.TranscribeAudioAsync(audioBytes, "audio/webm", "en-US", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechToTextResult("raw text", "cleaned text", 0.9, "MockSpeechToTextService"));
        var sut = BuildSut();

        var result = await sut.TranscribeIssue(TestDealershipId, request);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<AiOperationResponseDto<IssueTranscriptionResultDto>>().Subject;
        dto.Result!.RawTranscript.Should().Be("raw text");
        dto.Result.CleanedDescription.Should().Be("cleaned text");
    }

    [Fact]
    public async Task TranscribeIssue_WhenContentTypeIsNotAudio_ShouldReturn400()
    {
        var request = new IssueTranscriptionRequestDto
        {
            AudioBase64 = Convert.ToBase64String(new byte[] { 0x00 }),
            ContentType = "application/json"
        };
        var sut = BuildSut();

        var result = await sut.TranscribeIssue(TestDealershipId, request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TranscribeIssue_WhenTenantMissing_ShouldThrowUnauthorized()
    {
        var request = new IssueTranscriptionRequestDto
        {
            AudioBase64 = Convert.ToBase64String(new byte[] { 0x00 }),
            ContentType = "audio/webm"
        };
        var sut = BuildSut(tenantId: null);

        var act = async () => await sut.TranscribeIssue(TestDealershipId, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── RefineIssueText ──────────────────────────────────────────────────

    [Fact]
    public async Task RefineIssueText_WhenSucceeds_ShouldReturnOkWithCleanedDescription()
    {
        var request = new IssueTextRefinementRequestDto { RawTranscript = "raw transcript text" };
        _issueTextRefinementServiceMock.Setup(s => s.RefineTranscriptAsync("raw transcript text", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueTextRefinementResult("The water heater is broken.", 0.88, "MockIssueTextRefinementService"));
        var sut = BuildSut();

        var result = await sut.RefineIssueText(TestDealershipId, request);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<AiOperationResponseDto<IssueTextRefinementResultDto>>().Subject;
        dto.Result!.CleanedDescription.Should().Be("The water heater is broken.");
    }

    [Fact]
    public async Task RefineIssueText_WhenRawTranscriptIsEmpty_ShouldReturn400()
    {
        var request = new IssueTextRefinementRequestDto { RawTranscript = "" };
        var sut = BuildSut();

        var result = await sut.RefineIssueText(TestDealershipId, request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefineIssueText_WhenTenantMissing_ShouldThrowUnauthorized()
    {
        var request = new IssueTextRefinementRequestDto { RawTranscript = "text" };
        var sut = BuildSut(tenantId: null);

        var act = async () => await sut.RefineIssueText(TestDealershipId, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── SuggestCategory ──────────────────────────────────────────────────

    [Fact]
    public async Task SuggestCategory_WhenSucceeds_ShouldReturnOkWithCategory()
    {
        var request = new IssueCategorySuggestionRequestDto { IssueDescription = "The water heater is broken." };
        _issueTextRefinementServiceMock.Setup(s => s.SuggestCategoryAsync("The water heater is broken.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCategorySuggestionResult("Water System", 0.9, "MockIssueTextRefinementService"));
        var sut = BuildSut();

        var result = await sut.SuggestCategory(TestDealershipId, request);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<AiOperationResponseDto<IssueCategorySuggestionResultDto>>().Subject;
        dto.Result!.IssueCategory.Should().Be("Water System");
    }

    [Fact]
    public async Task SuggestCategory_WhenTenantMissing_ShouldThrowUnauthorized()
    {
        var request = new IssueCategorySuggestionRequestDto { IssueDescription = "text" };
        var sut = BuildSut(tenantId: null);

        var act = async () => await sut.SuggestCategory(TestDealershipId, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── SuggestInsights ──────────────────────────────────────────────────

    [Fact]
    public async Task SuggestInsights_WhenSucceeds_ShouldReturnOkWithUrgencyAndUsage()
    {
        var request = new IssueInsightsSuggestionRequestDto { IssueDescription = "I can't drive it safely." };
        _issueTextRefinementServiceMock.Setup(s => s.SuggestInsightsAsync("I can't drive it safely.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueInsightsSuggestionResult("High", "Full-Time", 0.85, "MockIssueTextRefinementService"));
        var sut = BuildSut();

        var result = await sut.SuggestInsights(TestDealershipId, request);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<AiOperationResponseDto<IssueInsightsSuggestionResultDto>>().Subject;
        dto.Result!.Urgency.Should().Be("High");
        dto.Result.RvUsage.Should().Be("Full-Time");
    }

    [Fact]
    public async Task SuggestInsights_WhenTenantMissing_ShouldThrowUnauthorized()
    {
        var request = new IssueInsightsSuggestionRequestDto { IssueDescription = "text" };
        var sut = BuildSut(tenantId: null);

        var act = async () => await sut.SuggestInsights(TestDealershipId, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static ClaimsService BuildClaimsService(string? tenantId)
    {
        var claims = new List<Claim>();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            claims.Add(new Claim(ClaimsService.TenantIdClaimType, tenantId));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new ClaimsService(accessor.Object);
    }
}
