using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Tests.Mappers;

public class DealershipMapperApplyUpdateTests
{
    [Fact]
    public void ApplyUpdate_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        var dto = BuildUpdateRequest();

        var act = () => DealershipMapper.ApplyUpdate(null!, dto, "usr_1");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyUpdate_WhenDtoIsNull_ShouldThrowArgumentNullException()
    {
        var entity = BuildDealership();

        var act = () => entity.ApplyUpdate(null!, "usr_1");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyUpdate_ShouldApplyAllFields()
    {
        var entity = BuildDealership();

        var dto = new DealershipUpdateRequestDto
        {
            Name = "  Updated Dealership  ",
            Slug = "  UPDATED-SLUG  ",
            LogoUrl = "  https://cdn.example.com/logo.png  ",
            ServiceEmail = "  service@example.com  ",
            Phone = "  (555) 123-4567  ",
            IntakeConfig = new IntakeConfigDto
            {
                AcceptedFileTypes = [".jpg", ".png"],
                MaxFileSizeMb = 50,
                MaxAttachments = 5,
                AiContext = "Test context",
                AllowAnonymousIntake = false
            }
        };

        entity.ApplyUpdate(dto, "usr_updater");

        entity.Name.Should().Be("Updated Dealership");
        entity.Slug.Should().Be("updated-slug");
        entity.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
        entity.ServiceEmail.Should().Be("service@example.com");
        entity.Phone.Should().Be("(555) 123-4567");
        entity.IntakeConfig.MaxFileSizeMb.Should().Be(50);
        entity.IntakeConfig.MaxAttachments.Should().Be(5);
        entity.IntakeConfig.AllowAnonymousIntake.Should().BeFalse();
    }

    [Fact]
    public void ApplyUpdate_ShouldCallMarkAsUpdated()
    {
        var entity = BuildDealership();

        entity.ApplyUpdate(BuildUpdateRequest(), "usr_updater");

        entity.UpdatedByUserId.Should().Be("usr_updater");
        entity.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void ApplyUpdate_ShouldNormalizeSlugToLowercase()
    {
        var entity = BuildDealership();

        var dto = BuildUpdateRequest() with { Slug = "MY-DEALERSHIP" };

        entity.ApplyUpdate(dto, "usr_1");

        entity.Slug.Should().Be("my-dealership");
    }

    [Fact]
    public void ApplyUpdate_WhenNullableFieldsAreNull_ShouldSetToNull()
    {
        var entity = BuildDealership();
        entity.LogoUrl = "https://cdn.example.com/old-logo.png";
        entity.ServiceEmail = "old@example.com";
        entity.Phone = "(555) 000-0000";

        var dto = new DealershipUpdateRequestDto
        {
            Name = "Test",
            Slug = "test",
            LogoUrl = null,
            ServiceEmail = null,
            Phone = null
        };

        entity.ApplyUpdate(dto, "usr_1");

        entity.LogoUrl.Should().BeNull();
        entity.ServiceEmail.Should().BeNull();
        entity.Phone.Should().BeNull();
    }

    [Fact]
    public void ApplyUpdate_WhenIntakeConfigIsNull_ShouldKeepExistingConfig()
    {
        var entity = BuildDealership();
        var originalConfig = entity.IntakeConfig;

        var dto = new DealershipUpdateRequestDto
        {
            Name = "Test",
            Slug = "test",
            IntakeConfig = null
        };

        entity.ApplyUpdate(dto, "usr_1");

        entity.IntakeConfig.Should().BeSameAs(originalConfig);
    }

    private static Dealership BuildDealership() => new()
    {
        TenantId = "ten_1",
        Name = "Blue Compass RV",
        Slug = "blue-compass",
        Phone = "(801) 555-1000",
        IntakeConfig = new IntakeFormConfigEmbedded()
    };

    private static DealershipUpdateRequestDto BuildUpdateRequest() => new()
    {
        Name = "Blue Compass RV",
        Slug = "blue-compass",
        Phone = "(801) 555-1000"
    };
}
