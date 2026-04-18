using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Tests.Mappers;

public class LocationMapperTests
{
    // ── ToDetailDto ──────────────────────────────────────────────────────────

    [Fact]
    public void ToDetailDto_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        Location? entity = null;

        var act = () => entity!.ToDetailDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToDetailDto_ShouldMapAllFields()
    {
        var now = DateTime.UtcNow;
        var entity = new Location
        {
            TenantId = "ten_1",
            Name = "Salt Lake Service Center",
            Slug = "salt-lake-service-center",
            Phone = "(801) 555-0100",
            Address = new AddressEmbedded
            {
                Address1 = "123 Main St",
                City = "Salt Lake City",
                State = "UT",
                PostalCode = "84101"
            },
            IntakeConfig = new IntakeFormConfigEmbedded
            {
                MaxFileSizeMb = 50,
                MaxAttachments = 5,
                AllowAnonymousIntake = false
            },
            CreatedAtUtc = now
        };

        var dto = entity.ToDetailDto();

        dto.Id.Should().Be(entity.Id);
        dto.TenantId.Should().Be("ten_1");
        dto.Name.Should().Be("Salt Lake Service Center");
        dto.Slug.Should().Be("salt-lake-service-center");
        dto.Phone.Should().Be("(801) 555-0100");
        dto.Address.Should().NotBeNull();
        dto.Address!.Address1.Should().Be("123 Main St");
        dto.Address.City.Should().Be("Salt Lake City");
        dto.Address.State.Should().Be("UT");
        dto.Address.PostalCode.Should().Be("84101");
        dto.IntakeConfig.Should().NotBeNull();
        dto.IntakeConfig!.MaxFileSizeMb.Should().Be(50);
        dto.IntakeConfig.AllowAnonymousIntake.Should().BeFalse();
        dto.CreatedAtUtc.Should().Be(now);
        dto.UpdatedAtUtc.Should().BeNull();
    }

    // ── ToSummaryDto ─────────────────────────────────────────────────────────

    [Fact]
    public void ToSummaryDto_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        Location? entity = null;

        var act = () => entity!.ToSummaryDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToSummaryDto_ShouldMapAllFields()
    {
        var entity = new Location
        {
            TenantId = "ten_1",
            Name = "Denver Service Center",
            Slug = "denver-service-center",
            Phone = "(303) 555-0100",
            Address = new AddressEmbedded
            {
                Address1 = "789 Broadway",
                City = "Denver",
                State = "CO",
                PostalCode = "80202"
            }
        };

        var dto = entity.ToSummaryDto();

        dto.LocationId.Should().Be(entity.Id);
        dto.Name.Should().Be("Denver Service Center");
        dto.Slug.Should().Be("denver-service-center");
        dto.Phone.Should().Be("(303) 555-0100");
        dto.Address.Should().NotBeNull();
        dto.Address!.City.Should().Be("Denver");
        dto.CreatedAtUtc.Should().Be(entity.CreatedAtUtc);
    }

    // ── ToEntity (create) ────────────────────────────────────────────────────

