using FluentAssertions;
using RVS.Blazor.Intake.Services;
using RVS.UI.Shared.Tests.Fakes;
using RVS.UI.Shared.Theme;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests for the Intake <see cref="ThemeService"/> — the high-contrast toggle survives a reload (issue #758).
/// </summary>
public class IntakeThemeServiceTests
{
    private readonly InMemoryWebStorageJSRuntime _js = new("localStorage");

    [Fact]
    public void Constructor_WhenJsRuntimeIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new ThemeService(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ToggleAsync_ThenInitializeAsyncOnANewInstance_ShouldRestoreHighContrast()
    {
        var before = new ThemeService(_js);
        await before.ToggleAsync();

        var after = new ThemeService(_js);
        await after.InitializeAsync();

        after.Mode.Should().Be(ThemeMode.HighContrast);
        after.CurrentTheme.Should().BeSameAs(IntakeTheme.HighContrast);
    }

    [Fact]
    public async Task ToggleAsync_Twice_ShouldPersistLight()
    {
        var before = new ThemeService(_js);
        await before.ToggleAsync();
        await before.ToggleAsync();

        var after = new ThemeService(_js);
        await after.InitializeAsync();

        after.Mode.Should().Be(ThemeMode.Light);
    }

    [Fact]
    public async Task ToggleAsync_ShouldNotifySubscribers()
    {
        var sut = new ThemeService(_js);
        var raised = 0;
        sut.OnThemeChanged += () => raised++;

        await sut.ToggleAsync();

        raised.Should().Be(1);
    }

    [Fact]
    public async Task InitializeAsync_NothingStored_ShouldStayLight()
    {
        var sut = new ThemeService(_js);

        await sut.InitializeAsync();

        sut.Mode.Should().Be(ThemeMode.Light);
    }

    [Fact]
    public async Task InitializeAsync_UnrecognisedValue_ShouldStayLight()
    {
        // Intake has no dark mode; a value it does not know is not a reason to guess.
        _js.Items[ThemeService.ThemeStorageKey] = "dark";
        var sut = new ThemeService(_js);

        await sut.InitializeAsync();

        sut.Mode.Should().Be(ThemeMode.Light);
    }

    [Fact]
    public async Task InitializeAsync_StorageBlocked_ShouldStayLightWithoutThrowing()
    {
        _js.ThrowOnAccess = true;
        var sut = new ThemeService(_js);

        await sut.InitializeAsync();

        sut.Mode.Should().Be(ThemeMode.Light);
    }

    [Fact]
    public async Task ToggleAsync_StorageBlocked_ShouldStillSwitchTheTheme()
    {
        _js.ThrowOnAccess = true;
        var sut = new ThemeService(_js);

        await sut.ToggleAsync();

        sut.Mode.Should().Be(ThemeMode.HighContrast);
    }
}
