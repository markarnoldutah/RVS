using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using RVS.API.Controllers;
using RVS.API.Integrations;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

public class IntakeControllerTests
{
    private readonly Mock<IIntakeOrchestrationService> _intakeServiceMock = new();
    private readonly Mock<ICategorizationService> _categorizationMock = new();
    private readonly Mock<IAttachmentService> _attachmentServiceMock = new();
    private readonly Mock<IVinDecoderService> _vinDecoderServiceMock = new();
    private readonly Mock<IVinExtractionService> _vinExtractionServiceMock = new();
    private readonly Mock<ISpeechToTextService> _speechToTextServiceMock = new();
    private readonly Mock<IIssueTextRefinementService> _issueTextRefinementServiceMock = new();
    private readonly IntakeController _sut;

    public IntakeControllerTests()
    {
        var aiOptions = Options.Create(new AiOptions { MaxImageBytes = 5 * 1024 * 1024, MaxAudioBytes = 10 * 1024 * 1024 });
        _sut = new IntakeController(
            _intakeServiceMock.Object,
            _categorizationMock.Object,
            _attachmentServiceMock.Object,
            _vinDecoderServiceMock.Object,
            _vinExtractionServiceMock.Object,
            _speechToTextServiceMock.Object,
            _issueTextRefinementServiceMock.Object,
            aiOptions);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetConfig_ShouldReturnOkWithIntakeConfigDto()
    {
        var config = new IntakeConfigResponseDto
        {
            LocationName = "Salt Lake",
            LocationSlug = "camping-world-slc",
            DealershipName = "Camping World",
            MaxFileSizeMb = 25,
            MaxAttachments = 10,
            AllowAnonymousIntake = true
        };
        _intakeServiceMock.Setup(s => s.GetIntakeConfigAsync("camping-world-slc", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _sut.GetConfig("camping-world-slc");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<IntakeConfigResponseDto>().Subject;
        dto.LocationName.Should().Be("Salt Lake");
        dto.DealershipName.Should().Be("Camping World");
    }

    [Fact]
    public async Task GetConfig_WithMagicLinkToken_ShouldPassTokenToService()
    {
        var config = new IntakeConfigResponseDto
        {
            LocationName = "Test",
            LocationSlug = "test-slug",
            DealershipName = "Test Dealer",
            PrefillCustomer = new CustomerInfoDto
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane@example.com"
            }
        };
        _intakeServiceMock.Setup(s => s.GetIntakeConfigAsync("test-slug", "magic-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _sut.GetConfig("test-slug", "magic-token");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<IntakeConfigResponseDto>().Subject;
        dto.PrefillCustomer.Should().NotBeNull();
        dto.PrefillCustomer!.FirstName.Should().Be("Jane");
    }

    [Fact]
    public async Task GetDiagnosticQuestions_ShouldReturnOkWithQuestions()
    {
        _categorizationMock.Setup(s => s.SuggestDiagnosticQuestionsAsync("Electrical", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Is the battery new?", "When did the issue start?" });

        var request = new DiagnosticQuestionsRequest { IssueCategory = "Electrical" };
        var result = await _sut.GetDiagnosticQuestions("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DiagnosticQuestionsResponseDto>().Subject;
        dto.Questions.Should().HaveCount(2);
        dto.Questions[0].QuestionText.Should().Be("Is the battery new?");
    }

    [Fact]
    public async Task SubmitServiceRequest_ShouldReturnCreatedAtAction()
    {
        var sr = BuildServiceRequest();
        _intakeServiceMock.Setup(s => s.ExecuteAsync("test-slug", It.IsAny<ServiceRequestCreateRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var request = new ServiceRequestCreateRequestDto
        {
            Customer = new CustomerInfoDto { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com" },
            Asset = new AssetInfoDto { AssetId = "RV:1FTFW1ET5EKE12345" },
            IssueCategory = "Electrical",
            IssueDescription = "Battery not charging"
        };

        var result = await _sut.SubmitServiceRequest("test-slug", request);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<ServiceRequestDetailResponseDto>().Subject;
        dto.Id.Should().Be(sr.Id);
    }

    private static ServiceRequest BuildServiceRequest() => new()
    {
        Id = "sr_test_1",
        TenantId = "ten_test",
        LocationId = "loc_1",
        Status = "New",
        IssueCategory = "Electrical",
        IssueDescription = "Battery not charging",
        CreatedByUserId = "intake",
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com"
        },
        AssetInfo = new AssetInfoEmbedded
        {
            AssetId = "RV:1FTFW1ET5EKE12345",
            Manufacturer = "Thor",
            Model = "Ace",
            Year = 2023
        }
    };

    [Fact]
    public async Task DecodeVin_WhenVinResolved_ShouldReturnOkWithVinDecodeResponse()
    {
        _vinDecoderServiceMock.Setup(s => s.DecodeVinAsync("1RGDE4428R1000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VinDecoderResult("1RGDE4428R1000001", "Grand Design", "Momentum 395MS", 2024));

        var result = await _sut.DecodeVin("test-slug", "1RGDE4428R1000001");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<VinDecodeResponseDto>().Subject;
        dto.Vin.Should().Be("1RGDE4428R1000001");
        dto.Manufacturer.Should().Be("Grand Design");
        dto.Model.Should().Be("Momentum 395MS");
        dto.Year.Should().Be(2024);
    }

    [Fact]
    public async Task DecodeVin_WhenVinNotResolved_ShouldReturnNotFound()
    {
        _vinDecoderServiceMock.Setup(s => s.DecodeVinAsync("INVALIDVIN1234567", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinDecoderResult?)null);

        var result = await _sut.DecodeVin("test-slug", "INVALIDVIN1234567");

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetUploadSas_ShouldReturnOkWithUploadSasResponse()
    {
        _intakeServiceMock.Setup(s => s.ResolveSlugToTenantIdAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ten_test");
        var sasResponse = new AttachmentUploadSasResponseDto
        {
            SasUrl = "https://blob.example.com/sas?sig=upload",
            BlobName = "ten_test/sr_1/guid_photo.jpg",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };
        _attachmentServiceMock.Setup(s => s.GenerateUploadSasAsync("ten_test", "sr_1", "photo.jpg", "image/jpeg", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sasResponse);

        var result = await _sut.GetUploadSas("test-slug", "sr_1", "photo.jpg", "image/jpeg");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AttachmentUploadSasResponseDto>().Subject;
        dto.SasUrl.Should().Contain("blob.example.com");
        dto.BlobName.Should().Contain("sr_1");
    }

    [Fact]
    public async Task ConfirmUpload_ShouldReturnCreatedWithAttachmentDto()
    {
        _intakeServiceMock.Setup(s => s.ResolveSlugToTenantIdAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ten_test");
        var attachmentDto = new AttachmentDto
        {
            AttachmentId = "att_1",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            BlobUri = "ten_test/sr_1/att_1_photo.jpg"
        };
        var request = new AttachmentConfirmRequestDto
        {
            BlobName = "ten_test/sr_1/att_1_photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024
        };
        _attachmentServiceMock.Setup(s => s.ConfirmAttachmentAsync("ten_test", "sr_1", request, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachmentDto);

        var result = await _sut.ConfirmUpload("test-slug", "sr_1", request);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<AttachmentDto>().Subject;
        dto.FileName.Should().Be("photo.jpg");
    }

    // ── ExtractVin ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractVin_WhenExtractionSucceeds_ShouldReturnOkWithVin()
    {
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF };
        var base64 = Convert.ToBase64String(imageBytes);
        var request = new VinExtractionRequestDto { ImageBase64 = base64, ContentType = "image/jpeg" };

        _vinExtractionServiceMock.Setup(s => s.ExtractVinFromImageAsync(imageBytes, "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VinExtractionResult("1RGDE4428R1000001", 0.95, "MockVinExtractionService"));

        var result = await _sut.ExtractVin("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AiOperationResponseDto<VinExtractionResultDto>>().Subject;
        dto.Success.Should().BeTrue();
        dto.Result!.Vin.Should().Be("1RGDE4428R1000001");
        dto.Confidence.Should().Be(0.95);
        dto.Provider.Should().Be("MockVinExtractionService");
    }

    [Fact]
    public async Task ExtractVin_WhenExtractionReturnsNull_ShouldReturnOkWithNullResultAndWarning()
    {
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF };
        var base64 = Convert.ToBase64String(imageBytes);
        var request = new VinExtractionRequestDto { ImageBase64 = base64, ContentType = "image/jpeg" };

        _vinExtractionServiceMock.Setup(s => s.ExtractVinFromImageAsync(imageBytes, "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinExtractionResult?)null);

        var result = await _sut.ExtractVin("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AiOperationResponseDto<VinExtractionResultDto>>().Subject;
        dto.Success.Should().BeTrue();
        dto.Result.Should().BeNull();
        dto.Confidence.Should().Be(0.0);
        dto.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task ExtractVin_WhenImageBase64IsEmpty_ShouldReturn400()
    {
        var request = new VinExtractionRequestDto { ImageBase64 = "", ContentType = "image/jpeg" };

        var result = await _sut.ExtractVin("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExtractVin_WhenContentTypeIsNotImage_ShouldReturn400()
    {
        var base64 = Convert.ToBase64String(new byte[] { 0x00 });
        var request = new VinExtractionRequestDto { ImageBase64 = base64, ContentType = "application/json" };

        var result = await _sut.ExtractVin("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExtractVin_WhenImageBase64IsInvalid_ShouldReturn400()
    {
        var request = new VinExtractionRequestDto { ImageBase64 = "not-valid-base64!!!", ContentType = "image/jpeg" };

        var result = await _sut.ExtractVin("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExtractVin_WhenImageExceedsMaxSize_ShouldReturn413()
    {
        // 6 MB > 5 MB max
        var largeBase64 = Convert.ToBase64String(new byte[6 * 1024 * 1024]);
        var request = new VinExtractionRequestDto { ImageBase64 = largeBase64, ContentType = "image/jpeg" };

        var result = await _sut.ExtractVin("test-slug", request);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(413);
    }

    // ── TranscribeIssue ──────────────────────────────────────────────────

    [Fact]
    public async Task TranscribeIssue_WhenTranscriptionSucceeds_ShouldReturnOkWithTranscript()
    {
        var audioBytes = new byte[] { 0x00, 0x01, 0x02 };
        var base64 = Convert.ToBase64String(audioBytes);
        var request = new IssueTranscriptionRequestDto { AudioBase64 = base64, ContentType = "audio/webm" };

        _speechToTextServiceMock.Setup(s => s.TranscribeAudioAsync(audioBytes, "audio/webm", "en-US", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechToTextResult("My water heater broke", "Water heater is broken.", 0.92, "MockSpeechToTextService"));

        var result = await _sut.TranscribeIssue("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AiOperationResponseDto<IssueTranscriptionResultDto>>().Subject;
        dto.Success.Should().BeTrue();
        dto.Result!.RawTranscript.Should().Be("My water heater broke");
        dto.Result.CleanedDescription.Should().Be("Water heater is broken.");
        dto.Confidence.Should().Be(0.92);
        dto.Provider.Should().Be("MockSpeechToTextService");
    }

    [Fact]
    public async Task TranscribeIssue_WhenTranscriptionReturnsNull_ShouldReturnOkWithNullResultAndWarning()
    {
        var audioBytes = new byte[] { 0x00, 0x01, 0x02 };
        var base64 = Convert.ToBase64String(audioBytes);
        var request = new IssueTranscriptionRequestDto { AudioBase64 = base64, ContentType = "audio/webm" };

        _speechToTextServiceMock.Setup(s => s.TranscribeAudioAsync(audioBytes, "audio/webm", "en-US", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpeechToTextResult?)null);

        var result = await _sut.TranscribeIssue("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AiOperationResponseDto<IssueTranscriptionResultDto>>().Subject;
        dto.Success.Should().BeTrue();
        dto.Result.Should().BeNull();
        dto.Confidence.Should().Be(0.0);
        dto.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task TranscribeIssue_WhenAudioBase64IsEmpty_ShouldReturn400()
    {
        var request = new IssueTranscriptionRequestDto { AudioBase64 = "", ContentType = "audio/webm" };

        var result = await _sut.TranscribeIssue("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TranscribeIssue_WhenContentTypeIsNotAudio_ShouldReturn400()
    {
        var base64 = Convert.ToBase64String(new byte[] { 0x00 });
        var request = new IssueTranscriptionRequestDto { AudioBase64 = base64, ContentType = "application/json" };

        var result = await _sut.TranscribeIssue("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TranscribeIssue_WhenAudioBase64IsInvalid_ShouldReturn400()
    {
        var request = new IssueTranscriptionRequestDto { AudioBase64 = "not-valid-base64!!!", ContentType = "audio/webm" };

        var result = await _sut.TranscribeIssue("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TranscribeIssue_WhenAudioExceedsMaxSize_ShouldReturn413()
    {
        var largeBase64 = Convert.ToBase64String(new byte[11 * 1024 * 1024]);
        var request = new IssueTranscriptionRequestDto { AudioBase64 = largeBase64, ContentType = "audio/webm" };

        var result = await _sut.TranscribeIssue("test-slug", request);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(413);
    }

    [Fact]
    public async Task TranscribeIssue_WhenLocaleProvided_ShouldUseProvidedLocale()
    {
        var audioBytes = new byte[] { 0x00 };
        var base64 = Convert.ToBase64String(audioBytes);
        var request = new IssueTranscriptionRequestDto { AudioBase64 = base64, ContentType = "audio/webm", Locale = "es-MX" };

        _speechToTextServiceMock.Setup(s => s.TranscribeAudioAsync(audioBytes, "audio/webm", "es-MX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechToTextResult("Mi calentador de agua se rompió", null, 0.88, "MockSpeechToTextService"));

        var result = await _sut.TranscribeIssue("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AiOperationResponseDto<IssueTranscriptionResultDto>>().Subject;
        dto.Result!.RawTranscript.Should().Be("Mi calentador de agua se rompió");
    }

    // ── RefineIssueText ──────────────────────────────────────────────────

    [Fact]
    public async Task RefineIssueText_WhenRefinementSucceeds_ShouldReturnOkWithCleanedDescription()
    {
        var request = new IssueTextRefinementRequestDto { RawTranscript = "um my water heater stopped working" };

        _issueTextRefinementServiceMock.Setup(s => s.RefineTranscriptAsync("um my water heater stopped working", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueTextRefinementResult("Water heater stopped working.", 0.88, "RuleBasedIssueTextRefinementService"));

        var result = await _sut.RefineIssueText("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AiOperationResponseDto<IssueTextRefinementResultDto>>().Subject;
        dto.Success.Should().BeTrue();
        dto.Result!.CleanedDescription.Should().Be("Water heater stopped working.");
        dto.Confidence.Should().Be(0.88);
    }

    [Fact]
    public async Task RefineIssueText_WhenRefinementReturnsNull_ShouldReturnOkWithNullResult()
    {
        var request = new IssueTextRefinementRequestDto { RawTranscript = "some text" };

        _issueTextRefinementServiceMock.Setup(s => s.RefineTranscriptAsync("some text", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IssueTextRefinementResult?)null);

        var result = await _sut.RefineIssueText("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AiOperationResponseDto<IssueTextRefinementResultDto>>().Subject;
        dto.Result.Should().BeNull();
        dto.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task RefineIssueText_WhenRawTranscriptIsEmpty_ShouldReturn400()
    {
        var request = new IssueTextRefinementRequestDto { RawTranscript = "" };

        var result = await _sut.RefineIssueText("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefineIssueText_WhenRawTranscriptExceedsMaxLength_ShouldReturn400()
    {
        var request = new IssueTextRefinementRequestDto { RawTranscript = new string('x', 4001) };

        var result = await _sut.RefineIssueText("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SuggestCategory ──────────────────────────────────────────────────

    [Fact]
    public async Task SuggestCategory_WhenSuggestionSucceeds_ShouldReturnOkWithCategory()
    {
        var request = new IssueCategorySuggestionRequestDto { IssueDescription = "The battery is dead" };

        _issueTextRefinementServiceMock.Setup(s => s.SuggestCategoryAsync("The battery is dead", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCategorySuggestionResult("Electrical", 0.85, "RuleBasedIssueTextRefinementService"));

        var result = await _sut.SuggestCategory("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AiOperationResponseDto<IssueCategorySuggestionResultDto>>().Subject;
        dto.Success.Should().BeTrue();
        dto.Result!.IssueCategory.Should().Be("Electrical");
        dto.Confidence.Should().Be(0.85);
    }

    [Fact]
    public async Task SuggestCategory_WhenSuggestionReturnsNull_ShouldReturnOkWithNullResult()
    {
        var request = new IssueCategorySuggestionRequestDto { IssueDescription = "something is wrong" };

        _issueTextRefinementServiceMock.Setup(s => s.SuggestCategoryAsync("something is wrong", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IssueCategorySuggestionResult?)null);

        var result = await _sut.SuggestCategory("test-slug", request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AiOperationResponseDto<IssueCategorySuggestionResultDto>>().Subject;
        dto.Result.Should().BeNull();
        dto.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task SuggestCategory_WhenDescriptionIsEmpty_ShouldReturn400()
    {
        var request = new IssueCategorySuggestionRequestDto { IssueDescription = "" };

        var result = await _sut.SuggestCategory("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SuggestCategory_WhenDescriptionExceedsMaxLength_ShouldReturn400()
    {
        var request = new IssueCategorySuggestionRequestDto { IssueDescription = new string('x', 2001) };

        var result = await _sut.SuggestCategory("test-slug", request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
