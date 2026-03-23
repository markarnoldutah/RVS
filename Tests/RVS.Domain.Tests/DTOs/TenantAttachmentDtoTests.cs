using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

public class TenantAttachmentDtoTests
{
    [Fact]
    public void AccessGateStatusDto_DefaultValues()
    {
        var dto = new AccessGateStatusDto();

        dto.IsEnabled.Should().BeFalse();
        dto.DisabledReason.Should().BeNull();
        dto.DisabledMessage.Should().BeNull();
    }

    [Fact]
    public void AccessGateStatusDto_CanSetEnabled()
    {
        var dto = new AccessGateStatusDto { IsEnabled = true };

        dto.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AttachmentDto_CanSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var dto = new AttachmentDto
        {
            AttachmentId = "att-1",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024000,
            BlobUri = "https://storage.blob.core.windows.net/attachments/photo.jpg",
            CreatedAtUtc = now
        };

        dto.AttachmentId.Should().Be("att-1");
        dto.FileName.Should().Be("photo.jpg");
        dto.SizeBytes.Should().Be(1024000);
        dto.CreatedAtUtc.Should().Be(now);
    }

    [Fact]
    public void AttachmentSasDto_CanSetAllProperties()
    {
        var expires = DateTime.UtcNow.AddHours(1);
        var dto = new AttachmentSasDto
        {
            SasUrl = "https://storage.blob.core.windows.net/attachments/photo.jpg?sv=2024&sig=abc",
            ExpiresAtUtc = expires
        };

        dto.SasUrl.Should().Contain("sig=abc");
        dto.ExpiresAtUtc.Should().Be(expires);
    }

    [Fact]
    public void PagedResult_IsSealed_WithCorrectDefaults()
    {
        var result = new PagedResult<ServiceRequestSummaryResponseDto>();

        result.Page.Should().Be(0);
        result.PageSize.Should().Be(0);
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.ContinuationToken.Should().BeNull();
    }

    [Fact]
    public void PagedResult_CanSetProperties()
    {
        var items = new List<ServiceRequestSummaryResponseDto>
        {
            new() { Id = "sr-1", Status = "New", LocationId = "loc-1", CustomerFullName = "Jane Doe", IssueCategory = "Slide" }
        };

        var result = new PagedResult<ServiceRequestSummaryResponseDto>
        {
            Page = 1,
            PageSize = 25,
            TotalCount = 100,
            Items = items,
            ContinuationToken = "token123"
        };

        result.Page.Should().Be(1);
        result.TotalCount.Should().Be(100);
        result.Items.Should().HaveCount(1);
        result.ContinuationToken.Should().Be("token123");
    }
}
