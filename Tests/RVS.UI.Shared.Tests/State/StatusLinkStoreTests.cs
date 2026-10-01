using FluentAssertions;
using RVS.Blazor.Intake.State;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.State;

/// <summary>
/// The device remembers the customer's status link so "Check Request Status" can open it
/// directly (issue #716). These pin down what "an unexpired link" means on the client.
/// </summary>
public class StatusLinkStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryWebStorageJSRuntime _js = new("localStorage");
    private readonly StatusLinkStore _sut;

    public StatusLinkStoreTests()
    {
        _sut = new StatusLinkStore(_js, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task GetUnexpiredTokenAsync_WhenNothingSaved_ShouldReturnNull()
    {
        var token = await _sut.GetUnexpiredTokenAsync();

        token.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_ThenGetUnexpiredTokenAsync_ShouldReturnTheToken()
    {
        await _sut.SaveAsync("abc:123", Now.UtcDateTime.AddDays(30));

        var token = await _sut.GetUnexpiredTokenAsync();

        token.Should().Be("abc:123");
    }

    [Fact]
    public async Task SaveAsync_ShouldWriteToLocalStorageSoTheLinkOutlivesTheTab()
    {
        await _sut.SaveAsync("abc:123", Now.UtcDateTime.AddDays(30));

        _js.Items.Should().ContainKey(StatusLinkStore.StorageKey);
    }

    [Fact]
    public async Task GetUnexpiredTokenAsync_WhenSavedLinkHasExpired_ShouldReturnNullAndForgetIt()
    {
        await _sut.SaveAsync("abc:123", Now.UtcDateTime.AddDays(30));
        var later = new StatusLinkStore(_js, new FixedTimeProvider(Now.AddDays(31)));

        var token = await later.GetUnexpiredTokenAsync();

        token.Should().BeNull();
        _js.Items.Should().NotContainKey(StatusLinkStore.StorageKey);
    }

    [Fact]
    public async Task GetUnexpiredTokenAsync_WhenSavedLinkHasNoExpiry_ShouldReturnTheToken()
    {
        // The API treats a token with no expiry as never expiring; the client agrees.
        await _sut.SaveAsync("abc:123", null);

        var token = await _sut.GetUnexpiredTokenAsync();

        token.Should().Be("abc:123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_WhenTokenIsBlank_ShouldNotOverwriteASavedLink(string? blank)
    {
        await _sut.SaveAsync("abc:123", Now.UtcDateTime.AddDays(30));

        await _sut.SaveAsync(blank, Now.UtcDateTime.AddDays(30));

        (await _sut.GetUnexpiredTokenAsync()).Should().Be("abc:123");
    }

    [Fact]
    public async Task SaveAsync_WhenAlreadyExpired_ShouldNotSaveIt()
    {
        await _sut.SaveAsync("abc:123", Now.UtcDateTime.AddSeconds(-1));

        _js.Items.Should().NotContainKey(StatusLinkStore.StorageKey);
    }

    [Fact]
    public async Task SaveAsync_WithANewerLink_ShouldReplaceTheOldOne()
    {
        await _sut.SaveAsync("old:token", Now.UtcDateTime.AddDays(5));

        await _sut.SaveAsync("new:token", Now.UtcDateTime.AddDays(90));

        (await _sut.GetUnexpiredTokenAsync()).Should().Be("new:token");
    }

    [Fact]
    public async Task GetUnexpiredTokenAsync_WhenStoredValueIsCorrupt_ShouldReturnNullAndForgetIt()
    {
        _js.Items[StatusLinkStore.StorageKey] = "{not json";

        var token = await _sut.GetUnexpiredTokenAsync();

        token.Should().BeNull();
        _js.Items.Should().NotContainKey(StatusLinkStore.StorageKey);
    }

    [Fact]
    public async Task GetUnexpiredTokenAsync_WhenStoredValueHasNoToken_ShouldReturnNull()
    {
        _js.Items[StatusLinkStore.StorageKey] = """{"token":"","expiresAtUtc":null}""";

        var token = await _sut.GetUnexpiredTokenAsync();

        token.Should().BeNull();
    }

    [Fact]
    public async Task ForgetIfSavedAsync_WhenTokenMatches_ShouldForgetIt()
    {
        await _sut.SaveAsync("abc:123", Now.UtcDateTime.AddDays(30));

        await _sut.ForgetIfSavedAsync("abc:123");

        (await _sut.GetUnexpiredTokenAsync()).Should().BeNull();
    }

    [Fact]
    public async Task ForgetIfSavedAsync_WhenTokenDiffers_ShouldKeepTheSavedLink()
    {
        // A dead link opened from an old email must not wipe the good one this device holds.
        await _sut.SaveAsync("abc:123", Now.UtcDateTime.AddDays(30));

        await _sut.ForgetIfSavedAsync("old:emailed");

        (await _sut.GetUnexpiredTokenAsync()).Should().Be("abc:123");
    }

    [Fact]
    public async Task GetUnexpiredTokenAsync_WhenStorageIsBlocked_ShouldReturnNull()
    {
        _js.ThrowOnAccess = true;

        var token = await _sut.GetUnexpiredTokenAsync();

        token.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WhenStorageIsBlocked_ShouldNotThrow()
    {
        _js.ThrowOnAccess = true;

        var act = () => _sut.SaveAsync("abc:123", Now.UtcDateTime.AddDays(30));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ForgetIfSavedAsync_WhenStorageIsBlocked_ShouldNotThrow()
    {
        _js.ThrowOnAccess = true;

        var act = () => _sut.ForgetIfSavedAsync("abc:123");

        await act.Should().NotThrowAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
