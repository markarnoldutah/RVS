using FluentAssertions;
using RVS.Blazor.Intake.State;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.State;

/// <summary>
/// A customer who ticks "Remember my details on this device" gets their name, email, phone,
/// preferred contact method and vehicle filled in on their next visit (issues #811, #819).
/// These pin down the device side of that.
/// </summary>
public class RememberedDetailsStoreTests
{
    private static readonly RememberedDetails Jane =
        new("Jane", "Doe", "jane@example.com", "(801) 555-1234", "1FTFW1ET5DFC10312",
            "Text", "Grand Design", "Reflection 312BHTS", 2021, "Yes", "06/2021");

    private readonly InMemoryWebStorageJSRuntime _js = new("localStorage");
    private readonly RememberedDetailsStore _sut;

    public RememberedDetailsStoreTests()
    {
        _sut = new RememberedDetailsStore(_js);
    }

    [Fact]
    public async Task GetAsync_WhenNothingSaved_ShouldReturnNull()
    {
        var details = await _sut.GetAsync();

        details.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_ShouldReturnTheDetails()
    {
        await _sut.SaveAsync(Jane);

        var details = await _sut.GetAsync();

        details.Should().Be(Jane);
    }

    [Fact]
    public async Task SaveAsync_ShouldWriteToLocalStorageSoTheDetailsOutliveTheTab()
    {
        await _sut.SaveAsync(Jane);

        _js.Items.Should().ContainKey(RememberedDetailsStore.StorageKey);
    }

    [Fact]
    public async Task SaveAsync_WhenDetailsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.SaveAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_WithoutAVin_ShouldRoundTripTheContactDetails()
    {
        await _sut.SaveAsync(Jane with { Vin = null });

        var details = await _sut.GetAsync();

        details.Should().Be(Jane with { Vin = null });
    }

    [Fact]
    public async Task GetAsync_WhenEntryPredatesPreferredContactAndVehicle_ShouldStillReturnIt()
    {
        // Issue #819: details saved under #811 carry no preferred contact or vehicle details.
        _js.Items[RememberedDetailsStore.StorageKey] =
            """{"firstName":"Jane","lastName":"Doe","email":"jane@example.com","phone":null,"vin":"SERIAL123"}""";

        var details = await _sut.GetAsync();

        details.Should().Be(new RememberedDetails("Jane", "Doe", "jane@example.com", null, "SERIAL123"));
    }

    [Fact]
    public async Task ForgetAsync_ShouldRemoveTheSavedDetails()
    {
        await _sut.SaveAsync(Jane);

        await _sut.ForgetAsync();

        _js.Items.Should().NotContainKey(RememberedDetailsStore.StorageKey);
        (await _sut.GetAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenEntryIsUnreadable_ShouldReturnNullAndForgetIt()
    {
        _js.Items[RememberedDetailsStore.StorageKey] = "{not json";

        var details = await _sut.GetAsync();

        details.Should().BeNull();
        _js.Items.Should().NotContainKey(RememberedDetailsStore.StorageKey);
    }

    [Fact]
    public async Task GetAsync_WhenEntryHasNoEmail_ShouldReturnNullAndForgetIt()
    {
        _js.Items[RememberedDetailsStore.StorageKey] = """{"firstName":"Jane"}""";

        var details = await _sut.GetAsync();

        details.Should().BeNull();
        _js.Items.Should().NotContainKey(RememberedDetailsStore.StorageKey);
    }

    [Fact]
    public async Task GetAsync_WhenStorageIsBlocked_ShouldReturnNull()
    {
        _js.ThrowOnAccess = true;

        var details = await _sut.GetAsync();

        details.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WhenStorageIsBlocked_ShouldNotThrow()
    {
        _js.ThrowOnAccess = true;

        var act = () => _sut.SaveAsync(Jane);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ForgetAsync_WhenStorageIsBlocked_ShouldNotThrow()
    {
        _js.ThrowOnAccess = true;

        var act = () => _sut.ForgetAsync();

        await act.Should().NotThrowAsync();
    }
}
