using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

public class AttachmentsControllerTests
{
    private readonly Mock<IAttachmentService> _serviceMock = new();
    private readonly ClaimsService _claimsService;
    private readonly AttachmentsController _sut;

    private const string TenantId = "ten_test";

    public AttachmentsControllerTests()
    {
        _claimsService = BuildClaimsService(TenantId);
        _sut = new AttachmentsController(_serviceMock.Object, _claimsService);
    }

    [Fact]
    public async Task Upload_ShouldReturnCreatedAtActionWithAttachmentDto()
    {
        var sr = BuildServiceRequestWithAttachment();
        _serviceMock.Setup(s => s.CreateAttachmentAsync(
                TenantId, "sr_1", "photo.jpg", "image/jpeg", It.IsAny<Stream>(),
                10, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sr);

        var file = CreateFormFile("photo.jpg", "image/jpeg");
        var result = await _sut.Upload("dlr_1", "sr_1", file, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<AttachmentDto>().Subject;
        dto.FileName.Should().Be("photo.jpg");
    }

    [Fact]
    public async Task GetReadSas_ShouldReturnOkWithSasDto()
    {
        var sasDto = new AttachmentSasDto { SasUrl = "https://blob.example.com/sas", ExpiresAtUtc = DateTime.UtcNow.AddHours(1) };
        _serviceMock.Setup(s => s.GenerateReadSasAsync(TenantId, "sr_1", "att_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sasDto);

        var result = await _sut.GetReadSas("dlr_1", "sr_1", "att_1", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AttachmentSasDto>().Subject;
        dto.SasUrl.Should().Contain("blob.example.com");
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent()
    {
        _serviceMock.Setup(s => s.DeleteAttachmentAsync(TenantId, "sr_1", "att_1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Delete("dlr_1", "sr_1", "att_1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    private static ServiceRequest BuildServiceRequestWithAttachment() => new()
    {
        Id = "sr_1",
        TenantId = TenantId,
        LocationId = "loc_1",
        Status = "New",
        IssueCategory = "Electrical",
        IssueDescription = "Test",
        CreatedByUserId = "intake",
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com"
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = "RV:1FTFW1ET5EKE12345" },
        Attachments =
        [
            new ServiceRequestAttachmentEmbedded
            {
                AttachmentId = "att_1",
                FileName = "photo.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 1024,
                BlobUri = "ten_test/sr_1/att_1_photo.jpg"
            }
        ]
    };

    private static IFormFile CreateFormFile(string fileName, string contentType)
    {
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var stream = new MemoryStream(content);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(content.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        return fileMock.Object;
    }

    private static ClaimsService BuildClaimsService(string tenantId)
    {
        var claims = new List<Claim> { new(ClaimsService.TenantIdClaimType, tenantId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new ClaimsService(accessor.Object);
    }
}
