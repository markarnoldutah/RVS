using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
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
    private readonly Mock<IVinDecoderService> _vinDecoderMock = new();
    private readonly IntakeController _sut;

    public IntakeControllerTests()
    {
        _sut = new IntakeController(
            _intakeServiceMock.Object,
            _categorizationMock.Object,
            _attachmentServiceMock.Object,
            _vinDecoderMock.Object);
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

    [Fact]
    public async Task DecodeVin_WhenVinDecodesSuccessfully_ShouldReturnOkWithVinDecodeDto()
    {
        var vinResult = new VinDecoderResult("1RGDE4428R1000001", "Grand Design", "Momentum 395MS", 2024);
        _vinDecoderMock.Setup(s => s.DecodeVinAsync("1RGDE4428R1000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinResult);

        var result = await _sut.DecodeVin("test-slug", "1RGDE4428R1000001");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<VinDecodeResponseDto>().Subject;
        dto.Vin.Should().Be("1RGDE4428R1000001");
        dto.Manufacturer.Should().Be("Grand Design");
        dto.Model.Should().Be("Momentum 395MS");
        dto.Year.Should().Be(2024);
    }

    [Fact]
    public async Task DecodeVin_WhenVinCannotBeDecoded_ShouldReturnNotFound()
    {
        _vinDecoderMock.Setup(s => s.DecodeVinAsync("INVALID12345678901", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinDecoderResult?)null);

        var result = await _sut.DecodeVin("test-slug", "INVALID12345678901");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
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
}
