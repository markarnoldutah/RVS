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
            AssignedBayId = "  bay_2  ",
            ScheduledDateUtc = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            RequiredSkills = ["electrical", "plumbing"],
            BoardSequence = 4,
            ServiceEvent = new ServiceEventDto
            {
                ComponentType = "  Wiring  ",
                FailureMode = "  Short Circuit  ",
                RepairAction = "  Replaced  ",
                PartsUsed = ["Wire", "Connector"],
                LaborHours = 2.5m,
                ServiceDateUtc = new DateTime(2026, 6, 14, 8, 0, 0, DateTimeKind.Utc)
            }
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
        entity.AssignedBayId.Should().Be("bay_2");
        entity.ScheduledDateUtc.Should().Be(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));
        entity.RequiredSkills.Should().BeEquivalentTo(["electrical", "plumbing"]);
        entity.BoardSequence.Should().Be(4);
        entity.ServiceEvent.Should().NotBeNull();
        entity.ServiceEvent!.ComponentType.Should().Be("Wiring");
        entity.ServiceEvent.FailureMode.Should().Be("Short Circuit");
        entity.ServiceEvent.RepairAction.Should().Be("Replaced");
        entity.ServiceEvent.PartsUsed.Should().BeEquivalentTo(["Wire", "Connector"]);
        entity.ServiceEvent.LaborHours.Should().Be(2.5m);
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
    public void ApplyUpdate_WhenServiceEventIsNull_ShouldSetToNull()
    {
        var entity = BuildServiceRequest();
        entity.ServiceEvent = new ServiceEventEmbedded { FailureMode = "Leak" };

        var dto = BuildUpdateRequest() with { ServiceEvent = null };

        entity.ApplyUpdate(dto, "usr_1");

        entity.ServiceEvent.Should().BeNull();
    }

    [Fact]
    public void ToEmbedded_ServiceEventDto_ShouldMapAllFields()
    {
        var dto = new ServiceEventDto
        {
            ComponentType = "  Wiring  ",
            FailureMode = "  Short  ",
            RepairAction = "  Replaced  ",
            PartsUsed = ["Part1"],
            LaborHours = 1.5m,
            ServiceDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var embedded = dto.ToEmbedded();

        embedded.ComponentType.Should().Be("Wiring");
        embedded.FailureMode.Should().Be("Short");
        embedded.RepairAction.Should().Be("Replaced");
        embedded.PartsUsed.Should().BeEquivalentTo(["Part1"]);
        embedded.LaborHours.Should().Be(1.5m);
        embedded.ServiceDateUtc.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToDto_ServiceEventEmbedded_ShouldMapAllFields()
    {
        var embedded = new ServiceEventEmbedded
        {
            ComponentType = "Engine",
            FailureMode = "Overheating",
            RepairAction = "Coolant flush",
            PartsUsed = ["Coolant"],
            LaborHours = 3.0m,
            ServiceDateUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var dto = embedded.ToDto();

        dto.ComponentType.Should().Be("Engine");
        dto.FailureMode.Should().Be("Overheating");
        dto.RepairAction.Should().Be("Coolant flush");
        dto.PartsUsed.Should().BeEquivalentTo(["Coolant"]);
        dto.LaborHours.Should().Be(3.0m);
        dto.ServiceDateUtc.Should().Be(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
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
