using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

public class ServiceRequestDtoTests
{
    [Fact]
    public void ServiceRequestCreateRequestDto_SetsRequiredProperties()
    {
        var dto = new ServiceRequestCreateRequestDto
        {
            Customer = new CustomerInfoDto { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com" },
            Asset = new AssetInfoDto { Vin = "1HGBH41JXMN109186" },
            IssueCategory = "Slide System",
            IssueDescription = "Slide won't retract"
        };

        dto.Customer.FirstName.Should().Be("Jane");
        dto.Asset.Vin.Should().Be("1HGBH41JXMN109186");
        dto.IssueCategory.Should().Be("Slide System");
        dto.IssueDescription.Should().Be("Slide won't retract");
        dto.Urgency.Should().BeNull();
        dto.RvUsage.Should().BeNull();
        dto.DiagnosticResponses.Should().BeNull();
    }

    [Fact]
    public void ServiceRequestSearchRequestDto_HasCorrectDefaults()
    {
        var dto = new ServiceRequestSearchRequestDto();

        dto.Page.Should().Be(1);
        dto.PageSize.Should().Be(25);
        dto.Keyword.Should().BeNull();
        dto.Status.Should().BeNull();
        dto.LocationId.Should().BeNull();
    }

    [Fact]
    public void ServiceRequestDetailResponseDto_DefaultCollectionsAreEmpty()
    {
        var dto = new ServiceRequestDetailResponseDto();

        dto.RequiredSkills.Should().BeEmpty();
        dto.DiagnosticResponses.Should().BeEmpty();
        dto.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void ServiceRequestSummaryResponseDto_CanSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var dto = new ServiceRequestSummaryResponseDto
        {
            Id = "sr-1",
            LocationId = "loc-1",
            Status = "New",
            CustomerFullName = "Jane Doe",
            AssetDisplay = "2023 Grand Design Momentum 395G",
            IssueCategory = "Slide System",
            TechnicianSummary = "Needs slide motor replacement",
            AttachmentCount = 3,
            AssignedTechnicianId = "tech-1",
            Priority = "High",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dto.Id.Should().Be("sr-1");
        dto.CustomerFullName.Should().Be("Jane Doe");
        dto.AttachmentCount.Should().Be(3);
        dto.Priority.Should().Be("High");
    }

    [Fact]
    public void ServiceRequestSearchResultResponseDto_HasDefaultEmptyResults()
    {
        var dto = new ServiceRequestSearchResultResponseDto();

        dto.Results.Should().NotBeNull();
        dto.Results.Items.Should().BeEmpty();
    }

    [Fact]
    public void ServiceRequestDetailResponseDto_WithExpression()
    {
        var customer = new CustomerInfoDto { FirstName = "Jane", LastName = "Doe", Email = "jane@test.com" };
        var asset = new AssetInfoDto { Vin = "ABC123" };

        var dto = new ServiceRequestDetailResponseDto
        {
            Id = "sr-1",
            Status = "New",
            LocationId = "loc-1",
            Customer = customer,
            Asset = asset,
            IssueCategory = "Electrical",
            IssueDescription = "Test"
        };

        var updated = dto with { Status = "InProgress", Priority = "High" };

        updated.Status.Should().Be("InProgress");
        updated.Priority.Should().Be("High");
        updated.Id.Should().Be("sr-1");
        updated.Customer.Should().Be(customer);
    }
}
