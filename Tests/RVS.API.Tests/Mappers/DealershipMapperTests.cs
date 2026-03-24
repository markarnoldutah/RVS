using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.Entities;

namespace RVS.API.Tests.Mappers;

public class DealershipMapperTests
{
    // ── ToDetailDto ──────────────────────────────────────────────────────────

    [Fact]
    public void ToDetailDto_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        Dealership? entity = null;

        var act = () => entity!.ToDetailDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToDetailDto_ShouldMapAllFields()
    {
        var now = DateTime.UtcNow;
        var entity = new Dealership
        {
            TenantId = "ten_1",
            Name = "Blue Compass RV",
            Slug = "blue-compass-rv",
            LogoUrl = "https://cdn.example.com/logo.png",
            ServiceEmail = "service@bluecompass.com",
            Phone = "(801) 555-1000",
            IntakeConfig = new IntakeFormConfigEmbedded
            {
                MaxFileSizeMb = 25,
                MaxAttachments = 10,
                AllowAnonymousIntake = true
            },
            CreatedAtUtc = now
        };

        var dto = entity.ToDetailDto();

        dto.Id.Should().Be(entity.Id);
        dto.TenantId.Should().Be("ten_1");
        dto.Name.Should().Be("Blue Compass RV");
        dto.Slug.Should().Be("blue-compass-rv");
        dto.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
        dto.ServiceEmail.Should().Be("service@bluecompass.com");
        dto.Phone.Should().Be("(801) 555-1000");
        dto.IntakeConfig.Should().NotBeNull();
        dto.IntakeConfig!.MaxFileSizeMb.Should().Be(25);
        dto.IntakeConfig.AllowAnonymousIntake.Should().BeTrue();
        dto.CreatedAtUtc.Should().Be(now);
        dto.UpdatedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_WhenOptionalFieldsAreNull_ShouldMapAsNull()
    {
        var entity = new Dealership
        {
            TenantId = "ten_1",
            Name = "Happy Trails RV",
            Slug = "happy-trails-rv"
        };

        var dto = entity.ToDetailDto();

        dto.LogoUrl.Should().BeNull();
        dto.ServiceEmail.Should().BeNull();
        dto.Phone.Should().BeNull();
    }

    // ── ToSummaryDto ─────────────────────────────────────────────────────────

    [Fact]
    public void ToSummaryDto_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        Dealership? entity = null;

        var act = () => entity!.ToSummaryDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToSummaryDto_ShouldMapCoreFields()
    {
        var entity = new Dealership
        {
            TenantId = "ten_1",
            Name = "Happy Trails RV",
            Slug = "happy-trails-rv",
            Phone = "(303) 555-2000"
        };

        var dto = entity.ToSummaryDto();

        dto.Id.Should().Be(entity.Id);
        dto.Name.Should().Be("Happy Trails RV");
        dto.Slug.Should().Be("happy-trails-rv");
        dto.Phone.Should().Be("(303) 555-2000");
    }

    [Fact]
    public void ToSummaryDto_WhenPhoneIsNull_ShouldMapAsNull()
    {
        var entity = new Dealership
        {
            TenantId = "ten_1",
            Name = "Happy Trails RV",
            Slug = "happy-trails-rv"
        };

        var dto = entity.ToSummaryDto();

        dto.Phone.Should().BeNull();
    }
}
