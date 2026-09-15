using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Provisioning;

namespace RVS.API.Tests.Mappers;

public class TenantProvisioningMapperTests
{
    private const string IntakeBaseUrl = "https://rvintake.com/";

    // ── ToEntity ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToEntity_WhenDtoIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => ((TenantCreateRequestDto)null!).ToEntity("org_nova", "usr_admin");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToEntity_ShouldCreatePilotTenantWithTrimmedFieldsAndCanonicalPlan()
    {
        var dto = new TenantCreateRequestDto
        {
            Name = "  Nova RV Services ",
            BillingEmail = " billing@nova.example.com ",
            Plan = "Mobile",
            Notes = " Pilot P1 ",
        };

        var tenant = dto.ToEntity("org_nova_rv_services", "usr_admin");

        tenant.Id.Should().Be("org_nova_rv_services");
        tenant.TenantId.Should().Be("org_nova_rv_services");
        tenant.Name.Should().Be("Nova RV Services");
        tenant.BillingEmail.Should().Be("billing@nova.example.com");
        tenant.Status.Should().Be("Pilot");
        tenant.Plan.Should().Be("mobile");
        tenant.Notes.Should().Be("Pilot P1");
        tenant.CreatedByUserId.Should().Be("usr_admin");
    }

    [Fact]
    public void ToEntity_WhenOptionalFieldsBlank_ShouldStoreNull()
    {
        var dto = new TenantCreateRequestDto { Name = "Nova", Plan = "location", BillingEmail = " ", Notes = "" };

        var tenant = dto.ToEntity("org_nova", "usr_admin");

        tenant.BillingEmail.Should().BeNull();
        tenant.Notes.Should().BeNull();
    }

    // ── ApplyUpdate ──────────────────────────────────────────────────────────

    [Fact]
    public void ApplyUpdate_ShouldOnlyChangeNonNullFields()
    {
        var tenant = BuildTenant();

        tenant.ApplyUpdate(new TenantUpdateRequestDto { Status = "active" }, "usr_admin");

        tenant.Status.Should().Be("Active");
        tenant.Plan.Should().Be("mobile");
        tenant.BillingEmail.Should().Be("billing@nova.example.com");
        tenant.Notes.Should().Be("Pilot P1");
        tenant.UpdatedByUserId.Should().Be("usr_admin");
        tenant.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void ApplyUpdate_WhenBillingEmailAndNotesBlank_ShouldClearThem()
    {
        var tenant = BuildTenant();

        tenant.ApplyUpdate(new TenantUpdateRequestDto { BillingEmail = "", Notes = " " }, "usr_admin");

        tenant.BillingEmail.Should().BeNull();
        tenant.Notes.Should().BeNull();
    }

    [Fact]
    public void ApplyUpdate_WhenDtoIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => BuildTenant().ApplyUpdate(null!, "usr_admin");

        act.Should().Throw<ArgumentNullException>();
    }

    // ── ToSummaryDto ─────────────────────────────────────────────────────────

    [Fact]
    public void ToSummaryDto_ShouldMapTenantGateAndLocationsWithIntakeUrls()
    {
        var disabledAt = DateTimeOffset.UtcNow;
        var overview = new TenantOverview(
            BuildTenant(),
            new TenantAccessGateEmbedded { LoginsEnabled = false, DisabledReason = "PastDue", DisabledAtUtc = disabledAt },
            [new Location { Id = "loc_nova_1", TenantId = "org_nova", Name = "Hurricane", Slug = "nova-hurricane" }]);

        var dto = overview.ToSummaryDto(IntakeBaseUrl);

        dto.TenantId.Should().Be("org_nova");
        dto.Name.Should().Be("Nova RV Services");
        dto.Status.Should().Be("Pilot");
        dto.Plan.Should().Be("mobile");
        dto.BillingEmail.Should().Be("billing@nova.example.com");
        dto.Notes.Should().Be("Pilot P1");
        dto.LoginsEnabled.Should().BeFalse();
        dto.DisabledReason.Should().Be("PastDue");
        dto.DisabledAtUtc.Should().Be(disabledAt);
        dto.Locations.Should().ContainSingle();
        dto.Locations[0].LocationId.Should().Be("loc_nova_1");
        dto.Locations[0].Name.Should().Be("Hurricane");
        dto.Locations[0].Slug.Should().Be("nova-hurricane");
        dto.Locations[0].IntakeUrl.Should().Be("https://rvintake.com/nova-hurricane");
    }

    // ── ToResponseDto (TenantProvisioningResult) ─────────────────────────────