    [Fact]
    public void ToEntity_WhenDtoIsNull_ShouldThrowArgumentNullException()
    {
        LocationCreateRequestDto? dto = null;

        var act = () => dto!.ToEntity("ten_1", "usr_1");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToEntity_WhenTenantIdIsEmpty_ShouldThrowArgumentException()
    {
        var dto = BuildValidCreateRequest();

        var act = () => dto.ToEntity("", "usr_1");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToEntity_WhenCreatedByUserIdIsEmpty_ShouldThrowArgumentException()
    {
        var dto = BuildValidCreateRequest();

        var act = () => dto.ToEntity("ten_1", "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToEntity_ShouldMapAllFields()
    {
        var dto = BuildValidCreateRequest();

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.TenantId.Should().Be("ten_1");
        entity.CreatedByUserId.Should().Be("usr_1");
        entity.Name.Should().Be("Phoenix Service Center");
        entity.Slug.Should().Be("phoenix-service-center");
        entity.Phone.Should().Be("(602) 555-0200");
    }

    [Fact]
    public void ToEntity_ShouldLowercaseSlug()
    {
        var dto = BuildValidCreateRequest() with { Slug = "Phoenix-Service-CENTER" };

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.Slug.Should().Be("phoenix-service-center");
    }

    [Fact]
    public void ToEntity_ShouldTrimNameAndSlug()
    {
        var dto = BuildValidCreateRequest() with
        {
            Name = "  Phoenix Service Center  ",
            Slug = "  phoenix-service-center  "
        };

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.Name.Should().Be("Phoenix Service Center");
        entity.Slug.Should().Be("phoenix-service-center");
    }

    [Fact]
    public void ToEntity_ShouldMapAddress()
    {
        var dto = BuildValidCreateRequest() with
        {
            Address = new AddressDto
            {
                Address1 = "456 Desert Rd",
                City = "Phoenix",
                State = "AZ",
                PostalCode = "85001"
            }
        };

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.Address.Address1.Should().Be("456 Desert Rd");
        entity.Address.City.Should().Be("Phoenix");
        entity.Address.State.Should().Be("AZ");
        entity.Address.PostalCode.Should().Be("85001");
    }

    [Fact]
    public void ToEntity_WhenNoAddress_ShouldUseEmptyAddressEmbedded()
    {
        var dto = BuildValidCreateRequest() with { Address = null };

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.Address.Should().NotBeNull();
        entity.Address.Address1.Should().BeNull();
    }

    // ── ApplyUpdate ──────────────────────────────────────────────────────────

    [Fact]
    public void ApplyUpdate_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        Location? entity = null;
        var dto = BuildValidCreateRequest();

        var act = () => entity!.ApplyUpdate(dto, "usr_1");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyUpdate_WhenDtoIsNull_ShouldThrowArgumentNullException()
    {
        var entity = new Location { TenantId = "ten_1", Name = "Old Name" };

        var act = () => entity.ApplyUpdate(null!, "usr_1");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyUpdate_ShouldMutateEntityAndCallMarkAsUpdated()
    {
        var entity = new Location { TenantId = "ten_1", Name = "Old Name", Slug = "old-slug" };
        var dto = BuildValidCreateRequest();

        entity.ApplyUpdate(dto, "usr_updater");

        entity.Name.Should().Be("Phoenix Service Center");
        entity.Slug.Should().Be("phoenix-service-center");
        entity.UpdatedByUserId.Should().Be("usr_updater");
        entity.UpdatedAtUtc.Should().NotBeNull();
    }

    // ── AddressEmbedded ↔ AddressDto ─────────────────────────────────────────

    [Fact]
    public void AddressToDto_WhenNull_ShouldThrowArgumentNullException()
    {
        AddressEmbedded? address = null;

        var act = () => address!.ToDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddressToDto_ShouldMapAllFields()
    {
        var address = new AddressEmbedded
        {
            Address1 = "100 Pine St",
            Address2 = "Suite 5",
            City = "Boise",
            State = "ID",
            PostalCode = "83702"
        };

        var dto = address.ToDto();

        dto.Address1.Should().Be("100 Pine St");
        dto.Address2.Should().Be("Suite 5");
        dto.City.Should().Be("Boise");
        dto.State.Should().Be("ID");
        dto.PostalCode.Should().Be("83702");
    }

    [Fact]
    public void AddressToEmbedded_WhenNull_ShouldThrowArgumentNullException()
    {
        AddressDto? dto = null;

        var act = () => dto!.ToEmbedded();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddressToEmbedded_ShouldTrimStringFields()
    {
        var dto = new AddressDto
        {
            Address1 = "  100 Pine St  ",
            City = " Boise ",
            State = " ID ",
            PostalCode = " 83702 "
        };

        var embedded = dto.ToEmbedded();

        embedded.Address1.Should().Be("100 Pine St");
        embedded.City.Should().Be("Boise");
        embedded.State.Should().Be("ID");
        embedded.PostalCode.Should().Be("83702");
    }

    // ── IntakeFormConfigEmbedded ↔ IntakeConfigDto ────────────────────────────

    [Fact]
    public void IntakeConfigToDto_WhenNull_ShouldThrowArgumentNullException()
    {
        IntakeFormConfigEmbedded? config = null;

        var act = () => config!.ToDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IntakeConfigToDto_ShouldMapAllFields()
    {
        var config = new IntakeFormConfigEmbedded
        {
            AcceptedFileTypes = [".jpg", ".png"],
            MaxFileSizeMb = 30,
            MaxAttachments = 8,
            AiContext = "Focus on slides",
            AllowAnonymousIntake = false
        };

        var dto = config.ToDto();

        dto.AcceptedFileTypes.Should().BeEquivalentTo([".jpg", ".png"]);
        dto.MaxFileSizeMb.Should().Be(30);
        dto.MaxAttachments.Should().Be(8);
        dto.AiContext.Should().Be("Focus on slides");
        dto.AllowAnonymousIntake.Should().BeFalse();
    }

    [Fact]
    public void IntakeConfigToEmbedded_WhenNull_ShouldThrowArgumentNullException()
    {
        IntakeConfigDto? dto = null;

        var act = () => dto!.ToEmbedded();

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LocationCreateRequestDto BuildValidCreateRequest() =>
        new()
        {
            Name = "Phoenix Service Center",
            Slug = "phoenix-service-center",
            Phone = "(602) 555-0200"
        };

    // ── Auto-slug generation ─────────────────────────────────────────────────

    [Fact]
    public void ToEntity_WhenSlugIsNull_ShouldAutoGenerateFromDealershipAndLocationName()
    {
        var dto = new LocationCreateRequestDto { Name = "Salt Lake City", Phone = null };

        var entity = dto.ToEntity("ten_1", "usr_1", "Camping World");

        entity.Slug.Should().Be("camping-world-salt-lake-city");
    }

    [Fact]
    public void ToEntity_WhenSlugIsWhitespace_ShouldAutoGenerateFromDealershipAndLocationName()
    {
        var dto = new LocationCreateRequestDto { Name = "Provo Service" } with { Slug = "   " };

        var entity = dto.ToEntity("ten_1", "usr_1", "Blue Compass RV");

        entity.Slug.Should().Be("blue-compass-rv-provo-service");
    }

    [Fact]
    public void ToEntity_WhenSlugProvidedExplicitly_ShouldUseProvidedSlug()
    {
        var dto = new LocationCreateRequestDto { Name = "My Location", Slug = "custom-slug" };

        var entity = dto.ToEntity("ten_1", "usr_1", "Some Dealer");

        entity.Slug.Should().Be("custom-slug");
    }

    // ── ApplyUpdate with null slug ───────────────────────────────────────────

    [Fact]
    public void ApplyUpdate_WhenSlugIsNull_ShouldPreserveExistingSlug()
    {
        var entity = new Location { TenantId = "ten_1", Name = "Old Name", Slug = "old-slug" };
        var dto = new LocationCreateRequestDto { Name = "New Name", Phone = "(555) 000-0000" };

        entity.ApplyUpdate(dto, "usr_updater");

        entity.Name.Should().Be("New Name");
        entity.Slug.Should().Be("old-slug");
    }
}
