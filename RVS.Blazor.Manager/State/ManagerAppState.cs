using Microsoft.JSInterop;
using RVS.UI.Shared.Services;

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
    private string? _selectedDealershipId;
    private readonly SemaphoreSlim _dealershipResolveLock = new(1, 1);

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

    /// <summary>
    /// Dealership identifier for the authenticated user's tenant, cached after first resolve.
    /// V1 assumes a single dealership per tenant; if multi-dealership support lands later,
    /// this becomes the currently-selected dealership.
    /// </summary>
    public string? SelectedDealershipId => _selectedDealershipId;

    /// <summary>
    /// Resolves and caches <see cref="SelectedDealershipId"/> via the lookup API on first call.
    /// Subsequent calls return the cached value. Safe to call concurrently.
    /// </summary>
    /// <param name="lookupClient">Lookup client for the authenticated tenant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved dealership id, or <c>null</c> if the tenant has no dealerships.</returns>
    public async Task<string?> EnsureDealershipResolvedAsync(LookupApiClient lookupClient, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lookupClient);

        if (_selectedDealershipId is not null)
            return _selectedDealershipId;

        await _dealershipResolveLock.WaitAsync(ct);
        try
        {
            if (_selectedDealershipId is not null)
                return _selectedDealershipId;

            var dealerships = await lookupClient.GetDealershipsAsync(ct);
            _selectedDealershipId = dealerships.FirstOrDefault()?.Id;
            return _selectedDealershipId;
        }
        finally
        {
            _dealershipResolveLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        OnLocationChanged = null;
        _dealershipResolveLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
