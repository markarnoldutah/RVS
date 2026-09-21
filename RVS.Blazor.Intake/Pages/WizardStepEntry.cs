using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace RVS.Blazor.Intake.Pages;

/// <summary>
/// Puts the customer at the start of each intake wizard step (issue #645). The wizard keeps one
/// scroll position across steps, so without this a customer who pressed Continue at the bottom of
/// a long step arrived at the bottom of the next one.
/// <para>
/// Call <see cref="OnRenderedAsync"/> from the host's <c>OnAfterRenderAsync</c>. It acts once per
/// step change: the JS side scrolls to the top and focuses the step's first empty field, if the
/// step opts in with <c>data-rvs-autofocus</c>, or else the step container itself.
/// </para>
/// </summary>
public sealed class WizardStepEntry
{
    /// <summary>The JS function in <c>wwwroot/js/interop.js</c> that does the scroll and focus.</summary>
    public const string EnterStepFunction = "rvs_enterWizardStep";

    private readonly IJSRuntime _js;
    private int _enteredStep;

    public WizardStepEntry(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        _js = js;
    }

    /// <summary>
    /// Enters <paramref name="currentStep"/> if it is not the step last entered; a re-render of
    /// the same step does nothing, so typing into a field never yanks the page back to the top.
    /// </summary>
    public async Task OnRenderedAsync(int currentStep, ElementReference stepElement)
    {
        if (currentStep == _enteredStep)
        {
            return;
        }

        _enteredStep = currentStep;

        try
        {
            await _js.InvokeVoidAsync(EnterStepFunction, stepElement);
        }
        catch (JSException)
        {
            // Scroll and focus are an enhancement; the step is usable without them.
        }
    }
}
