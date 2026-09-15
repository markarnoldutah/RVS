using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class TenantIdGeneratorTests
{
    // ── FromName ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Nova RV Services", "org_nova_rv_services")]
    [InlineData("  Blue Compass RV  ", "org_blue_compass_rv")]
    [InlineData("Café & Sons, LLC", "org_cafe_sons_llc")]
    [InlineData("St. George RV-Repair", "org_st_george_rv_repair")]
    public void FromName_ShouldReturnOrgPrefixedSnakeCase(string name, string expected)
    {
        TenantIdGenerator.FromName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("***")]
    public void FromName_WhenNameHasNoAlphanumerics_ShouldReturnEmpty(string? name)
    {
        TenantIdGenerator.FromName(name).Should().BeEmpty();
    }

    [Fact]
    public void FromName_ShouldCapAtMaxLength()
    {
        var id = TenantIdGenerator.FromName(new string('a', 200));

        id.Length.Should().Be(TenantIdGenerator.MaxLength);
        id.Should().StartWith("org_");
        id.Should().NotEndWith("_");
    }

    [Fact]
    public void FromName_ShouldProduceAnIdTheValidatorAccepts()
    {
        var id = TenantIdGenerator.FromName("Nova RV Services");

        TenantProvisioningValidator.ValidateTenantId(id).IsValid.Should().BeTrue();
    }

    // ── Fixed child ids (Spec P-6) ───────────────────────────────────────────

    [Fact]
    public void DealershipIdFor_ShouldStripOrgPrefix()
    {
        TenantIdGenerator.DealershipIdFor("org_nova_rv").Should().Be("dlr_nova_rv");
    }

    [Fact]
    public void FirstLocationIdFor_ShouldStripOrgPrefix()
    {
        TenantIdGenerator.FirstLocationIdFor("org_nova_rv").Should().Be("loc_nova_rv_1");
    }

    [Fact]
    public void DealershipIdFor_WhenTenantIdHasNoOrgPrefix_ShouldUseItWhole()
    {
        TenantIdGenerator.DealershipIdFor("ten_legacy").Should().Be("dlr_ten_legacy");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void DealershipIdFor_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => TenantIdGenerator.DealershipIdFor(tenantId!);

        act.Should().Throw<ArgumentException>();
    }
}
