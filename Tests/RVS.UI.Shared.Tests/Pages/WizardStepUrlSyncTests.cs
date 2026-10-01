using FluentAssertions;
using RVS.Blazor.Intake.Pages;

namespace RVS.UI.Shared.Tests.Pages;

/// <summary>
/// The rules that keep the browser's history and the wizard's current step agreeing with each
/// other. Every case here is something a customer can actually do: press Back, press Forward,
/// refresh, or edit the address bar.
/// </summary>
public class WizardStepUrlSyncTests
{
    // ---- First load ------------------------------------------------------------------------

    [Fact]
    public void Reconcile_FirstLoadOfABareUrl_ShouldWriteStepOneIntoTheUrl()
    {
        // The entry link has no step. Stamping it makes the first history entry one of ours,
        // so a later Back has somewhere to land.
        var result = WizardStepUrlSync.Reconcile(urlStep: null, currentStep: 1, maxStepReached: 1, isFirstLoad: true);

        result.Action.Should().Be(WizardStepSyncAction.RewriteUrl);
        result.Step.Should().Be(1);
    }

    [Fact]
    public void Reconcile_FirstLoadOfABareUrlWithARestoredSession_ShouldWriteTheRestoredStep()
    {
        // A refresh on a shared bare link: the session is the only record of where they were.
        var result = WizardStepUrlSync.Reconcile(urlStep: null, currentStep: 5, maxStepReached: 5, isFirstLoad: true);

        result.Action.Should().Be(WizardStepSyncAction.RewriteUrl);
        result.Step.Should().Be(5);
    }

    [Fact]
    public void Reconcile_FirstLoadWhereTheUrlAndTheSessionAgree_ShouldDoNothing()
    {
        var result = WizardStepUrlSync.Reconcile(urlStep: 4, currentStep: 4, maxStepReached: 4, isFirstLoad: true);

        result.Action.Should().Be(WizardStepSyncAction.None);
    }

    [Fact]
    public void Reconcile_FirstLoadWhereTheUrlIsBehindTheSession_ShouldAdoptTheUrl()
    {
        // Reload of a bookmarked ?step=2 while the session got to 6 — the URL is the request.
        var result = WizardStepUrlSync.Reconcile(urlStep: 2, currentStep: 6, maxStepReached: 6, isFirstLoad: true);

        result.Action.Should().Be(WizardStepSyncAction.AdoptUrlStep);
        result.Step.Should().Be(2);
    }

    // ---- Back and forward ------------------------------------------------------------------

    [Fact]
    public void Reconcile_WhenHistoryMovesBack_ShouldAdoptTheEarlierStep()
    {
        var result = WizardStepUrlSync.Reconcile(urlStep: 3, currentStep: 4, maxStepReached: 4, isFirstLoad: false);

        result.Action.Should().Be(WizardStepSyncAction.AdoptUrlStep);
        result.Step.Should().Be(3);
    }

    [Fact]
    public void Reconcile_WhenHistoryMovesForwardWithinWhatWasReached_ShouldAdoptTheLaterStep()
    {
        var result = WizardStepUrlSync.Reconcile(urlStep: 4, currentStep: 3, maxStepReached: 6, isFirstLoad: false);

        result.Action.Should().Be(WizardStepSyncAction.AdoptUrlStep);
        result.Step.Should().Be(4);
    }

    [Fact]
    public void Reconcile_AfterWeOurselvesPushedTheStep_ShouldDoNothing()
    {
        // Continue already moved the state, then pushed the URL; the round trip must be a no-op.
        var result = WizardStepUrlSync.Reconcile(urlStep: 3, currentStep: 3, maxStepReached: 3, isFirstLoad: false);

        result.Action.Should().Be(WizardStepSyncAction.None);
    }

    [Fact]
    public void Reconcile_WhenTheStepDisappearsFromTheUrlAfterTheFirstLoad_ShouldTreatItAsStepOne()
    {
        // Only reachable by hand-editing the address bar back to the bare link.
        var result = WizardStepUrlSync.Reconcile(urlStep: null, currentStep: 4, maxStepReached: 4, isFirstLoad: false);

        result.Action.Should().Be(WizardStepSyncAction.AdoptUrlStep);
        result.Step.Should().Be(1);
    }

    // ---- Steps the customer has not earned -------------------------------------------------

    [Fact]
    public void Reconcile_WhenTheUrlAsksForAStepBeyondTheOneReached_ShouldRewriteToTheStepReached()
    {
        // Typing ?step=8 must not skip validation. Rewrite rather than push, so the attempt
        // leaves no history entry to go forward into.
        var result = WizardStepUrlSync.Reconcile(urlStep: 8, currentStep: 2, maxStepReached: 2, isFirstLoad: false);

        result.Action.Should().Be(WizardStepSyncAction.RewriteUrl);
        result.Step.Should().Be(2);
    }

    [Fact]
    public void Reconcile_WhenTheUrlAsksForAStepBeyondTheWizard_ShouldRewriteToTheStepReached()
    {
        var result = WizardStepUrlSync.Reconcile(urlStep: 99, currentStep: 3, maxStepReached: 3, isFirstLoad: false);

        result.Action.Should().Be(WizardStepSyncAction.RewriteUrl);
        result.Step.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Reconcile_WhenTheUrlAsksForAStepBelowOne_ShouldRewriteToStepOne(int urlStep)
    {
        var result = WizardStepUrlSync.Reconcile(urlStep, currentStep: 3, maxStepReached: 5, isFirstLoad: false);

        result.Action.Should().Be(WizardStepSyncAction.RewriteUrl);
        result.Step.Should().Be(1);
    }

    [Fact]
    public void Reconcile_WhenTheUrlAsksForTheStepReached_ShouldAdoptIt()
    {
        // The boundary of the clamp: the furthest step reached is legitimate.
        var result = WizardStepUrlSync.Reconcile(urlStep: 5, currentStep: 2, maxStepReached: 5, isFirstLoad: false);

        result.Action.Should().Be(WizardStepSyncAction.AdoptUrlStep);
        result.Step.Should().Be(5);
    }

    [Fact]
    public void Reconcile_WhenTheClampLandsOnTheCurrentStep_ShouldStillRewriteTheUrl()
    {
        // State needs no change, but the URL is lying about the step and must be corrected.
        var result = WizardStepUrlSync.Reconcile(urlStep: 7, currentStep: 4, maxStepReached: 4, isFirstLoad: false);

        result.Action.Should().Be(WizardStepSyncAction.RewriteUrl);
        result.Step.Should().Be(4);
    }
}
