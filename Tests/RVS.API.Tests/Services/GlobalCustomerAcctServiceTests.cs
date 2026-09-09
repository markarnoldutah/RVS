using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Interfaces;
using RVS.Domain.Security;

namespace RVS.API.Tests.Services;

public class GlobalCustomerAcctServiceTests
{
    private readonly Mock<IGlobalCustomerAcctRepository> _repoMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly Mock<ILogger<GlobalCustomerAcctService>> _loggerMock = new();
    private readonly GlobalCustomerAcctService _sut;

    public GlobalCustomerAcctServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct e, CancellationToken _) => e);
        _sut = new GlobalCustomerAcctService(_repoMock.Object, _userContextMock.Object, _loggerMock.Object);
    }

    private void VerifyAuditLogged(string outcome) =>
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("status token") && v.ToString()!.Contains("outcome=" + outcome)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

    // ── GetByEmailAsync ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetByEmailAsync_WhenEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => _sut.GetByEmailAsync(email!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByEmailAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByEmailAsync("missing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct?)null);

        var act = () => _sut.GetByEmailAsync("missing@test.com");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByEmailAsync_WhenExists_ShouldReturnAccount()
    {
        var account = BuildAccount();
        _repoMock.Setup(r => r.GetByEmailAsync("mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.GetByEmailAsync("mike@test.com");

        result.Should().BeSameAs(account);
    }

    // ── GetOrCreateAsync ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetOrCreateAsync_WhenEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => _sut.GetOrCreateAsync(email!, "Mike", "Johnson");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetOrCreateAsync_WhenFirstNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? firstName)
    {
        var act = () => _sut.GetOrCreateAsync("mike@test.com", firstName!, "Johnson");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetOrCreateAsync_WhenLastNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? lastName)
    {
        var act = () => _sut.GetOrCreateAsync("mike@test.com", "Mike", lastName!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenAccountExists_ShouldReturnExisting()
    {
        var existing = BuildAccount();
        _repoMock.Setup(r => r.GetByEmailAsync("mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _sut.GetOrCreateAsync("Mike@Test.com", "Mike", "Johnson");

        result.Should().BeSameAs(existing);
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenAccountDoesNotExist_ShouldCreateNew()
    {
        _repoMock.Setup(r => r.GetByEmailAsync("mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct?)null);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct e, CancellationToken _) => e);

        var result = await _sut.GetOrCreateAsync("  Mike@Test.com  ", "  Mike  ", "  Johnson  ");

        result.Email.Should().Be("mike@test.com");
        result.FirstName.Should().Be("Mike");
        result.LastName.Should().Be("Johnson");
        result.CreatedByUserId.Should().Be("usr_test");
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── LinkProfileAsync ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task LinkProfileAsync_WhenIdentityIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? identityId)
    {
        var act = () => _sut.LinkProfileAsync(identityId!, "ten_1", "cp_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task LinkProfileAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.LinkProfileAsync("gca_1", tenantId!, "cp_1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task LinkProfileAsync_WhenProfileIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? profileId)
    {
        var act = () => _sut.LinkProfileAsync("gca_1", "ten_1", profileId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LinkProfileAsync_WhenAccountNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync("gca_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct?)null);

        var act = () => _sut.LinkProfileAsync("gca_missing", "ten_1", "cp_1");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task LinkProfileAsync_WhenNewProfile_ShouldAddLinkedProfile()
    {
        var account = BuildAccount();
        _repoMock.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.LinkProfileAsync(account.Id, "ten_1", "cp_1");

        result.LinkedProfiles.Should().ContainSingle(lp => lp.TenantId == "ten_1" && lp.ProfileId == "cp_1");
        result.UpdatedByUserId.Should().Be("usr_test");
    }

    [Fact]
    public async Task LinkProfileAsync_WhenAlreadyLinked_ShouldNotDuplicate()
    {
        var account = BuildAccount();
        account.LinkedProfiles.Add(new LinkedProfileEmbedded
        {
            TenantId = "ten_1",
            ProfileId = "cp_1",
            FirstSeenAtUtc = DateTime.UtcNow.AddDays(-10),
            RequestCount = 5
        });

        _repoMock.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.LinkProfileAsync(account.Id, "ten_1", "cp_1");

        result.LinkedProfiles.Should().HaveCount(1);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── GenerateMagicLinkTokenAsync (Spec X-5: hashed, ≥128-bit, TTL ≤ 30d) ───

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GenerateMagicLinkTokenAsync_WhenEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => _sut.GenerateMagicLinkTokenAsync(email!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateMagicLinkTokenAsync_WhenAccountNotFound_ShouldThrowKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByEmailAsync("missing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct?)null);

        var act = () => _sut.GenerateMagicLinkTokenAsync("missing@test.com");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GenerateMagicLinkTokenAsync_ShouldPersistOnlyTheHashAndReturnTheRawToken()
    {
        var account = BuildAccount();
        _repoMock.Setup(r => r.GetByEmailAsync("mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.GenerateMagicLinkTokenAsync("Mike@Test.com");

        result.RawToken.Should().NotBeNullOrWhiteSpace();
        result.RawToken.Should().Contain(":");
        result.Account.MagicLinkTokenHash.Should().Be(AnonymousTokenHelper.ComputeHash(result.RawToken));
        result.Account.MagicLinkTokenHash.Should().NotBe(result.RawToken, "the raw token is never persisted");
        result.Account.MagicLinkExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
        result.Account.UpdatedByUserId.Should().Be("usr_test");
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateMagicLinkTokenAsync_ShouldUseCustomExpiry()
    {
        var account = BuildAccount();
        var customExpiry = DateTime.UtcNow.AddDays(7);
        _repoMock.Setup(r => r.GetByEmailAsync("mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.GenerateMagicLinkTokenAsync("Mike@Test.com", customExpiry);

        result.Account.MagicLinkExpiresAtUtc.Should().Be(customExpiry);
    }

    [Fact]
    public async Task GenerateMagicLinkTokenAsync_ShouldProduceDeterministicPrefixButRotatingSuffix()
    {
        var account = BuildAccount();
        _repoMock.Setup(r => r.GetByEmailAsync("mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var token1 = (await _sut.GenerateMagicLinkTokenAsync("Mike@Test.com")).RawToken;
        var token2 = (await _sut.GenerateMagicLinkTokenAsync("Mike@Test.com")).RawToken;

        token1.Split(':')[0].Should().Be(token2.Split(':')[0], "same email → same hash prefix");
        token1.Split(':')[1].Should().NotBe(token2.Split(':')[1], "random suffix rotates per call");
    }

    [Fact]
    public async Task GenerateMagicLinkTokenAsync_ShouldProduceDifferentPrefixForDifferentEmails()
    {
        _repoMock.Setup(r => r.GetByEmailAsync("mike@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccount());
        _repoMock.Setup(r => r.GetByEmailAsync("jane@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlobalCustomerAcct { Email = "jane@test.com", FirstName = "Jane", LastName = "Doe" });

        var result1 = await _sut.GenerateMagicLinkTokenAsync("Mike@Test.com");
        var result2 = await _sut.GenerateMagicLinkTokenAsync("Jane@Test.com");

        result1.RawToken.Split(':')[0].Should().NotBe(result2.RawToken.Split(':')[0]);
    }

    // ── ValidateMagicLinkTokenAsync ──────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ValidateMagicLinkTokenAsync_WhenTokenIsNullOrWhiteSpace_ShouldThrowArgumentException(string? token)
    {
        var act = () => _sut.ValidateMagicLinkTokenAsync(token!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateMagicLinkTokenAsync_ShouldLookUpByHashOfTheIncomingToken()
    {
        var account = BuildAccount();
        account.MagicLinkTokenHash = AnonymousTokenHelper.ComputeHash("prefix:suffix");
        account.MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(20);
        _repoMock.Setup(r => r.GetByMagicLinkTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.ValidateMagicLinkTokenAsync("prefix:suffix");

        _repoMock.Verify(
            r => r.GetByMagicLinkTokenHashAsync(AnonymousTokenHelper.ComputeHash("prefix:suffix"), It.IsAny<CancellationToken>()),
            Times.Once);
        _repoMock.Verify(
            r => r.GetByMagicLinkTokenHashAsync("prefix:suffix", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateMagicLinkTokenAsync_WhenHashNotFound_ShouldThrowKeyNotFoundExceptionAndAuditMiss()
    {
        _repoMock.Setup(r => r.GetByMagicLinkTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct?)null);

        var act = () => _sut.ValidateMagicLinkTokenAsync("invalid:token");

        await act.Should().ThrowAsync<KeyNotFoundException>();
        VerifyAuditLogged("miss");
    }

    [Fact]
    public async Task ValidateMagicLinkTokenAsync_WhenTokenExpired_ShouldThrowMagicLinkExpiredExceptionAndAuditExpired()
    {
        var account = BuildAccount();
        account.MagicLinkTokenHash = AnonymousTokenHelper.ComputeHash("prefix:suffix");
        account.MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(-1);
        _repoMock.Setup(r => r.GetByMagicLinkTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var act = () => _sut.ValidateMagicLinkTokenAsync("prefix:suffix");

        await act.Should().ThrowAsync<MagicLinkExpiredException>().WithMessage("*expired*");
        VerifyAuditLogged("expired");
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateMagicLinkTokenAsync_WhenTokenValid_ShouldReturnAccountAndAuditHit()
    {
        var account = BuildAccount();
        account.MagicLinkTokenHash = AnonymousTokenHelper.ComputeHash("prefix:suffix");
        account.MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(20);
        _repoMock.Setup(r => r.GetByMagicLinkTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.ValidateMagicLinkTokenAsync("prefix:suffix");

        result.Should().BeSameAs(account);
        VerifyAuditLogged("hit");
    }

    [Fact]
    public async Task ValidateMagicLinkTokenAsync_WhenTokenNearingExpiry_ShouldSlideRenewalForwardAndPersist()
    {
        var account = BuildAccount();
        account.MagicLinkTokenHash = AnonymousTokenHelper.ComputeHash("prefix:suffix");
        account.MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(3);
        _repoMock.Setup(r => r.GetByMagicLinkTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.ValidateMagicLinkTokenAsync("prefix:suffix");

        result.MagicLinkExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
        result.UpdatedByUserId.Should().Be("usr_test");
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateMagicLinkTokenAsync_WhenTokenComfortablyValid_ShouldNotWrite()
    {
        var account = BuildAccount();
        account.MagicLinkTokenHash = AnonymousTokenHelper.ComputeHash("prefix:suffix");
        account.MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(29.5);
        _repoMock.Setup(r => r.GetByMagicLinkTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.ValidateMagicLinkTokenAsync("prefix:suffix");

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateMagicLinkTokenAsync_WhenExpiryIsNull_ShouldRenewAndReturnAccount()
    {
        var account = BuildAccount();
        account.MagicLinkTokenHash = AnonymousTokenHelper.ComputeHash("prefix:suffix");
        account.MagicLinkExpiresAtUtc = null;
        _repoMock.Setup(r => r.GetByMagicLinkTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.ValidateMagicLinkTokenAsync("prefix:suffix");

        result.Should().BeSameAs(account);
        result.MagicLinkExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GlobalCustomerAcct BuildAccount() => new()
    {
        Email = "mike@test.com",
        FirstName = "Mike",
        LastName = "Johnson",
    };
}
