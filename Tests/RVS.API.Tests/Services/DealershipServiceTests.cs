using FluentAssertions;
using Moq;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class DealershipServiceTests
{
    private readonly Mock<IDealershipRepository> _repoMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly DealershipService _sut;

    public DealershipServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _sut = new DealershipService(_repoMock.Object, _userContextMock.Object);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetByIdAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetByIdAsync(tenantId!, "dlr_1");

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
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "dlr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dealership?)null);

        var act = () => _sut.GetByIdAsync("ten_1", "dlr_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnDealership()
    {
        var dealership = BuildDealership();
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", dealership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dealership);

        var result = await _sut.GetByIdAsync("ten_1", dealership.Id);

        result.Should().BeSameAs(dealership);
    }

    // ── GetBySlugAsync ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetBySlugAsync_WhenSlugIsNullOrWhiteSpace_ShouldThrowArgumentException(string? slug)
    {
        var act = () => _sut.GetBySlugAsync(slug!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetBySlugAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetBySlugAsync("no-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dealership?)null);

        var act = () => _sut.GetBySlugAsync("no-slug");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetBySlugAsync_WhenExists_ShouldReturnDealership()
    {
        var dealership = BuildDealership();
        _repoMock.Setup(r => r.GetBySlugAsync("blue-compass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dealership);

        var result = await _sut.GetBySlugAsync("blue-compass");

        result.Should().BeSameAs(dealership);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.CreateAsync(tenantId!, BuildDealership());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.CreateAsync("ten_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_ShouldDelegateToRepository()
    {
        var entity = BuildDealership();
        _repoMock.Setup(r => r.CreateAsync(entity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.CreateAsync("ten_1", entity);

        result.Should().BeSameAs(entity);
        _repoMock.Verify(r => r.CreateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.UpdateAsync(tenantId!, "dlr_1", BuildUpdateRequest());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateAsync_WhenIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? id)
    {
        var act = () => _sut.UpdateAsync("ten_1", id!, BuildUpdateRequest());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.UpdateAsync("ten_1", "dlr_1", (DealershipUpdateRequestDto)null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("ten_1", "dlr_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dealership?)null);

        var act = () => _sut.UpdateAsync("ten_1", "dlr_missing", BuildUpdateRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ShouldApplyChangesAndCallMarkAsUpdated()
    {
        var existing = BuildDealership();
        var request = new DealershipUpdateRequestDto
        {
            Name = "Updated Name",
            Slug = "updated-slug",
            LogoUrl = "https://cdn.example.com/new-logo.png",
            ServiceEmail = "new@example.com",
            Phone = "(555) 000-0000"
        };

        _repoMock.Setup(r => r.GetByIdAsync("ten_1", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Dealership>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dealership e, CancellationToken _) => e);

        var result = await _sut.UpdateAsync("ten_1", existing.Id, request);

        result.Name.Should().Be("Updated Name");
        result.Slug.Should().Be("updated-slug");
        result.LogoUrl.Should().Be("https://cdn.example.com/new-logo.png");
        result.ServiceEmail.Should().Be("new@example.com");
        result.Phone.Should().Be("(555) 000-0000");
        result.UpdatedByUserId.Should().Be("usr_test");
        result.UpdatedAtUtc.Should().NotBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dealership BuildDealership() => new()
    {
        TenantId = "ten_1",
        Name = "Blue Compass RV",
        Slug = "blue-compass",
        Phone = "(801) 555-1000"
    };

    private static DealershipUpdateRequestDto BuildUpdateRequest() => new()
    {
        Name = "Blue Compass RV",
        Slug = "blue-compass",
        Phone = "(801) 555-1000"
    };
}
