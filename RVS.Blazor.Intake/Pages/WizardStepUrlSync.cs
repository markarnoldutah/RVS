namespace RVS.Blazor.Intake.Pages;

/// <summary>What the wizard host should do after comparing the URL's step with its own.</summary>
public enum WizardStepSyncAction
{
    /// <summary>The URL and the wizard agree; nothing to do.</summary>
    None,

    /// <summary>Move the wizard to the step the URL asks for.</summary>
    AdoptUrlStep,

    /// <summary>Correct the URL to the step the wizard is on, without adding a history entry.</summary>
    RewriteUrl
}

/// <summary>The action the host should take, and the step it applies to.</summary>
public readonly record struct WizardStepSync(WizardStepSyncAction Action, int Step);

/// <summary>
/// Reconciles the <c>step</c> query parameter with the wizard's current step, so the browser's
/// Back and Forward buttons walk the wizard instead of leaving the form.
/// <para>
/// Every forward move pushes a history entry carrying the new step, which makes Back an ordinary
/// browser navigation back to the previous step's entry. That also makes the step something a
/// customer can type, so a step past <c>maxStepReached</c> is refused: it would skip the
/// validation of the steps in between.
/// </para>
/// </summary>
public static class WizardStepUrlSync
{
    /// <summary>The query parameter that carries the wizard step.</summary>
    public const string StepParameterName = "step";

    /// <summary>
    /// Decides what to do about <paramref name="urlStep"/> — the step in the URL, absent on the
    /// entry link — given the wizard's <paramref name="currentStep"/> and the furthest step the
    /// customer has reached.
    /// </summary>
    /// <param name="isFirstLoad">
    /// True while the host is initializing. Only then does a URL with no step mean "the entry
    /// link", and the restored session is the better record of where the customer was. After
    /// that, the host has stamped the step into the URL, so a missing one means the address bar
    /// was edited back to the bare link, which is a request for Step 1.
    /// </param>
    public static WizardStepSync Reconcile(int? urlStep, int currentStep, int maxStepReached, bool isFirstLoad)
    {
        if (urlStep is null)
        {
            return isFirstLoad
                ? new WizardStepSync(WizardStepSyncAction.RewriteUrl, currentStep)
                : Reconcile(1, currentStep, maxStepReached, isFirstLoad);
        }

        var allowed = Math.Clamp(urlStep.Value, 1, Math.Max(1, maxStepReached));

        // The URL named a step the customer has not reached. Rewrite rather than push, so the
        // attempt leaves nothing to press Forward into.
        if (allowed != urlStep.Value)
        {
            return new WizardStepSync(WizardStepSyncAction.RewriteUrl, allowed);
        }

        return allowed == currentStep
            ? new WizardStepSync(WizardStepSyncAction.None, currentStep)
            : new WizardStepSync(WizardStepSyncAction.AdoptUrlStep, allowed);
    }
}
