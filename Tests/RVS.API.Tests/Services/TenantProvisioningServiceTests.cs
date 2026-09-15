using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Domain.Provisioning;

namespace RVS.API.Tests.Services;

/// <summary>
/// Spec P-1 … P-7 (issue #563): the orchestration behind the <c>api/admin/tenants</c> tool.
/// </summary>
public sealed class TenantProvisioningServiceTests
{
    private const string TenantId = "ten_nova_rv_services";
    private const string TenantName = "Nova RV Services";
    private const string DealershipId = "dlr_nova_rv_services";
    private const string FirstLocationId = "loc_nova_rv_services_1";
    private const string GeneratedSlug = "nova-rv-services-hurricane";
    private const string AdminUserId = "auth0|platform-admin";
    private const string OwnerEmail = "jay@nova.example.com";
    private const string NewUserId = "auth0|jay";
    private const string TicketUrl = "https://tenant.example.auth0.com/lo/reset?ticket=s3cret#";

    private readonly Mock<ITenantRepository> _tenantRepoMock = new();
    private readonly Mock<ITenantConfigService> _tenantConfigServiceMock = new();
    private readonly Mock<IDealershipService> _dealershipServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();
    private readonly Mock<ISlugLookupRepository> _slugRepoMock = new();
    private readonly Mock<IIdentityProvisioner> _identityMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly Mock<ILogger<TenantProvisioningService>> _loggerMock = new();
    private readonly TenantProvisioningService _sut;

