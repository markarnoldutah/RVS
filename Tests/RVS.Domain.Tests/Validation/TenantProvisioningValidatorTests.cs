using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class TenantProvisioningValidatorTests
{
    // ── ValidateTenantId ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("ten_nova_rv")]
    [InlineData("ten_a")]
    [InlineData("ten_shop_2")]
    public void ValidateTenantId_WhenWellFormed_ShouldPass(string tenantId)
    {
        TenantProvisioningValidator.ValidateTenantId(tenantId).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nova_rv")]
    [InlineData("ten_")]
    [InlineData("ten_Nova")]
    [InlineData("org_nova_rv")]
    [InlineData("ten_nova-rv")]
    [InlineData("ten__nova")]
    [InlineData("ten_nova_")]
    [InlineData("ten_nova rv")]
    public void ValidateTenantId_WhenMalformed_ShouldFail(string? tenantId)
    {
        TenantProvisioningValidator.ValidateTenantId(tenantId).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateTenantId_WhenLongerThanMax_ShouldFail()
    {
        var tenantId = "ten_" + new string('a', TenantIdGenerator.MaxLength);

        TenantProvisioningValidator.ValidateTenantId(tenantId).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateTenantId_WhenPlatformTenant_ShouldFailAsReserved()
    {
        var result = TenantProvisioningValidator.ValidateTenantId(TenantProvisioningValidator.PlatformTenantId);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("reserved");
    }

    // ── Roles ────────────────────────────────────────────────────────────────

    [Fact]
    public void ProvisionableRoles_ShouldBeTheFourActiveDealerRoles()
    {
        TenantProvisioningValidator.ProvisionableRoles.Should().BeEquivalentTo(
            ["dealer:owner", "dealer:manager", "dealer:advisor", "dealer:readonly"]);
    }

    [Theory]
    [InlineData("dealer:manager", true)]
    [InlineData("dealer:advisor", true)]
    [InlineData("dealer:readonly", true)]
    [InlineData("dealer:owner", false)]
    [InlineData(null, false)]
    public void IsLocationScopedRole_ShouldMatchRoleScope(string? role, bool expected)
    {
        TenantProvisioningValidator.IsLocationScopedRole(role).Should().Be(expected);
    }

    // ── Canonical status / plan ──────────────────────────────────────────────

    [Theory]
    [InlineData("pilot", "Pilot")]
    [InlineData("ACTIVE", "Active")]
    [InlineData(" Churned ", "Churned")]
    [InlineData("Trial", null)]
    [InlineData(null, null)]
    public void CanonicalStatus_ShouldNormaliseKnownValues(string? value, string? expected)
    {
        TenantProvisioningValidator.CanonicalStatus(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("Mobile", "mobile")]
    [InlineData("location", "location")]
    [InlineData("enterprise", null)]
    public void CanonicalPlan_ShouldNormaliseKnownValues(string? value, string? expected)
    {
        TenantProvisioningValidator.CanonicalPlan(value).Should().Be(expected);
    }

    // ── ValidateCreateTenant (Spec P-1) ──────────────────────────────────────

    [Fact]
    public void ValidateCreateTenant_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => TenantProvisioningValidator.ValidateCreateTenant(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateCreateTenant_WhenValid_ShouldPass()
    {
        TenantProvisioningValidator.ValidateCreateTenant(ValidCreate()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateCreateTenant_WhenTenantIdOmitted_ShouldPass()
    {
        TenantProvisioningValidator.ValidateCreateTenant(ValidCreate() with { TenantId = null }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateCreateTenant_WhenLocationScopedOwnerRole_ShouldPass()
    {
        // The first user can be location-scoped; it is scoped to the first location.
        TenantProvisioningValidator.ValidateCreateTenant(ValidCreate() with { OwnerRole = "dealer:manager" })
            .IsValid.Should().BeTrue();
    }

    public static TheoryData<string, TenantCreateRequestDto> InvalidCreates() => new()
    {
        { "blank name", ValidCreate() with { Name = " " } },
        { "name too long", ValidCreate() with { Name = new string('a', 101) } },
        { "name with markup", ValidCreate() with { Name = "<b>Nova</b>" } },
        { "bad tenant id", ValidCreate() with { TenantId = "Nova RV" } },
        { "platform tenant id", ValidCreate() with { TenantId = "ten_rvs_platform" } },
        { "bad billing email", ValidCreate() with { BillingEmail = "not-an-email" } },
        { "unknown plan", ValidCreate() with { Plan = "enterprise" } },
        { "blank plan", ValidCreate() with { Plan = "" } },
        { "notes too long", ValidCreate() with { Notes = new string('n', 2001) } },
        { "blank location name", ValidCreate() with { LocationName = "" } },
        { "bad location slug", ValidCreate() with { LocationSlug = "Has Spaces" } },
        { "phone too long", ValidCreate() with { LocationPhone = new string('1', 31) } },
        { "blank owner email", ValidCreate() with { OwnerEmail = "" } },
        { "bad owner email", ValidCreate() with { OwnerEmail = "jay at nova" } },
        { "blank owner name", ValidCreate() with { OwnerDisplayName = "" } },
        { "archived role", ValidCreate() with { OwnerRole = "dealer:technician" } },
        { "platform role", ValidCreate() with { OwnerRole = "platform:admin" } },
    };

    [Theory]
    [MemberData(nameof(InvalidCreates))]
    public void ValidateCreateTenant_WhenInvalid_ShouldFailWithMessage(string because, TenantCreateRequestDto request)
    {
        var result = TenantProvisioningValidator.ValidateCreateTenant(request);

        result.IsValid.Should().BeFalse(because);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    // ── ValidateUpdateTenant ─────────────────────────────────────────────────

    [Fact]
    public void ValidateUpdateTenant_WhenAllFieldsNull_ShouldPass()
    {
        TenantProvisioningValidator.ValidateUpdateTenant(new TenantUpdateRequestDto()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Trial", null, null)]
    [InlineData(null, "enterprise", null)]
    [InlineData(null, null, "not-an-email")]
    public void ValidateUpdateTenant_WhenFieldInvalid_ShouldFail(string? status, string? plan, string? billingEmail)
    {
        var request = new TenantUpdateRequestDto { Status = status, Plan = plan, BillingEmail = billingEmail };

        TenantProvisioningValidator.ValidateUpdateTenant(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateUpdateTenant_WhenBillingEmailBlank_ShouldPassAsAClear()
    {
        TenantProvisioningValidator.ValidateUpdateTenant(new TenantUpdateRequestDto { BillingEmail = "" })
            .IsValid.Should().BeTrue();
    }

    // ── ValidateAddUser (Spec P-2) ───────────────────────────────────────────

    [Fact]
    public void ValidateAddUser_WhenOwnerWithoutLocations_ShouldPass()
    {
        var request = new TenantUserCreateRequestDto
        {
            Email = "owner@nova.example.com",
            DisplayName = "Jay Lyons",
            Role = "dealer:owner",
        };

        TenantProvisioningValidator.ValidateAddUser(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAddUser_WhenLocationScopedRoleWithoutLocations_ShouldFail()
    {
        var request = new TenantUserCreateRequestDto
        {
            Email = "advisor@nova.example.com",
            DisplayName = "Sam Advisor",
            Role = "dealer:advisor",
            LocationIds = [],
        };

        var result = TenantProvisioningValidator.ValidateAddUser(request);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("location");
    }

    [Fact]
    public void ValidateAddUser_WhenLocationScopedRoleWithLocation_ShouldPass()
    {
        var request = new TenantUserCreateRequestDto
        {
            Email = "advisor@nova.example.com",
            DisplayName = "Sam Advisor",
            Role = "dealer:advisor",
            LocationIds = ["loc_nova_rv_1"],
        };

        TenantProvisioningValidator.ValidateAddUser(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Sam", "dealer:owner")]
    [InlineData("bad", "Sam", "dealer:owner")]
    [InlineData("sam@nova.example.com", "", "dealer:owner")]
    [InlineData("sam@nova.example.com", "Sam", "dealer:corporate-admin")]
    [InlineData("sam@nova.example.com", "Sam", "")]
    public void ValidateAddUser_WhenFieldInvalid_ShouldFail(string email, string displayName, string role)
    {
        var request = new TenantUserCreateRequestDto { Email = email, DisplayName = displayName, Role = role };

        TenantProvisioningValidator.ValidateAddUser(request).IsValid.Should().BeFalse();
    }

    // ── ValidateAccessGate (Spec P-4) ────────────────────────────────────────

    [Fact]
    public void ValidateAccessGate_WhenDisablingWithoutReason_ShouldFail()
    {
        TenantProvisioningValidator.ValidateAccessGate(new TenantAccessGateUpdateRequestDto { LoginsEnabled = false })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateAccessGate_WhenDisablingWithReason_ShouldPass()
    {
        TenantProvisioningValidator.ValidateAccessGate(
                new TenantAccessGateUpdateRequestDto { LoginsEnabled = false, Reason = "PastDue" })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAccessGate_WhenEnablingWithoutReason_ShouldPass()
    {
        TenantProvisioningValidator.ValidateAccessGate(new TenantAccessGateUpdateRequestDto { LoginsEnabled = true })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAccessGate_WhenReasonTooLong_ShouldFail()
    {
        TenantProvisioningValidator.ValidateAccessGate(
                new TenantAccessGateUpdateRequestDto { LoginsEnabled = false, Reason = new string('r', 201) })
            .IsValid.Should().BeFalse();
    }

    // ── ValidateAddLocation (Spec P-5) ───────────────────────────────────────

    [Fact]
    public void ValidateAddLocation_WhenValid_ShouldPass()
    {
        TenantProvisioningValidator.ValidateAddLocation(ValidLocation()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAddLocation_WhenNoRecipients_ShouldFail()
    {
        TenantProvisioningValidator.ValidateAddLocation(ValidLocation() with { Recipients = [] })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateAddLocation_WhenElevenRecipients_ShouldFail()
    {
        var recipients = Enumerable.Range(1, 11).Select(i => $"svc{i}@nova.example.com").ToList();

        TenantProvisioningValidator.ValidateAddLocation(ValidLocation() with { Recipients = recipients })
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", null, "svc@nova.example.com")]
    [InlineData("Hurricane", "Bad Slug", "svc@nova.example.com")]
    [InlineData("Hurricane", null, "not-an-email")]
    public void ValidateAddLocation_WhenFieldInvalid_ShouldFail(string name, string? slug, string recipient)
    {
        var request = ValidLocation() with { Name = name, Slug = slug, Recipients = [recipient] };

        TenantProvisioningValidator.ValidateAddLocation(request).IsValid.Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TenantCreateRequestDto ValidCreate() => new()
    {
        Name = "Nova RV Services",
        TenantId = "ten_nova_rv_services",
        BillingEmail = "billing@nova.example.com",
        Plan = "mobile",
        Notes = "Pilot P1",
        LocationName = "Hurricane",
        LocationSlug = null,
        LocationPhone = "(435) 555-0100",
        OwnerEmail = "jay@nova.example.com",
        OwnerDisplayName = "Jay Lyons",
        OwnerRole = "dealer:owner",
    };

    private static TenantLocationCreateRequestDto ValidLocation() => new()
    {
        Name = "St. George",
        Slug = "nova-st-george",
        Phone = "(435) 555-0101",
        Recipients = ["svc@nova.example.com"],
    };
}