    [Fact]
    public void ToResponseDto_WhenEveryStepSucceeded_ShouldCarryIntakeAndTicketUrls()
    {
        var expires = DateTime.UtcNow.AddDays(7);
        var result = new TenantProvisioningResult(
            "org_nova",
            [
                new ProvisioningStep(ProvisioningStepNames.Tenant, ProvisioningStepStatus.Created),
                new ProvisioningStep(ProvisioningStepNames.IdentityUser, ProvisioningStepStatus.AlreadyExisted),
            ],
            new Location { Id = "loc_nova_1", TenantId = "org_nova", Name = "Hurricane", Slug = "nova-hurricane" },
            "auth0|u1",
            new PasswordTicket("https://auth.example.com/ticket#abc", expires));

        var dto = result.ToResponseDto(IntakeBaseUrl);

        dto.TenantId.Should().Be("org_nova");
        dto.Succeeded.Should().BeTrue();
        dto.Steps.Select(s => (s.Step, s.Status)).Should().Equal(
            ("tenant", "created"),
            ("identity-user", "already existed"));
        dto.LocationId.Should().Be("loc_nova_1");
        dto.LocationSlug.Should().Be("nova-hurricane");
        dto.IntakeUrl.Should().Be("https://rvintake.com/nova-hurricane");
        dto.UserId.Should().Be("auth0|u1");
        dto.PasswordTicketUrl.Should().Be("https://auth.example.com/ticket#abc");
        dto.PasswordTicketExpiresAtUtc.Should().Be(expires);
    }

    [Fact]
    public void ToResponseDto_WhenAStepFailedBeforeLocation_ShouldReportFailureWithoutUrls()
    {
        var result = new TenantProvisioningResult(
            "org_nova",
            [
                new ProvisioningStep(ProvisioningStepNames.Tenant, ProvisioningStepStatus.Failed, "Cosmos unavailable"),
                new ProvisioningStep(ProvisioningStepNames.Location, ProvisioningStepStatus.Skipped),
            ],
            Location: null,
            UserId: null,
            PasswordTicket: null);

        var dto = result.ToResponseDto(IntakeBaseUrl);

        dto.Succeeded.Should().BeFalse();
        dto.Steps[0].Message.Should().Be("Cosmos unavailable");
        dto.IntakeUrl.Should().BeNull();
        dto.PasswordTicketUrl.Should().BeNull();
    }

    // ── User / ticket / location responses ───────────────────────────────────

    [Fact]
    public void ToResponseDto_ForUserResult_ShouldReportCreatedOrExisting()
    {
        var expires = DateTime.UtcNow.AddDays(7);
        var result = new TenantUserProvisioningResult(
            "org_nova", "auth0|u1", "sam@nova.example.com", "dealer:advisor", Created: false,
            new PasswordTicket("https://auth.example.com/ticket#xyz", expires));

        var dto = result.ToResponseDto();

        dto.TenantId.Should().Be("org_nova");
        dto.UserId.Should().Be("auth0|u1");
        dto.Email.Should().Be("sam@nova.example.com");
        dto.Role.Should().Be("dealer:advisor");
        dto.Status.Should().Be(ProvisioningStepStatus.AlreadyExisted);
        dto.PasswordTicketUrl.Should().Be("https://auth.example.com/ticket#xyz");
        dto.PasswordTicketExpiresAtUtc.Should().Be(expires);
    }

    [Fact]
    public void ToResponseDto_ForPasswordTicket_ShouldCarryUserId()
    {
        var expires = DateTime.UtcNow.AddDays(7);

        var dto = new PasswordTicket("https://auth.example.com/ticket#r", expires).ToResponseDto("auth0|u1");

        dto.UserId.Should().Be("auth0|u1");
        dto.PasswordTicketUrl.Should().Be("https://auth.example.com/ticket#r");
        dto.ExpiresAtUtc.Should().Be(expires);
    }

    [Fact]
    public void ToProvisioningResponseDto_ForLocation_ShouldBuildIntakeUrl()
    {
        var location = new Location { Id = "loc_2", TenantId = "org_nova", Name = "St. George", Slug = "nova-st-george" };

        var dto = location.ToProvisioningResponseDto("https://rvintake.com");

        dto.TenantId.Should().Be("org_nova");
        dto.LocationId.Should().Be("loc_2");
        dto.Name.Should().Be("St. George");
        dto.Slug.Should().Be("nova-st-george");
        dto.IntakeUrl.Should().Be("https://rvintake.com/nova-st-george");
    }

    private static Tenant BuildTenant() => new()
    {
        Id = "org_nova",
        Name = "Nova RV Services",
        BillingEmail = "billing@nova.example.com",
        Status = "Pilot",
        Plan = "mobile",
        Notes = "Pilot P1",
    };
}
