using FluentAssertions;
using Moq;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

public class TenantConfigServiceTests
{
    private readonly Mock<ITenantConfigRepository> _repoMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly TenantConfigService _sut;

    public TenantConfigServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _sut = new TenantConfigService(_repoMock.Object, _userContextMock.Object);
    }

    // ── GetTenantConfigAsync ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetTenantConfigAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetTenantConfigAsync(tenantId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetTenantConfigAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetAsync("ten_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantConfig?)null);

        var act = () => _sut.GetTenantConfigAsync("ten_missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetTenantConfigAsync_WhenExists_ShouldReturnConfig()
    {
        var config = BuildConfig();
        _repoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _sut.GetTenantConfigAsync("ten_1");

        result.Should().BeSameAs(config);
    }

    // ── CreateTenantConfigAsync ──────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateTenantConfigAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.CreateTenantConfigAsync(tenantId!, new TenantConfigCreateRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateTenantConfigAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.CreateTenantConfigAsync("ten_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateTenantConfigAsync_WhenConfigAlreadyExists_ShouldThrowInvalidOperationException()
    {
        _repoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildConfig());

        var act = () => _sut.CreateTenantConfigAsync("ten_1", new TenantConfigCreateRequestDto());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateTenantConfigAsync_ShouldCreateAndReturnConfig()
    {
        _repoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantConfig?)null);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<TenantConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantConfig e, CancellationToken _) => e);

        var result = await _sut.CreateTenantConfigAsync("ten_1", new TenantConfigCreateRequestDto());

        result.Should().NotBeNull();
        result.TenantId.Should().Be("ten_1");
        result.Id.Should().Be("ten_1_config");
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<TenantConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateTenantConfigAsync ──────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateTenantConfigAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.UpdateTenantConfigAsync(tenantId!, new TenantConfigUpdateRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateTenantConfigAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.UpdateTenantConfigAsync("ten_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateTenantConfigAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantConfig?)null);

        var act = () => _sut.UpdateTenantConfigAsync("ten_1", new TenantConfigUpdateRequestDto());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateTenantConfigAsync_ShouldApplyUpdateAndSave()
    {
        var config = BuildConfig();
        _repoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _sut.UpdateTenantConfigAsync("ten_1", new TenantConfigUpdateRequestDto());

        result.UpdatedByUserId.Should().Be("usr_test");
        result.UpdatedAtUtc.Should().NotBeNull();
        _repoMock.Verify(r => r.SaveAsync(config, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetAccessGateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAccessGateAsync_WhenConfigExists_ShouldReturnAccessGate()
    {
        var config = BuildConfig();
        config.AccessGate = new TenantAccessGateEmbedded
        {
            LoginsEnabled = false,
            DisabledReason = "Maintenance"
        };
        _repoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _sut.GetAccessGateAsync("ten_1");

        result.LoginsEnabled.Should().BeFalse();
        result.DisabledReason.Should().Be("Maintenance");
    }

    [Fact]
    public async Task GetAccessGateAsync_WhenAccessGateIsNull_ShouldReturnDefaultWithLoginsEnabled()
    {
        var config = BuildConfig();
        config.AccessGate = null!;
        _repoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _sut.GetAccessGateAsync("ten_1");

        result.LoginsEnabled.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TenantConfig BuildConfig() => new()
    {
        Id = "ten_1_config",
        TenantId = "ten_1",
        AccessGate = new TenantAccessGateEmbedded { LoginsEnabled = true }
    };
}
