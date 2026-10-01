using FluentAssertions;
using Microsoft.JSInterop;
using RVS.Blazor.Intake.Pages;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.Pages;

public class WizardStepEntryTests
{
    private readonly RecordingJSRuntime _js = new();

    [Fact]
    public void Constructor_WhenJsRuntimeIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new WizardStepEntry(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task OnRenderedAsync_FirstRenderOfAStep_ShouldEnterTheStep()
    {
        var sut = new WizardStepEntry(_js);

        await sut.OnRenderedAsync(1, default);

        _js.Invocations.Should().Equal(WizardStepEntry.EnterStepFunction);
    }

    [Fact]
    public async Task OnRenderedAsync_ReRenderOfTheSameStep_ShouldNotEnterAgain()
    {
        var sut = new WizardStepEntry(_js);

        await sut.OnRenderedAsync(2, default);
        await sut.OnRenderedAsync(2, default);
        await sut.OnRenderedAsync(2, default);

        _js.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public async Task OnRenderedAsync_StepChangesForward_ShouldEnterEachStep()
    {
        var sut = new WizardStepEntry(_js);

        await sut.OnRenderedAsync(2, default);
        await sut.OnRenderedAsync(3, default);

        _js.Invocations.Should().HaveCount(2);
    }

    [Fact]
    public async Task OnRenderedAsync_StepChangesBackward_ShouldEnterTheEarlierStep()
    {
        var sut = new WizardStepEntry(_js);

        await sut.OnRenderedAsync(5, default);
        await sut.OnRenderedAsync(4, default);

        _js.Invocations.Should().HaveCount(2);
    }

    [Fact]
    public async Task OnRenderedAsync_WhenJsThrows_ShouldNotPropagate()
    {
        _js.ThrowOnInvoke = new JSException("scroll failed");
        var sut = new WizardStepEntry(_js);

        var act = () => sut.OnRenderedAsync(2, default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnRenderedAsync_AfterJsFailure_ShouldNotRetryTheSameStep()
    {
        _js.ThrowOnInvoke = new JSException("scroll failed");
        var sut = new WizardStepEntry(_js);

        await sut.OnRenderedAsync(2, default);
        _js.ThrowOnInvoke = null;
        await sut.OnRenderedAsync(2, default);

        _js.Invocations.Should().HaveCount(1);
    }
}
