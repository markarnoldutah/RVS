using FluentAssertions;
using Microsoft.JSInterop;
using RVS.Blazor.Intake.Pages.Steps.Shared;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.Pages.Steps;

/// <summary>
/// Tests for <see cref="PickerReload"/> — noticing that Android reloaded the page while the
/// camera or file picker was open (issue #736).
/// </summary>
public class PickerReloadTests
{
    [Fact]
    public async Task TakeAsync_ShouldAskTheBrowserOnce()
    {
        var js = new RecordingJSRuntime();

        await PickerReload.TakeAsync(js);

        js.Invocations.Should().Equal(PickerReload.TakeFunction);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TakeAsync_ShouldReturnWhatTheBrowserReports(bool reloaded)
    {
        var result = await PickerReload.TakeAsync(new ReturningJSRuntime(reloaded));

        result.Should().Be(reloaded);
    }

    [Fact]
    public async Task TakeAsync_WhenTheBrowserCallFails_ShouldReportNoReload()
    {
        // The notice is an explanation, not a gate; a failed check just means no notice.
        var js = new RecordingJSRuntime { ThrowOnInvoke = new JSException("sessionStorage blocked") };

        var result = await PickerReload.TakeAsync(js);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TakeAsync_WithNullJsRuntime_ShouldThrow()
    {
        var act = () => PickerReload.TakeAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed class ReturningJSRuntime(bool value) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult((TValue)(object)value);
    }
}
