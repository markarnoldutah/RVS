using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Tests.Mappers;

public class ServiceRequestMapperApplyUpdateTests
{
    [Fact]
    public void ApplyUpdate_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        var dto = BuildUpdateRequest();

        var act = () => ServiceRequestMapper.ApplyUpdate(null!, dto, "usr_1");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyUpdate_WhenDtoIsNull_ShouldThrowArgumentNullException()
    {
        var entity = BuildServiceRequest();

        var act = () => entity.ApplyUpdate(null!, "usr_1");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyUpdate_ShouldApplyAllFields()
    {
        var entity = BuildServiceRequest();

        var dto = new ServiceRequestUpdateRequestDto
        {
            Status = "InProgress",
            IssueDescription = "  Updated description  ",
            IssueCategory = "  Electrical  ",
            TechnicianSummary = "  Check wiring  ",
            Priority = "  Low  ",
            Urgency = "  This week  ",
            RvUsage = "  Full-time  ",
            HasExtendedWarranty = "  Yes  ",
            ApproxPurchaseDate = "  March 2023  ",
            AssignedTechnicianId = "  tech_1  ",
            ScheduledDateUtc = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            RequiredSkills = ["electrical", "plumbing"],
            BoardSequence = 4
        };

        entity.ApplyUpdate(dto, "usr_updater");

        entity.Status.Should().Be("InProgress");
        entity.IssueDescription.Should().Be("Updated description");
        entity.IssueCategory.Should().Be("Electrical");
        entity.TechnicianSummary.Should().Be("Check wiring");
        entity.Priority.Should().Be("Low");
        entity.Urgency.Should().Be("This week");
        entity.RvUsage.Should().Be("Full-time");
        entity.HasExtendedWarranty.Should().Be("Yes");
        entity.ApproxPurchaseDate.Should().Be("March 2023");
        entity.AssignedTechnicianId.Should().Be("tech_1");
        entity.ScheduledDateUtc.Should().Be(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));
        entity.RequiredSkills.Should().BeEquivalentTo(["electrical", "plumbing"]);
        entity.BoardSequence.Should().Be(4);
    }

    [Fact]
    public void ApplyUpdate_ShouldCallMarkAsUpdated()
    {
        var entity = BuildServiceRequest();

        entity.ApplyUpdate(BuildUpdateRequest(), "usr_updater");

        entity.UpdatedByUserId.Should().Be("usr_updater");
        entity.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void ApplyUpdate_WhenNullableFieldsAreNull_ShouldSetToNull()
    {
        var entity = BuildServiceRequest();
        entity.IssueCategory = "Plumbing";
        entity.TechnicianSummary = "Existing summary";

        var dto = new ServiceRequestUpdateRequestDto
        {
            Status = "New",
            IssueDescription = "desc",
            Priority = "High",
            IssueCategory = null,
            TechnicianSummary = null
        };

        entity.ApplyUpdate(dto, "usr_1");

        entity.IssueCategory.Should().BeNull();
        entity.TechnicianSummary.Should().BeNull();
    }

    [Fact]
    public void ApplyUpdate_WhenBoardSequenceIsNull_ShouldPreserveExistingValue()
    {
        var entity = BuildServiceRequest();
        entity.BoardSequence = 5;

        var dto = BuildUpdateRequest() with { BoardSequence = null };

        entity.ApplyUpdate(dto, "usr_1");

        entity.BoardSequence.Should().Be(5);
    }

    [Fact]
    public void ApplyUpdate_WhenBoardSequenceIsProvided_ShouldUpdateValue()
    {
        var entity = BuildServiceRequest();
        entity.BoardSequence = 5;

        var dto = BuildUpdateRequest() with { BoardSequence = 2 };

        entity.ApplyUpdate(dto, "usr_1");

        entity.BoardSequence.Should().Be(2);
    }

    [Fact]
    public void ApplyUpdate_WhenCustomerIsNull_ShouldPreserveExistingCustomerSnapshot()
    {
        var entity = BuildServiceRequest();
        entity.CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "555-1212"
        };

        var dto = BuildUpdateRequest() with { Customer = null };

        entity.ApplyUpdate(dto, "usr_1");

        entity.CustomerSnapshot.FirstName.Should().Be("Jane");
        entity.CustomerSnapshot.LastName.Should().Be("Doe");
        entity.CustomerSnapshot.Email.Should().Be("jane@example.com");
        entity.CustomerSnapshot.Phone.Should().Be("555-1212");
    }

    [Fact]
    public void ApplyUpdate_WhenCustomerIsProvided_ShouldReplaceCustomerSnapshotAndTrimFields()
    {
        var entity = BuildServiceRequest();

        var dto = BuildUpdateRequest() with
        {
            Customer = new CustomerInfoDto
            {
                FirstName = "  John  ",
                LastName = "  Smith  ",
                Email = "  john@example.com  ",
                Phone = "  555-9999  ",
                PreferredContact = "  text  "
            }
        };

        entity.ApplyUpdate(dto, "usr_1");

        entity.CustomerSnapshot.FirstName.Should().Be("John");
        entity.CustomerSnapshot.LastName.Should().Be("Smith");
        entity.CustomerSnapshot.Email.Should().Be("john@example.com");
        entity.CustomerSnapshot.Phone.Should().Be("555-9999");
        entity.CustomerSnapshot.PreferredContact.Should().Be("Text");
    }

    [Fact]
    public void ApplyUpdate_WhenAssetIsNull_ShouldPreserveExistingAssetInfo()
    {
        var entity = BuildServiceRequest();
        entity.AssetInfo = new AssetInfoEmbedded
        {
            AssetId = "VIN123",
            Manufacturer = "Winnebago",
            Model = "Vista",
            Year = 2022
        };

        var dto = BuildUpdateRequest() with { Asset = null };

        entity.ApplyUpdate(dto, "usr_1");

        entity.AssetInfo.AssetId.Should().Be("VIN123");
        entity.AssetInfo.Manufacturer.Should().Be("Winnebago");
        entity.AssetInfo.Model.Should().Be("Vista");
        entity.AssetInfo.Year.Should().Be(2022);
    }

    [Fact]
    public void ApplyUpdate_WhenAssetIsProvided_ShouldReplaceAssetInfoAndTrimFields()
    {
        var entity = BuildServiceRequest();

        var dto = BuildUpdateRequest() with
        {
            Asset = new AssetInfoDto
            {
                AssetId = "  VIN999  ",
                Manufacturer = "  Thor  ",
                Model = "  Aria  ",
                Year = 2024
            }
        };

        entity.ApplyUpdate(dto, "usr_1");

        entity.AssetInfo.AssetId.Should().Be("VIN999");
        entity.AssetInfo.Manufacturer.Should().Be("Thor");
        entity.AssetInfo.Model.Should().Be("Aria");
        entity.AssetInfo.Year.Should().Be(2024);
    }

    private static ServiceRequest BuildServiceRequest() => new()
    {
        Id = "sr_test",
        TenantId = "ten_1",
        Status = "New",
        LocationId = "loc_1",
        IssueDescription = "Original description",
        IssueCategory = "Plumbing",
        Priority = "High"
    };

    private static ServiceRequestUpdateRequestDto BuildUpdateRequest() => new()
    {
        Status = "New",
        IssueDescription = "Water heater not working",
        Priority = "High"
    };
}
