using Microsoft.JSInterop;

namespace RVS.Blazor.Manager.State;

/// <summary>
/// Centrally managed application state for the Manager app.
/// Singleton-scoped in WASM — shared across all pages and components.
/// Persists selected location to localStorage so it survives page reloads.
/// </summary>
public sealed class ManagerAppState(IJSRuntime js) : IAsyncDisposable
{
    private const string LocationStorageKey = "rvs-manager-selected-location";

    private string? _selectedLocation;

    /// <summary>
    /// The currently selected location ID, shared across all pages.
    /// </summary>
    public string? SelectedLocation => _selectedLocation;

    /// <summary>
    /// Raised whenever the selected location changes. Pages subscribe to reload data.
    /// </summary>
    public event Func<string?, Task>? OnLocationChanged;

    /// <summary>
    /// Loads the persisted location from localStorage. Call once during app startup (e.g., in MainLayout).
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _selectedLocation = await js.InvokeAsync<string?>("localStorage.getItem", LocationStorageKey);
        }
        catch (JSException)
        {
            // localStorage unavailable (e.g., pre-render) — leave null.
        }
    }

    /// <summary>
    /// Sets the selected location, persists to localStorage, and notifies subscribers.
    /// </summary>
    public async Task SetLocationAsync(string? locationId)
    {
        if (string.Equals(_selectedLocation, locationId, StringComparison.Ordinal))
            return;

        _selectedLocation = locationId;

        try
        {
            if (locationId is not null)
            {
                await js.InvokeVoidAsync("localStorage.setItem", LocationStorageKey, locationId);
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", LocationStorageKey);
            }
        }
        catch (JSException)
        {
            // Best-effort persistence.
        }

        if (OnLocationChanged is not null)
        {
            await OnLocationChanged.Invoke(locationId);
        }
    }

    public ValueTask DisposeAsync()
    {
        OnLocationChanged = null;
        return ValueTask.CompletedTask;
    }
}
