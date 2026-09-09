using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class LocationServiceTests
{
    private readonly Mock<ILocationRepository> _locationRepoMock = new();
    private readonly Mock<ISlugLookupRepository> _slugRepoMock = new();
    private readonly Mock<IDealershipRepository> _dealershipRepoMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly Mock<ILogger<LocationService>> _loggerMock = new();
    private readonly LocationService _sut;

    public LocationServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _sut = new LocationService(
            _locationRepoMock.Object,
            _slugRepoMock.Object,
            _dealershipRepoMock.Object,
            _userContextMock.Object,
            _notificationMock.Object,
            _loggerMock.Object);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetByIdAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetByIdAsync(tenantId!, "loc_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetByIdAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.GetByIdAsync("ten_1", id!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", "loc_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var act = () => _sut.GetByIdAsync("ten_1", "loc_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnLocation()
    {
        var location = BuildLocation();
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var result = await _sut.GetByIdAsync("ten_1", location.Id);

        result.Should().BeSameAs(location);
    }

    // ── ListByTenantAsync ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ListByTenantAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.ListByTenantAsync(tenantId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ListByTenantAsync_ShouldReturnLocationsFromRepository()
    {
        var locations = new List<Location> { BuildLocation(), BuildLocation() };
        _locationRepoMock.Setup(r => r.ListByTenantAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);

        var result = await _sut.ListByTenantAsync("ten_1");

        result.Should().HaveCount(2);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.CreateAsync(tenantId!, BuildLocation());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.CreateAsync("ten_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateSlugLookupThenLocation()
    {
        var location = BuildLocation();
        _slugRepoMock.Setup(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup());
        _locationRepoMock.Setup(r => r.CreateAsync(location, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var result = await _sut.CreateAsync("ten_1", location);

        result.Should().BeSameAs(location);
        _slugRepoMock.Verify(r => r.UpsertAsync(
            It.Is<SlugLookup>(s => s.Slug == location.Slug && s.LocationId == location.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _locationRepoMock.Verify(r => r.CreateAsync(location, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenLocationCreateFails_ShouldRollbackSlugAndRethrow()
    {
        var location = BuildLocation();
        _slugRepoMock.Setup(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup());
        _locationRepoMock.Setup(r => r.CreateAsync(location, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos conflict"));

        var act = () => _sut.CreateAsync("ten_1", location);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Cosmos conflict");
        _slugRepoMock.Verify(r => r.DeleteAsync(location.Slug, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenSlugProvided_ShouldUseItVerbatim()
    {
        var location = BuildLocation();
        location.Slug = "preset-slug";

        _slugRepoMock.Setup(r => r.GetBySlugAsync("preset-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);
        _slugRepoMock.Setup(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup());
        _locationRepoMock.Setup(r => r.CreateAsync(location, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        await _sut.CreateAsync("ten_1", location);

        location.Slug.Should().Be("preset-slug");
        _slugRepoMock.Verify(r => r.UpsertAsync(
            It.Is<SlugLookup>(s => s.Slug == "preset-slug"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenSlugProvidedButTaken_ShouldThrowArgumentException()
    {
        var location = BuildLocation();
        location.Slug = "taken-slug";

        _slugRepoMock.Setup(r => r.GetBySlugAsync("taken-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup { Slug = "taken-slug" });

        var act = () => _sut.CreateAsync("ten_1", location);

        await act.Should().ThrowAsync<ArgumentException>();
        _locationRepoMock.Verify(r => r.CreateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenSlugMissing_ShouldGenerateSlugFromOrgAndLocationName()
    {
        var location = BuildLocation();
        location.Slug = string.Empty; // request server-side generation

        _dealershipRepoMock.Setup(r => r.ListByTenantAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Dealership { TenantId = "ten_1", Slug = "camping-world" }]);
        _slugRepoMock.Setup(r => r.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);
        _slugRepoMock.Setup(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup());
        _locationRepoMock.Setup(r => r.CreateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        await _sut.CreateAsync("ten_1", location);

        location.Slug.Should().Be("camping-world-salt-lake-service-center");
    }

    [Fact]
    public async Task CreateAsync_WhenGeneratedSlugCollides_ShouldAppendNumericSuffix()
    {
        var location = BuildLocation();
        location.Slug = string.Empty;

        _dealershipRepoMock.Setup(r => r.ListByTenantAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Dealership { TenantId = "ten_1", Slug = "camping-world" }]);
        _slugRepoMock.Setup(r => r.GetBySlugAsync("camping-world-salt-lake-service-center", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup());
        _slugRepoMock.Setup(r => r.GetBySlugAsync("camping-world-salt-lake-service-center-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);
        _slugRepoMock.Setup(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup());
        _locationRepoMock.Setup(r => r.CreateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        await _sut.CreateAsync("ten_1", location);

        location.Slug.Should().Be("camping-world-salt-lake-service-center-2");
    }

    [Fact]
    public async Task CreateAsync_WhenNoDealership_ShouldFallBackToLocationNameSlug()
    {
        var location = BuildLocation();
        location.Slug = string.Empty;

        _dealershipRepoMock.Setup(r => r.ListByTenantAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _slugRepoMock.Setup(r => r.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);
        _slugRepoMock.Setup(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup());
        _locationRepoMock.Setup(r => r.CreateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        await _sut.CreateAsync("ten_1", location);

        location.Slug.Should().Be("salt-lake-service-center");
    }

    [Fact]
    public async Task CreateAsync_WhenPacketConfigExceedsTenRecipients_ShouldThrowArgumentExceptionAndNotPersist()
    {
        var location = BuildLocation();
        location.PacketConfig = new PacketConfigEmbedded
        {
            Recipients = [.. Enumerable.Range(1, 11).Select(i => $"advisor{i}@dealer.com")],
        };

        var act = () => _sut.CreateAsync("ten_1", location);

        await act.Should().ThrowAsync<ArgumentException>();
        _slugRepoMock.Verify(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()), Times.Never);
        _locationRepoMock.Verify(r => r.CreateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenPacketConfigHasTenRecipients_ShouldSucceed()
    {
        var location = BuildLocation();
        location.Slug = "preset-slug";
        location.PacketConfig = new PacketConfigEmbedded
        {
            Recipients = [.. Enumerable.Range(1, 10).Select(i => $"advisor{i}@dealer.com")],
        };

        _slugRepoMock.Setup(r => r.GetBySlugAsync("preset-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlugLookup?)null);
        _slugRepoMock.Setup(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup());
        _locationRepoMock.Setup(r => r.CreateAsync(location, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var result = await _sut.CreateAsync("ten_1", location);

        result.Should().BeSameAs(location);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.UpdateAsync(tenantId!, "loc_1", BuildLocation());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.UpdateAsync("ten_1", id!, BuildLocation());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.UpdateAsync("ten_1", "loc_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", "loc_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var act = () => _sut.UpdateAsync("ten_1", "loc_missing", BuildLocation());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenSlugUnchanged_ShouldNotTouchSlugLookup()
    {
        var existing = BuildLocation();
        var updated = BuildLocation();
        updated.Name = "Updated Name";

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        await _sut.UpdateAsync("ten_1", existing.Id, updated);

        _slugRepoMock.Verify(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()), Times.Never);
        _slugRepoMock.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenSlugChanged_ShouldCreateNewSlugAndDeleteOld()
    {
        var existing = BuildLocation();
        var oldSlug = existing.Slug;
        var updated = new Location
        {
            TenantId = "ten_1",
            Name = "Renamed Location",
            Slug = "new-slug",
            Address = new AddressEmbedded(),
            IntakeConfig = new IntakeFormConfigEmbedded()
        };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _slugRepoMock.Setup(r => r.UpsertAsync(It.IsAny<SlugLookup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugLookup());
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        var result = await _sut.UpdateAsync("ten_1", existing.Id, updated);

        _slugRepoMock.Verify(r => r.UpsertAsync(
            It.Is<SlugLookup>(s => s.Slug == "new-slug"),
            It.IsAny<CancellationToken>()), Times.Once);
        _slugRepoMock.Verify(r => r.DeleteAsync(oldSlug, It.IsAny<CancellationToken>()), Times.Once);
        result.Name.Should().Be("Renamed Location");
        result.UpdatedByUserId.Should().Be("usr_test");
    }

    [Fact]
    public async Task UpdateAsync_WhenPacketConfigExceedsTenRecipients_ShouldThrowArgumentException()
    {
        var updated = BuildLocation();
        updated.PacketConfig = new PacketConfigEmbedded
        {
            Recipients = [.. Enumerable.Range(1, 11).Select(i => $"advisor{i}@dealer.com")],
        };

        var act = () => _sut.UpdateAsync("ten_1", updated.Id, updated);

        await act.Should().ThrowAsync<ArgumentException>();
        _locationRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistPacketConfigOntoExistingLocation()
    {
        var existing = BuildLocation();
        var updated = BuildLocation();
        updated.PacketConfig = new PacketConfigEmbedded
        {
            Recipients = ["svc@dealer.com"],
            AttachPdf = false,
            StatusLinkTtlDays = 7,
        };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        var result = await _sut.UpdateAsync("ten_1", existing.Id, updated);

        result.PacketConfig.Recipients.Should().ContainSingle().Which.Should().Be("svc@dealer.com");
        result.PacketConfig.AttachPdf.Should().BeFalse();
        result.PacketConfig.StatusLinkTtlDays.Should().Be(7);
        _locationRepoMock.Verify(r => r.UpdateAsync(
            It.Is<Location>(l => l.PacketConfig.Recipients.Contains("svc@dealer.com")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldCarryDisabledRecipientsAcrossASettingsSave()
    {
        var existing = BuildLocation();
        existing.PacketConfig = new PacketConfigEmbedded
        {
            Recipients = ["live@dealer.com"],
            DisabledRecipients =
            [
                new DisabledRecipientEmbedded { Email = "dead@dealer.com", Reason = "Bounced", DisabledAtUtc = DateTime.UtcNow },
            ],
        };

        var updated = BuildLocation();
        updated.PacketConfig = new PacketConfigEmbedded { Recipients = ["live@dealer.com", "second@dealer.com"] };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        var result = await _sut.UpdateAsync("ten_1", existing.Id, updated);

        result.PacketConfig.DisabledRecipients.Should().ContainSingle().Which.Email.Should().Be("dead@dealer.com");
        result.PacketConfig.Recipients.Should().BeEquivalentTo(["live@dealer.com", "second@dealer.com"]);
    }

    [Fact]
    public async Task UpdateAsync_WhenCallerAddsADisabledAddressBackToRecipients_ShouldTreatItAsAReEnable()
    {
        var existing = BuildLocation();
        existing.PacketConfig = new PacketConfigEmbedded
        {
            Recipients = ["live@dealer.com"],
            DisabledRecipients = [new DisabledRecipientEmbedded { Email = "dead@dealer.com" }],
        };

        var updated = BuildLocation();
        updated.PacketConfig = new PacketConfigEmbedded { Recipients = ["live@dealer.com", "dead@dealer.com"] };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        var result = await _sut.UpdateAsync("ten_1", existing.Id, updated);

        result.PacketConfig.DisabledRecipients.Should().BeEmpty();
        result.PacketConfig.Recipients.Should().Contain("dead@dealer.com");
    }

    // ── DisableRecipientForBounceAsync (Spec B-4, issue #439) ─────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DisableRecipientForBounceAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.DisableRecipientForBounceAsync(tenantId!, "loc_1", "dead@dealer.com", "Bounced");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DisableRecipientForBounceAsync_WhenRecipientEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => _sut.DisableRecipientForBounceAsync("ten_1", "loc_1", email!, "Bounced");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DisableRecipientForBounceAsync_WhenLocationNotFound_ShouldThrowKeyNotFoundException()
    {
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", "loc_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var act = () => _sut.DisableRecipientForBounceAsync("ten_1", "loc_missing", "dead@dealer.com", "Bounced");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DisableRecipientForBounceAsync_ShouldDisableOnlyThatRecipientAndPersist()
    {
        var location = BuildLocation();
        location.PacketConfig = new PacketConfigEmbedded { Recipients = ["keep@dealer.com", "dead@dealer.com"] };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        var result = await _sut.DisableRecipientForBounceAsync("ten_1", location.Id, "dead@dealer.com", "SuppressedRecipient");

        result.PacketConfig.Recipients.Should().ContainSingle().Which.Should().Be("keep@dealer.com");
        result.PacketConfig.DisabledRecipients.Should().ContainSingle().Which.Email.Should().Be("dead@dealer.com");
        result.PacketConfig.Enabled.Should().BeTrue("a bounce never disables the whole configuration");
        _locationRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableRecipientForBounceAsync_ShouldNotifyEachRemainingRecipient()
    {
        var location = BuildLocation();
        location.PacketConfig = new PacketConfigEmbedded
        {
            Recipients = ["a@dealer.com", "b@dealer.com", "dead@dealer.com"],
        };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        await _sut.DisableRecipientForBounceAsync("ten_1", location.Id, "dead@dealer.com", "Bounced");

        _notificationMock.Verify(n => n.SendEmailAsync("a@dealer.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationMock.Verify(n => n.SendEmailAsync("b@dealer.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationMock.Verify(n => n.SendEmailAsync("dead@dealer.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisableRecipientForBounceAsync_WhenNoActiveRecipientsRemain_ShouldNotNotifyAnyone()
    {
        var location = BuildLocation();
        location.PacketConfig = new PacketConfigEmbedded { Recipients = ["dead@dealer.com"] };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        var result = await _sut.DisableRecipientForBounceAsync("ten_1", location.Id, "dead@dealer.com", "Bounced");

        result.PacketConfig.Recipients.Should().BeEmpty();
        result.PacketConfig.DisabledRecipients.Should().ContainSingle();
        _notificationMock.Verify(
            n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DisableRecipientForBounceAsync_WhenAddressIsNotAnActiveRecipient_ShouldBeNoOp()
    {
        var location = BuildLocation();
        location.PacketConfig = new PacketConfigEmbedded { Recipients = ["keep@dealer.com"] };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var result = await _sut.DisableRecipientForBounceAsync("ten_1", location.Id, "stranger@dealer.com", "Bounced");

        result.PacketConfig.Recipients.Should().ContainSingle().Which.Should().Be("keep@dealer.com");
        result.PacketConfig.DisabledRecipients.Should().BeEmpty();
        _locationRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationMock.Verify(
            n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DisableRecipientForBounceAsync_WhenANotificationSendThrows_ShouldStillNotifyTheOthersAndSucceed()
    {
        var location = BuildLocation();
        location.PacketConfig = new PacketConfigEmbedded
        {
            Recipients = ["bad@dealer.com", "good@dealer.com", "dead@dealer.com"],
        };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);
        _notificationMock
            .Setup(n => n.SendEmailAsync("bad@dealer.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transport down"));

        var act = () => _sut.DisableRecipientForBounceAsync("ten_1", location.Id, "dead@dealer.com", "Bounced");

        await act.Should().NotThrowAsync();
        _notificationMock.Verify(n => n.SendEmailAsync("good@dealer.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ReEnableRecipientAsync (Spec B-4, issue #439) ────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ReEnableRecipientAsync_WhenRecipientEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => _sut.ReEnableRecipientAsync("ten_1", "loc_1", email!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReEnableRecipientAsync_WhenLocationNotFound_ShouldThrowKeyNotFoundException()
    {
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", "loc_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var act = () => _sut.ReEnableRecipientAsync("ten_1", "loc_missing", "dead@dealer.com");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ReEnableRecipientAsync_ShouldMoveTheAddressBackToActiveAndPersist()
    {
        var location = BuildLocation();
        location.PacketConfig = new PacketConfigEmbedded
        {
            Recipients = ["live@dealer.com"],
            DisabledRecipients = [new DisabledRecipientEmbedded { Email = "dead@dealer.com" }],
        };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _locationRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location e, CancellationToken _) => e);

        var result = await _sut.ReEnableRecipientAsync("ten_1", location.Id, "dead@dealer.com");

        result.PacketConfig.Recipients.Should().BeEquivalentTo(["live@dealer.com", "dead@dealer.com"]);
        result.PacketConfig.DisabledRecipients.Should().BeEmpty();
        _locationRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReEnableRecipientAsync_WhenAddressIsNotDisabled_ShouldBeNoOp()
    {
        var location = BuildLocation();
        location.PacketConfig = new PacketConfigEmbedded { Recipients = ["live@dealer.com"] };

        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        await _sut.ReEnableRecipientAsync("ten_1", location.Id, "stranger@dealer.com");

        _locationRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DeleteAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.DeleteAsync(tenantId!, "loc_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DeleteAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.DeleteAsync("ten_1", id!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", "loc_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var act = () => _sut.DeleteAsync("ten_1", "loc_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteLocationThenSlug()
    {
        var location = BuildLocation();
        _locationRepoMock.Setup(r => r.GetByIdAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _locationRepoMock.Setup(r => r.DeleteAsync("ten_1", location.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _slugRepoMock.Setup(r => r.DeleteAsync(location.Slug, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.DeleteAsync("ten_1", location.Id);

        _locationRepoMock.Verify(r => r.DeleteAsync("ten_1", location.Id, It.IsAny<CancellationToken>()), Times.Once);
        _slugRepoMock.Verify(r => r.DeleteAsync(location.Slug, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Location BuildLocation() => new()
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
        }
    };
}
