using Microsoft.JSInterop;

namespace RVS.Blazor.Intake.Pages.Steps.Shared;

/// <summary>
/// Notices that the page was reloaded while the camera or a file picker was open (issue #736).
/// <para>
/// On a phone short of memory — a Galaxy A16 was where we saw it — Android can kill the browser
/// tab while the camera app is in front. The photo is then handed to a page that no longer exists
/// and is lost; no page code can catch it. What the page can do is say so, instead of leaving the
/// customer on a step that looks as if nothing happened. <c>wwwroot/js/interop.js</c> marks every
/// file-input click in <c>sessionStorage</c> and clears the mark when the input reports a choice
/// or a cancel; a mark still there when the page loads means the picker never came back.
/// </para>
/// </summary>
public static class PickerReload
{
    /// <summary>The JS function in <c>wwwroot/js/interop.js</c> that reads and clears the mark.</summary>
    public const string TakeFunction = "rvs_takePickerReload";

    /// <summary>
    /// Whether the page was reloaded while a picker was open. Clears the mark, so it is reported
    /// once. A failed check reports no reload.
    /// </summary>
    public static async Task<bool> TakeAsync(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);

        try
        {
            return await js.InvokeAsync<bool>(TakeFunction);
        }
        catch (JSException)
        {
            return false;
        }
    }
}
