using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
using RVS.API.Services;
using RVS.Domain.DTOs;
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
    public async Task GetUploadSas_ShouldReturnOkWithUploadSasResponse()
    {
        var sasResponse = new AttachmentUploadSasResponseDto
        {
            SasUrl = "https://blob.example.com/sas?sig=upload",
            BlobName = "ten_test/sr_1/guid_photo.jpg",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };
        _serviceMock.Setup(s => s.GenerateUploadSasAsync(TenantId, "sr_1", "photo.jpg", "image/jpeg", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sasResponse);

        var result = await _sut.GetUploadSas("dlr_1", "sr_1", "photo.jpg", "image/jpeg", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AttachmentUploadSasResponseDto>().Subject;
        dto.SasUrl.Should().Contain("blob.example.com");
        dto.BlobName.Should().Contain("sr_1");
    }

    [Fact]
    public async Task ConfirmUpload_ShouldReturnCreatedAtActionWithAttachmentDto()
    {
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
        _serviceMock.Setup(s => s.ConfirmAttachmentAsync(TenantId, "sr_1", request, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachmentDto);

        var result = await _sut.ConfirmUpload("dlr_1", "sr_1", request, CancellationToken.None);

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
