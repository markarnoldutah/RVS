using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Tests.Mappers;

/// <summary>
/// Job type (<c>Spec C-11</c>, issue #843) through the service-request mapper: set, change and
/// clear on update, and carried out on the detail and summary DTOs.
/// </summary>
public class ServiceRequestMapperJobTypeTests
{
    [Theory]
    [InlineData("Remote")]
    [InlineData("OnSite")]
    [InlineData("InShop")]
    [InlineData("Inspection")]
    [InlineData("Install")]
    [InlineData("Other")]
    public void ApplyUpdate_WhenJobTypeSet_ShouldStoreIt(string jobType)
    {
        var entity = BuildServiceRequest();

        entity.ApplyUpdate(BuildUpdateRequest() with { JobType = jobType }, "usr_1");

        entity.JobType.Should().Be(jobType);
    }

    [Fact]
    public void ApplyUpdate_WhenJobTypeChanged_ShouldReplaceIt()
    {
        var entity = BuildServiceRequest();
        entity.JobType = "Remote";

        entity.ApplyUpdate(BuildUpdateRequest() with { JobType = "OnSite" }, "usr_1");

        entity.JobType.Should().Be("OnSite");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyUpdate_WhenJobTypeNullOrBlank_ShouldClearIt(string? jobType)
    {
        var entity = BuildServiceRequest();
        entity.JobType = "Inspection";

        entity.ApplyUpdate(BuildUpdateRequest() with { JobType = jobType }, "usr_1");

        entity.JobType.Should().BeNull();
    }

    [Fact]
    public void ApplyUpdate_ShouldTrimJobType()
    {
        var entity = BuildServiceRequest();

        entity.ApplyUpdate(BuildUpdateRequest() with { JobType = "  InShop  " }, "usr_1");

        entity.JobType.Should().Be("InShop");
    }

    [Fact]
    public void ToDetailDto_ShouldMapJobType()
    {
        var entity = BuildServiceRequest();
        entity.JobType = "Install";

        entity.ToDetailDto().JobType.Should().Be("Install");
    }

    [Fact]
    public void ToSummaryDto_ShouldMapJobType()
    {
        var entity = BuildServiceRequest();
        entity.JobType = "Remote";

        entity.ToSummaryDto().JobType.Should().Be("Remote");
    }

    [Fact]
    public void ToSummaryDto_WhenJobTypeUnset_ShouldMapNull()
    {
        BuildServiceRequest().ToSummaryDto().JobType.Should().BeNull();
    }

    [Fact]
    public void ToEntity_WhenJobTypeSet_ShouldStoreItTrimmed()
    {
        var entity = BuildCreateRequest("  Inspection  ").ToEntity("ten_1", "usr_1");

        entity.JobType.Should().Be("Inspection");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToEntity_WhenJobTypeNullOrBlank_ShouldLeaveItUnset(string? jobType)
    {
        var entity = BuildCreateRequest(jobType).ToEntity("ten_1", "usr_1");

        entity.JobType.Should().BeNull();
    }

    private static ServiceRequestCreateRequestDto BuildCreateRequest(string? jobType) => new()
    {
        Customer = new CustomerInfoDto { FirstName = "Ann", LastName = "Lee", Email = "ann@example.com" },
        Asset = new AssetInfoDto { AssetId = "1HGBH41JXMN109186" },
        IssueCategory = "Plumbing",
        IssueDescription = "Water heater not working",
        JobType = jobType,
    };

    private static ServiceRequest BuildServiceRequest() => new()
    {
        Id = "sr_1",
        TenantId = "ten_1",
        Status = "New",
        LocationId = "loc_1",
        CustomerProfileId = "cp_1",
        IssueDescription = "Water heater not working",
        Priority = "High",
        CustomerSnapshot = new CustomerSnapshotEmbedded { FirstName = "Ann", LastName = "Lee", Email = "ann@example.com" },
    };

    private static ServiceRequestUpdateRequestDto BuildUpdateRequest() => new()
    {
        Status = "New",
        IssueDescription = "Water heater not working",
        Priority = "High",
    };
}
