using FluentAssertions;
using RVS.Blazor.Intake.State;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.State;

/// <summary>
/// Tests for the wizard's form-start session id (<c>Spec A-13</c>, issue #839). One visit is one
/// start: the id is made the first time Step 1 shows the form, kept in <c>sessionStorage</c> with
/// the rest of the wizard, and only a new visit — a cleared session — makes another.
/// </summary>
public class IntakeWizardStateFormStartTests
{
    [Fact]
    public async Task TryBeginFormStartAsync_OnAFreshVisit_ShouldReturnANewSessionId()
    {
        var state = new IntakeWizardState(new InMemoryWebStorageJSRuntime());

        var sessionId = await state.TryBeginFormStartAsync();

        sessionId.Should().MatchRegex("^[0-9a-f]{32}$");
        state.FormStartSessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task TryBeginFormStartAsync_WhenAlreadyStarted_ShouldReturnNull()
    {
        // Back into Step 1, or Step 1 rendering again for any reason, is the same visit.
        var state = new IntakeWizardState(new InMemoryWebStorageJSRuntime());
        var first = await state.TryBeginFormStartAsync();

        var second = await state.TryBeginFormStartAsync();

        second.Should().BeNull();
        state.FormStartSessionId.Should().Be(first);
    }

    [Fact]
    public async Task TryBeginFormStartAsync_AfterARefresh_ShouldReturnNull()
    {
        var storage = new InMemoryWebStorageJSRuntime();
        var first = await new IntakeWizardState(storage).TryBeginFormStartAsync();

        var reloaded = new IntakeWizardState(storage);
        await reloaded.RestoreAsync();
        var second = await reloaded.TryBeginFormStartAsync();

        second.Should().BeNull();
        reloaded.FormStartSessionId.Should().Be(first);
    }

    [Fact]
    public async Task TryBeginFormStartAsync_AfterTheSessionIsCleared_ShouldStartANewVisit()
    {
        // A submitted visit, or a link to another location, clears the wizard; the next form
        // shown is a new visit.
        var state = new IntakeWizardState(new InMemoryWebStorageJSRuntime());
        var first = await state.TryBeginFormStartAsync();

        await state.ClearAsync();
        var second = await state.TryBeginFormStartAsync();

        second.Should().NotBeNull().And.NotBe(first);
    }

    [Fact]
    public async Task TryBeginFormStartAsync_WhenStorageIsUnavailable_ShouldStillReturnASessionId()
    {
        var state = new IntakeWizardState(new InMemoryWebStorageJSRuntime { ThrowOnAccess = true });

        var sessionId = await state.TryBeginFormStartAsync();

        sessionId.Should().NotBeNullOrWhiteSpace();
    }
}
