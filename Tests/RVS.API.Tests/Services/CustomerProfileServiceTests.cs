using FluentAssertions;
using Moq;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class CustomerProfileServiceTests
{
    private readonly Mock<ICustomerProfileRepository> _repoMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly CustomerProfileService _sut;

    public CustomerProfileServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _sut = new CustomerProfileService(_repoMock.Object, _userContextMock.Object);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetByIdAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetByIdAsync(tenantId!, "cp_1");

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
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "cp_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile?)null);

        var act = () => _sut.GetByIdAsync("ten_1", "cp_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnProfile()
    {
        var profile = BuildProfile();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", profile.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _sut.GetByIdAsync("ten_1", profile.Id);

        result.Should().BeSameAs(profile);
    }

    // ── GetByEmailAsync ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetByEmailAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetByEmailAsync(tenantId!, "mike@test.com");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetByEmailAsync_WhenEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => _sut.GetByEmailAsync("ten_1", email!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByEmailAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByEmailAsync("ten_1", "missing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile?)null);

        var act = () => _sut.GetByEmailAsync("ten_1", "missing@test.com");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByEmailAsync_WhenExists_ShouldReturnProfile()
    {
        var profile = BuildProfile();
        _repoMock.Setup(r => r.GetByEmailAsync("ten_1", "mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _sut.GetByEmailAsync("ten_1", "mike@test.com");

        result.Should().BeSameAs(profile);
    }

    // ── GetOrCreateAsync ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetOrCreateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetOrCreateAsync(tenantId!, "mike@test.com", "Mike", "Johnson");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetOrCreateAsync_WhenEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => _sut.GetOrCreateAsync("ten_1", email!, "Mike", "Johnson");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenProfileExists_ShouldReturnExisting()
    {
        var existing = BuildProfile();
        _repoMock.Setup(r => r.GetByEmailAsync("ten_1", "mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _sut.GetOrCreateAsync("ten_1", "Mike@Test.com", "Mike", "Johnson");

        result.Should().BeSameAs(existing);
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenProfileDoesNotExist_ShouldCreateNew()
    {
        _repoMock.Setup(r => r.GetByEmailAsync("ten_1", "mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile?)null);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile e, CancellationToken _) => e);

        var result = await _sut.GetOrCreateAsync("ten_1", "  Mike@Test.com  ", "  Mike  ", "  Johnson  ");

        result.TenantId.Should().Be("ten_1");
        result.Email.Should().Be("mike@test.com");
        result.FirstName.Should().Be("Mike");
        result.LastName.Should().Be("Johnson");
        result.Name.Should().Be("Mike Johnson");
        result.CreatedByUserId.Should().Be("usr_test");
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.UpdateAsync(tenantId!, "cp_1", BuildProfile());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.UpdateAsync("ten_1", id!, BuildProfile());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.UpdateAsync("ten_1", "cp_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "cp_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile?)null);

        var act = () => _sut.UpdateAsync("ten_1", "cp_missing", BuildProfile());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ShouldApplyChangesAndCallMarkAsUpdated()
    {
        var existing = BuildProfile();
        var updated = new CustomerProfile
        {
            TenantId = "ten_1",
            FirstName = "Michael",
            LastName = "Johnson Jr.",
            Phone = "(555) 000-0000"
        };

        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile e, CancellationToken _) => e);

        var result = await _sut.UpdateAsync("ten_1", existing.Id, updated);

        result.FirstName.Should().Be("Michael");
        result.LastName.Should().Be("Johnson Jr.");
        result.Phone.Should().Be("(555) 000-0000");
        result.Name.Should().Be("Michael Johnson Jr.");
        result.UpdatedByUserId.Should().Be("usr_test");
        result.UpdatedAtUtc.Should().NotBeNull();
    }

    // ── ResolveAndTrackAssetAsync ────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ResolveAndTrackAssetAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.ResolveAndTrackAssetAsync(tenantId!, "mike@test.com", "VIN123");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ResolveAndTrackAssetAsync_WhenEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => _sut.ResolveAndTrackAssetAsync("ten_1", email!, "VIN123");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ResolveAndTrackAssetAsync_WhenAssetIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? assetId)
    {
        var act = () => _sut.ResolveAndTrackAssetAsync("ten_1", "mike@test.com", assetId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveAndTrackAssetAsync_Branch2_NewAsset_ShouldAddWithActiveStatus()
    {
        var profile = BuildProfile();
        _repoMock.Setup(r => r.GetByEmailAsync("ten_1", "mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_1", "VIN123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile?)null);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile e, CancellationToken _) => e);

        var result = await _sut.ResolveAndTrackAssetAsync("ten_1", "Mike@Test.com", "VIN123");

        result.AssetsOwned.Should().ContainSingle(a => a.AssetId == "VIN123" && a.Status == AssetOwnershipStatus.Active);
        result.AssetsOwned.First().RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task ResolveAndTrackAssetAsync_Branch2_NewAssetWithMetadata_ShouldPopulateManufacturerModelYear()
    {
        var profile = BuildProfile();
        _repoMock.Setup(r => r.GetByEmailAsync("ten_1", "mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_1", "VIN123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile?)null);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile e, CancellationToken _) => e);

        var result = await _sut.ResolveAndTrackAssetAsync("ten_1", "Mike@Test.com", "VIN123", "Winnebago", "View 24D", 2023);

        var asset = result.AssetsOwned.First(a => a.AssetId == "VIN123" && a.Status == AssetOwnershipStatus.Active);
        asset.Manufacturer.Should().Be("Winnebago");
        asset.Model.Should().Be("View 24D");
        asset.Year.Should().Be(2023);
    }

    [Fact]
    public async Task ResolveAndTrackAssetAsync_Branch3_SameProfileOwnsAsset_ShouldIncrementRequestCount()
    {
        var profile = BuildProfile();
        profile.AssetsOwned.Add(new AssetOwnershipEmbedded
        {
            AssetId = "VIN123",
            Status = AssetOwnershipStatus.Active,
            FirstSeenAtUtc = DateTime.UtcNow.AddDays(-10),
            LastSeenAtUtc = DateTime.UtcNow.AddDays(-1),
            RequestCount = 3,
        });

        _repoMock.Setup(r => r.GetByEmailAsync("ten_1", "mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_1", "VIN123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile e, CancellationToken _) => e);

        var result = await _sut.ResolveAndTrackAssetAsync("ten_1", "Mike@Test.com", "VIN123");

        var asset = result.AssetsOwned.First(a => a.AssetId == "VIN123" && a.Status == AssetOwnershipStatus.Active);
        asset.RequestCount.Should().Be(4);
        asset.LastSeenAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ResolveAndTrackAssetAsync_Branch1_DifferentOwner_ShouldTransferOwnership()
    {
        var currentOwner = new CustomerProfile
        {
            Id = "cp_old_owner",
            TenantId = "ten_1",
            Email = "old@test.com",
            AssetsOwned =
            [
                new AssetOwnershipEmbedded
                {
                    AssetId = "VIN123",
                    Status = AssetOwnershipStatus.Active,
                    RequestCount = 5,
                }
            ]
        };

        var newProfile = BuildProfile();
        _repoMock.Setup(r => r.GetByEmailAsync("ten_1", "mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newProfile);
        _repoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_1", "VIN123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentOwner);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile e, CancellationToken _) => e);

        var result = await _sut.ResolveAndTrackAssetAsync("ten_1", "Mike@Test.com", "VIN123");

        // Old owner should have the asset deactivated
        currentOwner.AssetsOwned.First().Status.Should().Be(AssetOwnershipStatus.Inactive);
        currentOwner.AssetsOwned.First().DeactivatedAtUtc.Should().NotBeNull();
        currentOwner.AssetsOwned.First().DeactivationReason.Should().Be("OwnershipTransfer");

        // New profile should have the asset activated
        result.AssetsOwned.Should().ContainSingle(a => a.AssetId == "VIN123" && a.Status == AssetOwnershipStatus.Active);
        result.AssetsOwned.First(a => a.AssetId == "VIN123" && a.Status == AssetOwnershipStatus.Active).RequestCount.Should().Be(1);

        // Both profiles should have been updated
        _repoMock.Verify(r => r.UpdateAsync(currentOwner, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(newProfile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAndTrackAssetAsync_WhenProfileDoesNotExist_ShouldCreateThenTrack()
    {
        _repoMock.Setup(r => r.GetByEmailAsync("ten_1", "mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile?)null);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile e, CancellationToken _) => e);
        _repoMock.Setup(r => r.GetByActiveAssetIdAsync("ten_1", "VIN123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile?)null);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerProfile e, CancellationToken _) => e);

        var result = await _sut.ResolveAndTrackAssetAsync("ten_1", "Mike@Test.com", "VIN123");

        result.TenantId.Should().Be("ten_1");
        result.Email.Should().Be("mike@test.com");
        result.AssetsOwned.Should().ContainSingle(a => a.AssetId == "VIN123" && a.Status == AssetOwnershipStatus.Active);

        _repoMock.Verify(r => r.CreateAsync(It.IsAny<CustomerProfile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CustomerProfile BuildProfile() => new()
    {
        TenantId = "ten_1",
        Email = "mike@test.com",
        FirstName = "Mike",
        LastName = "Johnson",
        Name = "Mike Johnson",
    };
}
