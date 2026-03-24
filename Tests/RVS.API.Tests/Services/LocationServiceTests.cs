using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class LocationServiceTests
{
    private readonly Mock<ILocationRepository> _locationRepoMock = new();
    private readonly Mock<ISlugLookupRepository> _slugRepoMock = new();
    private readonly Mock<IDealershipRepository> _dealershipRepoMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
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
