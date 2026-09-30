using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace RVS.Blazor.Intake.State;

/// <summary>
/// What "Remember my details on this device" keeps (issue #811, Spec A-7): contact details and
/// the RV's VIN or serial number. Contact preferences and opt-outs are deliberately not kept.
/// </summary>
public sealed record RememberedDetails(
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("vin")] string? Vin);

/// <summary>
/// Keeps the customer's <see cref="RememberedDetails"/> on this device, only when they asked for
/// it on Step 2 (issue #811, Spec A-7).
/// <para>
/// Persisted in <c>localStorage</c> so it survives to the next visit, which for an RV customer
/// is often next season. Opt-in because a shared browser — a counter tablet, a family laptop —
/// would otherwise hand one customer's details to the next (the Sep 21 2026 decision, #673).
/// Storage that is blocked or unreadable is treated as empty: remembering is a convenience,
/// never a reason for the form to fail.
/// </para>
/// </summary>
public sealed class RememberedDetailsStore
{
    /// <summary>The <c>localStorage</c> key the details are kept under.</summary>
    public const string StorageKey = "rvs_intake_remembered_details";

    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// Initializes a new instance of <see cref="RememberedDetailsStore"/>.
    /// </summary>
    public RememberedDetailsStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Remembers <paramref name="details"/>, replacing whatever was kept before.</summary>
    public async Task SaveAsync(RememberedDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var json = JsonSerializer.Serialize(details);
        await TryAsync(() => _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json).AsTask());
    }

    /// <summary>
    /// Returns the remembered details, or <c>null</c> when there are none. An unreadable entry,
    /// or one without an email address, is removed on the way out.
    /// </summary>
    public async Task<RememberedDetails?> GetAsync()
    {
        string? json = null;
        await TryAsync(async () => json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey));

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        RememberedDetails? details;
        try
        {
            details = JsonSerializer.Deserialize<RememberedDetails>(json);
        }
        catch (JsonException)
        {
            details = null;
        }

        if (details is null || string.IsNullOrWhiteSpace(details.Email))
        {
            await ForgetAsync();
            return null;
        }

        return details;
    }

    /// <summary>Forgets the remembered details — the customer unticked the box or said "Not you?".</summary>
    public Task ForgetAsync() =>
        TryAsync(() => _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey).AsTask());

    private static async Task TryAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (JSException)
        {
            // localStorage blocked (private window, site data disabled) — behave as if empty.
        }
    }
}
