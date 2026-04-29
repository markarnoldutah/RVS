using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Tests.Mappers;

public class ConfigMapperTests
{
    // ── ToEntity ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToEntity_WhenDtoIsNull_ShouldThrowArgumentNullException()
    {
        TenantConfigCreateRequestDto? dto = null;

        var act = () => dto!.ToEntity("ten_1");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToEntity_WhenTenantIdIsEmpty_ShouldThrowArgumentException()
    {
        var dto = new TenantConfigCreateRequestDto();

        var act = () => dto.ToEntity("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToEntity_ShouldSetIdFromTenantId()
    {
        var dto = new TenantConfigCreateRequestDto();

        var entity = dto.ToEntity("ten_1");

        entity.Id.Should().Be("ten_1_config");
        entity.TenantId.Should().Be("ten_1");
    }

    [Fact]
    public void ToEntity_ShouldSeedDefaultCapabilities()
    {
        var dto = new TenantConfigCreateRequestDto();

        var entity = dto.ToEntity("ten_1");

        entity.AvailableCapabilities.Should().NotBeEmpty();
        entity.AvailableCapabilities.Should().Contain(c => c.Code == "diesel-service");
        entity.AvailableCapabilities.Should().Contain(c => c.Code == "warranty-service");
        entity.AvailableCapabilities.All(c => c.IsActive).Should().BeTrue();
    }

    // ── ToDto ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ToDto_WhenConfigIsNull_ShouldThrowArgumentNullException()
    {
        TenantConfig? config = null;

        var act = () => config!.ToDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToDto_ShouldMapCoreFields()
    {
        var config = BuildConfig();

        var dto = config.ToDto();

        dto.Id.Should().Be(config.Id);
        dto.TenantId.Should().Be(config.TenantId);
        dto.CreatedAtUtc.Should().Be(config.CreatedAtUtc);
    }

    [Fact]
    public void ToDto_ShouldMapAccessGate()
    {
        var config = BuildConfig();
        config.AccessGate = new TenantAccessGateEmbedded
        {
            LoginsEnabled = false,
            DisabledReason = "Maintenance",
            DisabledMessage = "We'll be back soon.",
            SupportContactEmail = "support@example.com"
        };

        var dto = config.ToDto();

        dto.AccessGate.LoginsEnabled.Should().BeFalse();
        dto.AccessGate.DisabledReason.Should().Be("Maintenance");
        dto.AccessGate.DisabledMessage.Should().Be("We'll be back soon.");
        dto.AccessGate.SupportContactEmail.Should().Be("support@example.com");
    }

    [Fact]
    public void ToDto_ShouldMapAvailableCapabilities()
    {
        var config = BuildConfig();
        config.AvailableCapabilities =
        [
            new() { Code = "diesel-service", Name = "Diesel Engine Service", SortOrder = 10, IsActive = true },
            new() { Code = "body-repair",    Name = "Body & Collision Repair", SortOrder = 20, IsActive = false },
        ];

        var dto = config.ToDto();

        dto.AvailableCapabilities.Should().HaveCount(2);
        dto.AvailableCapabilities[0].Code.Should().Be("diesel-service");
        dto.AvailableCapabilities[0].Name.Should().Be("Diesel Engine Service");
        dto.AvailableCapabilities[0].SortOrder.Should().Be(10);
        dto.AvailableCapabilities[0].IsActive.Should().BeTrue();
        dto.AvailableCapabilities[1].IsActive.Should().BeFalse();
    }

    [Fact]
    public void ToDto_WhenNoCapabilities_ShouldReturnEmptyList()
    {
        var config = BuildConfig();
        config.AvailableCapabilities = [];

        var dto = config.ToDto();

        dto.AvailableCapabilities.Should().BeEmpty();
    }

    // ── ApplyUpdateFromDto ────────────────────────────────────────────────────

    [Fact]
    public void ApplyUpdateFromDto_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        TenantConfig? entity = null;

        var act = () => entity!.ApplyUpdateFromDto(new TenantConfigUpdateRequestDto());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyUpdateFromDto_WhenDtoIsNull_ShouldThrowArgumentNullException()
    {
        var entity = BuildConfig();

        var act = () => entity.ApplyUpdateFromDto(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyUpdateFromDto_WhenCapabilitiesNull_ShouldLeaveExistingCapabilitiesUnchanged()
    {
        var entity = BuildConfig();
        entity.AvailableCapabilities = [new() { Code = "existing", Name = "Existing", SortOrder = 10 }];

        entity.ApplyUpdateFromDto(new TenantConfigUpdateRequestDto { AvailableCapabilities = null });

        entity.AvailableCapabilities.Should().HaveCount(1);
        entity.AvailableCapabilities[0].Code.Should().Be("existing");
    }

    [Fact]
    public void ApplyUpdateFromDto_WhenCapabilitiesProvided_ShouldReplaceList()
    {
        var entity = BuildConfig();
        entity.AvailableCapabilities = [new() { Code = "old", Name = "Old", SortOrder = 10 }];

        var newCapabilities = new List<TenantCapabilityDto>
        {
            new() { Code = "DIESEL-SERVICE", Name = "  Diesel Engine Service  ", SortOrder = 10, IsActive = true },
            new() { Code = "body-repair",    Name = "Body Repair",               SortOrder = 20, IsActive = false },
        };

        entity.ApplyUpdateFromDto(new TenantConfigUpdateRequestDto { AvailableCapabilities = newCapabilities });

        entity.AvailableCapabilities.Should().HaveCount(2);
        // Codes should be normalized to lowercase
        entity.AvailableCapabilities[0].Code.Should().Be("diesel-service");
        // Names should be trimmed
        entity.AvailableCapabilities[0].Name.Should().Be("Diesel Engine Service");
        entity.AvailableCapabilities[1].IsActive.Should().BeFalse();
    }

    // ── DefaultCapabilities ───────────────────────────────────────────────────

    [Fact]
    public void DefaultCapabilities_ShouldReturnNonEmptyList()
    {
        var defaults = ConfigMapper.DefaultCapabilities();

        defaults.Should().NotBeEmpty();
        defaults.Should().AllSatisfy(c =>
        {
            c.Code.Should().NotBeNullOrWhiteSpace();
            c.Name.Should().NotBeNullOrWhiteSpace();
            c.IsActive.Should().BeTrue();
            c.SortOrder.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void DefaultCapabilities_ShouldContainExpectedEntries()
    {
        var defaults = ConfigMapper.DefaultCapabilities();
        var codes = defaults.Select(c => c.Code).ToHashSet();

        codes.Should().Contain("diesel-service");
        codes.Should().Contain("warranty-service");
        codes.Should().Contain("winterization");
        codes.Should().Contain("safety-inspection");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static TenantConfig BuildConfig() => new()
    {
        Id = "ten_1_config",
        TenantId = "ten_1",
        AccessGate = new TenantAccessGateEmbedded { LoginsEnabled = true }
    };
}