    public TenantProvisioningServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns(AdminUserId);
        _sut = new TenantProvisioningService(
            _tenantRepoMock.Object,
            _tenantConfigServiceMock.Object,
            _dealershipServiceMock.Object,
            _locationServiceMock.Object,
            _slugRepoMock.Object,
            _identityMock.Object,
            _userContextMock.Object,
            _loggerMock.Object);
    }

    // ── CreateTenantAsync (Spec P-1) ─────────────────────────────────────────

    [Fact]
    public async Task CreateTenantAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.CreateTenantAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateTenantAsync_WhenRequestInvalid_ShouldThrowArgumentExceptionAndWriteNothing()
    {
        var act = () => _sut.CreateTenantAsync(ValidCreate() with { OwnerEmail = "not-an-email" });

        await act.Should().ThrowAsync<ArgumentException>();
        _tenantRepoMock.Verify(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenNameHasNoAlphanumericsAndNoTenantId_ShouldThrowArgumentException()
    {
        var act = () => _sut.CreateTenantAsync(ValidCreate() with { Name = "***", TenantId = null });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateTenantAsync_WhenTenantIdOmitted_ShouldDeriveItFromName()
    {
        SetupBrandNewTenant();

        var result = await _sut.CreateTenantAsync(ValidCreate() with { TenantId = null });

        result.TenantId.Should().Be(TenantId);
        _tenantRepoMock.Verify(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenNewTenant_ShouldCreateEveryStepInOrder()
    {
        SetupBrandNewTenant();

        var result = await _sut.CreateTenantAsync(ValidCreate());

        result.Succeeded.Should().BeTrue();
        result.Steps.Select(s => s.Name).Should().Equal(
            ProvisioningStepNames.Tenant,
            ProvisioningStepNames.TenantConfig,
            ProvisioningStepNames.Dealership,
            ProvisioningStepNames.Location,
            ProvisioningStepNames.SlugLookup,
            ProvisioningStepNames.IdentityUser);
        result.Steps.Should().OnlyContain(s => s.Status == ProvisioningStepStatus.Created);
        result.Location!.Slug.Should().Be(GeneratedSlug);
        result.UserId.Should().Be(NewUserId);
        result.PasswordTicket!.Url.Should().Be(TicketUrl);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenNewTenant_ShouldWriteCosmosDocumentsWithFixedIds()
    {
        SetupBrandNewTenant();

        await _sut.CreateTenantAsync(ValidCreate());

        _tenantRepoMock.Verify(r => r.CreateAsync(
            It.Is<Tenant>(t => t.Id == TenantId
                && t.Name == TenantName
                && t.Status == "Pilot"
                && t.Plan == "mobile"
                && t.BillingEmail == "billing@nova.example.com"
                && t.CreatedByUserId == AdminUserId),
            It.IsAny<CancellationToken>()), Times.Once);
        _tenantConfigServiceMock.Verify(s => s.CreateTenantConfigAsync(
            TenantId, It.IsAny<TenantConfigCreateRequestDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _dealershipServiceMock.Verify(s => s.CreateAsync(
            TenantId,
            It.Is<Dealership>(d => d.Id == DealershipId
                && d.TenantId == TenantId
                && d.Name == TenantName
                && d.Slug == "nova-rv-services"
                && d.CreatedByUserId == AdminUserId),
            It.IsAny<CancellationToken>()), Times.Once);
        _locationServiceMock.Verify(s => s.CreateAsync(
            TenantId,
            It.Is<Location>(l => l.Id == FirstLocationId
                && l.TenantId == TenantId
                && l.Name == "Nova RV Services - Hurricane"
                && l.Phone == "(435) 555-0100"
                && l.PacketConfig.Recipients.SequenceEqual(new[] { OwnerEmail })
                && l.CreatedByUserId == AdminUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenOwnerRole_ShouldProvisionTenantWideUser()
    {
        SetupBrandNewTenant();

        await _sut.CreateTenantAsync(ValidCreate());

        _identityMock.Verify(i => i.EnsureUserAsync(
            It.Is<IdentityUserRequest>(r => r.Email == OwnerEmail
                && r.DisplayName == "Jay Lyons"
                && r.TenantId == TenantId
                && r.OrgName == TenantName
                && r.Role == "dealer:owner"
                && r.LocationIds.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
        _identityMock.Verify(i => i.CreatePasswordTicketAsync(NewUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenOwnerRoleIsLocationScoped_ShouldScopeUserToFirstLocation()
    {
        SetupBrandNewTenant();

        await _sut.CreateTenantAsync(ValidCreate() with { OwnerRole = "dealer:manager" });

        _identityMock.Verify(i => i.EnsureUserAsync(
            It.Is<IdentityUserRequest>(r => r.Role == "dealer:manager"
                && r.LocationIds.SequenceEqual(new[] { FirstLocationId })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenTenantIdExistsWithDifferentName_ShouldThrowConflictAndWriteNothing()
    {
        _tenantRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = TenantId, Name = "Some Other Shop", Status = "Active", Plan = "location" });

        var act = () => _sut.CreateTenantAsync(ValidCreate());

        await act.Should().ThrowAsync<ConflictException>();
        _tenantRepoMock.Verify(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
        _identityMock.Verify(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenRetriedAfterSuccess_ShouldReportEveryStepAlreadyExistedWithoutDuplicates()
    {
        SetupFullyProvisionedTenant(existingUser: true);

        var result = await _sut.CreateTenantAsync(ValidCreate() with { Name = "nova rv services" });

        result.Succeeded.Should().BeTrue();
        result.Steps.Should().HaveCount(6);
        result.Steps.Should().OnlyContain(s => s.Status == ProvisioningStepStatus.AlreadyExisted);
        result.PasswordTicket!.Url.Should().Be(TicketUrl, "a retry issues a fresh set-password link");
        _tenantRepoMock.Verify(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
        _tenantConfigServiceMock.Verify(s => s.CreateTenantConfigAsync(
            It.IsAny<string>(), It.IsAny<TenantConfigCreateRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _dealershipServiceMock.Verify(s => s.CreateAsync(
            It.IsAny<string>(), It.IsAny<Dealership>(), It.IsAny<CancellationToken>()), Times.Never);
        _locationServiceMock.Verify(s => s.CreateAsync(
            It.IsAny<string>(), It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
        _slugRepoMock.Verify(r => r.CreateAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenLocationExistsButSlugLookupMissing_ShouldCreateSlugLookupWithNames()
    {
        SetupFullyProvisionedTenant(existingUser: true);
        _slugRepoMock.Setup(r => r.GetBySlugAsync(GeneratedSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);
        _slugRepoMock.Setup(r => r.CreateAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup s, CancellationToken _) => s);

        var result = await _sut.CreateTenantAsync(ValidCreate());

        result.Steps.Single(s => s.Name == ProvisioningStepNames.SlugLookup).Status
            .Should().Be(ProvisioningStepStatus.Created);
        _slugRepoMock.Verify(r => r.CreateAsync(
            It.Is<SlugLookup>(s => s.Id == $"slug_{GeneratedSlug}"
                && s.Slug == GeneratedSlug
                && s.TenantId == TenantId
                && s.LocationId == FirstLocationId
                && s.DealershipName == TenantName
                && s.LocationName == "Hurricane"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenSlugLookupPointsAtAnotherLocation_ShouldFailThatStep()
    {
        SetupFullyProvisionedTenant(existingUser: true);
        _slugRepoMock.Setup(r => r.GetBySlugAsync(GeneratedSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup { Slug = GeneratedSlug, TenantId = "ten_other", LocationId = "loc_other" });

        var result = await _sut.CreateTenantAsync(ValidCreate());

        result.Succeeded.Should().BeFalse();
        result.Steps.Single(s => s.Name == ProvisioningStepNames.SlugLookup).Status
            .Should().Be(ProvisioningStepStatus.Failed);
        result.Steps.Single(s => s.Name == ProvisioningStepNames.IdentityUser).Status
            .Should().Be(ProvisioningStepStatus.Skipped);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenIdentityStepFails_ShouldKeepCosmosStepsAndReportFailure()
    {
        SetupBrandNewTenant();
        _identityMock.Setup(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Auth0 provisioning is not configured."));

        var result = await _sut.CreateTenantAsync(ValidCreate());

        result.Succeeded.Should().BeFalse();
        result.Steps.Take(5).Should().OnlyContain(s => s.Status == ProvisioningStepStatus.Created);
        var identity = result.Steps.Last();
        identity.Name.Should().Be(ProvisioningStepNames.IdentityUser);
        identity.Status.Should().Be(ProvisioningStepStatus.Failed);
        identity.Message.Should().Contain("not configured");
        result.PasswordTicket.Should().BeNull();
    }

    [Fact]
    public async Task CreateTenantAsync_WhenIdentityEmailBelongsToAnotherTenant_ShouldFailIdentityStep()
    {
        SetupBrandNewTenant();
        _identityMock.Setup(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("That email already belongs to a user in another tenant."));

        var result = await _sut.CreateTenantAsync(ValidCreate());

        result.Steps.Last().Status.Should().Be(ProvisioningStepStatus.Failed);
        result.Steps.Last().Message.Should().Contain("another tenant");
    }

    [Fact]
    public async Task CreateTenantAsync_WhenAnEarlyStepFails_ShouldSkipTheRemainingSteps()
    {
        SetupBrandNewTenant();
        _dealershipServiceMock.Setup(s => s.CreateAsync(TenantId, It.IsAny<Dealership>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos is unavailable"));

        var result = await _sut.CreateTenantAsync(ValidCreate());

        result.Succeeded.Should().BeFalse();
        result.Steps.Select(s => s.Status).Should().Equal(
            ProvisioningStepStatus.Created,
            ProvisioningStepStatus.Created,
            ProvisioningStepStatus.Failed,
            ProvisioningStepStatus.Skipped,
            ProvisioningStepStatus.Skipped,
            ProvisioningStepStatus.Skipped);
        _locationServiceMock.Verify(s => s.CreateAsync(
            It.IsAny<string>(), It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
        _identityMock.Verify(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenCancelled_ShouldPropagateCancellation()
    {
        SetupBrandNewTenant();
        _tenantRepoMock.Setup(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.CreateTenantAsync(ValidCreate());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CreateTenantAsync_ShouldAuditLogWithoutTicketUrlOrEmail()
    {
        SetupBrandNewTenant();

        await _sut.CreateTenantAsync(ValidCreate());

        VerifyLoggedContaining(TenantId, AdminUserId);
        VerifyNeverLogged(TicketUrl);
        VerifyNeverLogged(OwnerEmail);
    }

    // ── AddUserAsync (Spec P-2) ──────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AddUserAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.AddUserAsync(tenantId!, ValidUser());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddUserAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.AddUserAsync(TenantId, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddUserAsync_WhenTenantNotFound_ShouldThrowKeyNotFoundException()
    {
        _tenantRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var act = () => _sut.AddUserAsync(TenantId, ValidUser());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddUserAsync_WhenRoleIsArchived_ShouldThrowArgumentException()
    {
        SetupExistingTenant();

        var act = () => _sut.AddUserAsync(TenantId, ValidUser() with { Role = "dealer:technician" });

        await act.Should().ThrowAsync<ArgumentException>();
        _identityMock.Verify(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddUserAsync_WhenLocationIdIsNotInTenant_ShouldThrowArgumentException()
    {
        SetupExistingTenant();

        var act = () => _sut.AddUserAsync(TenantId, ValidUser() with { LocationIds = ["loc_someone_else"] });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*loc_someone_else*");
        _identityMock.Verify(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddUserAsync_WhenLocationScopedRole_ShouldProvisionUserWithLocationsAndReturnTicket()
    {
        SetupExistingTenant();
        _identityMock.Setup(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUserResult("auth0|sam", Created: true));
        _identityMock.Setup(i => i.CreatePasswordTicketAsync("auth0|sam", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordTicket(TicketUrl, DateTime.UtcNow.AddDays(7)));

        var result = await _sut.AddUserAsync(TenantId, ValidUser());

        result.TenantId.Should().Be(TenantId);
        result.UserId.Should().Be("auth0|sam");
        result.Email.Should().Be("sam@nova.example.com");
        result.Role.Should().Be("dealer:advisor");
        result.Created.Should().BeTrue();
        result.PasswordTicket.Url.Should().Be(TicketUrl);
        _identityMock.Verify(i => i.EnsureUserAsync(
            It.Is<IdentityUserRequest>(r => r.Email == "sam@nova.example.com"
                && r.DisplayName == "Sam Advisor"
                && r.TenantId == TenantId
                && r.OrgName == TenantName
                && r.Role == "dealer:advisor"
                && r.LocationIds.SequenceEqual(new[] { FirstLocationId })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddUserAsync_WhenOwnerRole_ShouldIgnoreLocationIds()
    {
        SetupExistingTenant();
        _identityMock.Setup(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUserResult("auth0|sam", Created: false));
        _identityMock.Setup(i => i.CreatePasswordTicketAsync("auth0|sam", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordTicket(TicketUrl, DateTime.UtcNow.AddDays(7)));

        var result = await _sut.AddUserAsync(TenantId, ValidUser() with { Role = "dealer:owner", LocationIds = ["loc_ignored"] });

        result.Created.Should().BeFalse();
        _identityMock.Verify(i => i.EnsureUserAsync(
            It.Is<IdentityUserRequest>(r => r.Role == "dealer:owner" && r.LocationIds.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddUserAsync_ShouldAuditLogWithoutTicketUrlOrEmail()
    {
        SetupExistingTenant();
        _identityMock.Setup(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUserResult("auth0|sam", Created: true));
        _identityMock.Setup(i => i.CreatePasswordTicketAsync("auth0|sam", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordTicket(TicketUrl, DateTime.UtcNow.AddDays(7)));

        await _sut.AddUserAsync(TenantId, ValidUser());

        VerifyLoggedContaining(TenantId, AdminUserId);
        VerifyNeverLogged(TicketUrl);
        VerifyNeverLogged("sam@nova.example.com");
    }

    // ── CreatePasswordTicketAsync (Spec P-3) ─────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreatePasswordTicketAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.CreatePasswordTicketAsync(tenantId!, "auth0|sam");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreatePasswordTicketAsync_WhenUserIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? userId)
    {
        var act = () => _sut.CreatePasswordTicketAsync(TenantId, userId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreatePasswordTicketAsync_WhenUserNotFound_ShouldThrowKeyNotFoundException()
    {
        SetupExistingTenant();
        _identityMock.Setup(i => i.GetUserAsync("auth0|missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityUser?)null);

        var act = () => _sut.CreatePasswordTicketAsync(TenantId, "auth0|missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreatePasswordTicketAsync_WhenUserBelongsToAnotherTenant_ShouldThrowKeyNotFoundAndIssueNothing()
    {
        SetupExistingTenant();
        _identityMock.Setup(i => i.GetUserAsync("auth0|other", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUser("auth0|other", "x@other.example.com", "ten_other"));

        var act = () => _sut.CreatePasswordTicketAsync(TenantId, "auth0|other");

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _identityMock.Verify(i => i.CreatePasswordTicketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePasswordTicketAsync_WhenUserInTenant_ShouldReturnNewTicket()
    {
        SetupExistingTenant();
        var expires = DateTime.UtcNow.AddDays(7);
        _identityMock.Setup(i => i.GetUserAsync("auth0|sam", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUser("auth0|sam", "sam@nova.example.com", TenantId));
        _identityMock.Setup(i => i.CreatePasswordTicketAsync("auth0|sam", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordTicket(TicketUrl, expires));

        var ticket = await _sut.CreatePasswordTicketAsync(TenantId, "auth0|sam");

        ticket.Url.Should().Be(TicketUrl);
        ticket.ExpiresAtUtc.Should().Be(expires);
        VerifyNeverLogged(TicketUrl);
    }

    // ── SetAccessGateAsync (Spec P-4) ────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SetAccessGateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.SetAccessGateAsync(tenantId!, new TenantAccessGateUpdateRequestDto { LoginsEnabled = true });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAccessGateAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.SetAccessGateAsync(TenantId, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetAccessGateAsync_WhenTenantNotFound_ShouldThrowKeyNotFoundException()
    {
        _tenantRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var act = () => _sut.SetAccessGateAsync(TenantId, new TenantAccessGateUpdateRequestDto { LoginsEnabled = true });

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SetAccessGateAsync_WhenDisablingWithoutReason_ShouldThrowArgumentException()
    {
        SetupExistingTenant();

        var act = () => _sut.SetAccessGateAsync(TenantId, new TenantAccessGateUpdateRequestDto { LoginsEnabled = false });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAccessGateAsync_WhenDisabling_ShouldSetGateAndLeaveCommercialStatusAlone()
    {
        var tenant = SetupExistingTenant();
        var gate = new TenantAccessGateEmbedded { LoginsEnabled = false, DisabledReason = "PastDue", DisabledAtUtc = DateTimeOffset.UtcNow };
        _tenantConfigServiceMock.Setup(s => s.SetAccessGateAsync(TenantId, false, "PastDue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantConfig { Id = $"{TenantId}_config", TenantId = TenantId, AccessGate = gate });

        var overview = await _sut.SetAccessGateAsync(
            TenantId, new TenantAccessGateUpdateRequestDto { LoginsEnabled = false, Reason = " PastDue " });

        overview.AccessGate.LoginsEnabled.Should().BeFalse();
        overview.Tenant.Status.Should().Be("Pilot");
        _tenantRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLoggedContaining(TenantId, AdminUserId);
    }

    // ── UpdateTenantAsync ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateTenantAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.UpdateTenantAsync(tenantId!, new TenantUpdateRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateTenantAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.UpdateTenantAsync(TenantId, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateTenantAsync_WhenTenantNotFound_ShouldThrowKeyNotFoundException()
    {
        _tenantRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var act = () => _sut.UpdateTenantAsync(TenantId, new TenantUpdateRequestDto { Status = "Active" });

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateTenantAsync_WhenStatusUnknown_ShouldThrowArgumentException()
    {
        SetupExistingTenant();

        var act = () => _sut.UpdateTenantAsync(TenantId, new TenantUpdateRequestDto { Status = "Trial" });

        await act.Should().ThrowAsync<ArgumentException>();
        _tenantRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTenantAsync_ShouldApplyChangesWithoutTouchingTheAccessGate()
    {
        SetupExistingTenant();
        _tenantRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        var overview = await _sut.UpdateTenantAsync(TenantId, new TenantUpdateRequestDto { Status = "churned" });

        overview.Tenant.Status.Should().Be("Churned");
        overview.Tenant.UpdatedByUserId.Should().Be(AdminUserId);
        overview.AccessGate.LoginsEnabled.Should().BeTrue("status is commercial and never sets the gate");
        _tenantConfigServiceMock.Verify(s => s.SetAccessGateAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ListTenantsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListTenantsAsync_ShouldReturnEachTenantWithGateAndLocationsOrderedByName()
    {
        var zeta = new Tenant { Id = "ten_zeta", Name = "Zeta RV", Status = "Active", Plan = "location" };
        var alpha = new Tenant { Id = "ten_alpha", Name = "Alpha RV", Status = "Pilot", Plan = "mobile" };
        _tenantRepoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([zeta, alpha]);
        _tenantConfigServiceMock.Setup(s => s.GetAccessGateAsync("ten_zeta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAccessGateEmbedded { LoginsEnabled = false });
        _tenantConfigServiceMock.Setup(s => s.GetAccessGateAsync("ten_alpha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAccessGateEmbedded { LoginsEnabled = true });
        _locationServiceMock.Setup(s => s.ListByTenantAsync("ten_zeta", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _locationServiceMock.Setup(s => s.ListByTenantAsync("ten_alpha", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Location { Id = "loc_a", TenantId = "ten_alpha", Name = "Main", Slug = "alpha-main" }]);

        var result = await _sut.ListTenantsAsync();

        result.Select(o => o.Tenant.Id).Should().Equal("ten_alpha", "ten_zeta");
        result[0].AccessGate.LoginsEnabled.Should().BeTrue();
        result[0].Locations.Should().ContainSingle();
        result[1].AccessGate.LoginsEnabled.Should().BeFalse();
    }

    // ── AddLocationAsync (Spec P-5) ──────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AddLocationAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.AddLocationAsync(tenantId!, ValidLocation());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddLocationAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.AddLocationAsync(TenantId, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddLocationAsync_WhenTenantNotFound_ShouldThrowKeyNotFoundException()
    {
        _tenantRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var act = () => _sut.AddLocationAsync(TenantId, ValidLocation());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddLocationAsync_WhenNoRecipients_ShouldThrowArgumentException()
    {
        SetupExistingTenant();

        var act = () => _sut.AddLocationAsync(TenantId, ValidLocation() with { Recipients = [] });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddLocationAsync_ShouldCreateThroughLocationService()
    {
        SetupExistingTenant();
        _locationServiceMock.Setup(s => s.CreateAsync(TenantId, It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Location l, CancellationToken _) => l);

        var location = await _sut.AddLocationAsync(TenantId, ValidLocation());

        location.TenantId.Should().Be(TenantId);
        location.Name.Should().Be("Nova RV Services - St. George");
        location.Slug.Should().Be("nova-st-george");
        location.Phone.Should().Be("(435) 555-0101");
        location.PacketConfig.Recipients.Should().Equal("svc@nova.example.com", "jay@nova.example.com");
        location.CreatedByUserId.Should().Be(AdminUserId);
        VerifyLoggedContaining(TenantId, AdminUserId);
    }

    [Fact]
    public async Task AddLocationAsync_WhenNameAlreadyStartsWithBusinessName_ShouldNotPrefixItTwice()
    {
        SetupExistingTenant();
        _locationServiceMock.Setup(s => s.CreateAsync(TenantId, It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Location l, CancellationToken _) => l);

        var location = await _sut.AddLocationAsync(TenantId, ValidLocation() with { Name = "nova rv services - St. George" });

        location.Name.Should().Be("Nova RV Services - St. George");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TenantCreateRequestDto ValidCreate() => new()
    {
        Name = TenantName,
        TenantId = TenantId,
        BillingEmail = "billing@nova.example.com",
        Plan = "mobile",
        Notes = "Pilot P1",
        LocationName = "Hurricane",
        LocationPhone = "(435) 555-0100",
        OwnerEmail = OwnerEmail,
        OwnerDisplayName = "Jay Lyons",
        OwnerRole = "dealer:owner",
    };

    private static TenantUserCreateRequestDto ValidUser() => new()
    {
        Email = "sam@nova.example.com",
        DisplayName = "Sam Advisor",
        Role = "dealer:advisor",
        LocationIds = [FirstLocationId],
    };

    private static TenantLocationCreateRequestDto ValidLocation() => new()
    {
        Name = "St. George",
        Slug = "nova-st-george",
        Phone = "(435) 555-0101",
        Recipients = ["svc@nova.example.com", "jay@nova.example.com"],
    };

    private static Tenant BuildTenant() => new()
    {
        Id = TenantId,
        Name = TenantName,
        Status = "Pilot",
        Plan = "mobile",
    };

    private static Location BuildFirstLocation() => new()
    {
        Id = FirstLocationId,
        TenantId = TenantId,
        Name = "Hurricane",
        Slug = GeneratedSlug,
    };

    /// <summary>A tenant that exists with one location; used by the post-P-1 operations.</summary>
    private Tenant SetupExistingTenant()
    {
        var tenant = BuildTenant();
        _tenantRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _tenantConfigServiceMock.Setup(s => s.GetAccessGateAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAccessGateEmbedded { LoginsEnabled = true });
        _locationServiceMock.Setup(s => s.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildFirstLocation()]);
        return tenant;
    }

    /// <summary>Nothing exists yet: every P-1 step has something to create.</summary>
    private void SetupBrandNewTenant()
    {
        _tenantRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);
        _tenantRepoMock.Setup(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        _tenantConfigServiceMock.Setup(s => s.GetTenantConfigAsync(TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        _tenantConfigServiceMock.Setup(s => s.CreateTenantConfigAsync(
                TenantId, It.IsAny<TenantConfigCreateRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantConfig { Id = $"{TenantId}_config", TenantId = TenantId });

        _dealershipServiceMock.Setup(s => s.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _dealershipServiceMock.Setup(s => s.CreateAsync(TenantId, It.IsAny<Dealership>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Dealership d, CancellationToken _) => d);

        _locationServiceMock.Setup(s => s.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _locationServiceMock.Setup(s => s.CreateAsync(TenantId, It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Location l, CancellationToken _) =>
            {
                // LocationService generates {dealership-slug}-{location-name} when none is supplied.
                if (string.IsNullOrEmpty(l.Slug))
                {
                    l.Slug = GeneratedSlug;
                }
                return l;
            });

        _identityMock.Setup(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUserResult(NewUserId, Created: true));
        _identityMock.Setup(i => i.CreatePasswordTicketAsync(NewUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordTicket(TicketUrl, DateTime.UtcNow.AddDays(7)));
    }

    /// <summary>Everything from a previous successful run is already in place.</summary>
    private void SetupFullyProvisionedTenant(bool existingUser)
    {
        _tenantRepoMock.Setup(r => r.GetAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildTenant());
        _tenantConfigServiceMock.Setup(s => s.GetTenantConfigAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantConfig { Id = $"{TenantId}_config", TenantId = TenantId });
        _dealershipServiceMock.Setup(s => s.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Dealership { Id = DealershipId, TenantId = TenantId, Name = TenantName, Slug = "nova-rv-services" }]);
        _locationServiceMock.Setup(s => s.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildFirstLocation()]);
        _slugRepoMock.Setup(r => r.GetBySlugAsync(GeneratedSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup { Id = $"slug_{GeneratedSlug}", Slug = GeneratedSlug, TenantId = TenantId, LocationId = FirstLocationId });
        _identityMock.Setup(i => i.EnsureUserAsync(It.IsAny<IdentityUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUserResult(NewUserId, Created: !existingUser));
        _identityMock.Setup(i => i.CreatePasswordTicketAsync(NewUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordTicket(TicketUrl, DateTime.UtcNow.AddDays(7)));
    }

    private void VerifyLoggedContaining(params string[] fragments) =>
        _loggerMock.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => fragments.All(f => v.ToString()!.Contains(f))),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);

    private void VerifyNeverLogged(string fragment) =>
        _loggerMock.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(fragment)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
}
