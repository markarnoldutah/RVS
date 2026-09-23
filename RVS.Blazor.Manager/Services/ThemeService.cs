using Microsoft.JSInterop;
using MudBlazor;
using RVS.UI.Shared.Theme;

namespace RVS.Blazor.Manager.Services;

public enum ThemeMode
{
    Light,
    Dark,
    HighContrast
}

/// <summary>
/// Manages the active MudBlazor theme and persists the user's preference to localStorage.
/// Registered as scoped (effectively singleton in Blazor WASM).
/// Call <see cref="InitializeAsync"/> once during app startup to restore the saved preference.
/// </summary>
public sealed class ThemeService(IJSRuntime js)
{
    private const string ThemeStorageKey = "rvs-manager-theme-preference";

    private ThemeMode _mode = ThemeMode.Light;

    /// <summary>Raised whenever the active theme mode changes.</summary>
    public event Action? OnThemeChanged;

    /// <summary>The current theme mode (read-only). Use <see cref="SetModeAsync"/> to change.</summary>
    public ThemeMode Mode => _mode;

    /// <summary>Whether MudThemeProvider should render the dark palette.</summary>
    public bool IsDarkMode => _mode == ThemeMode.Dark;

    /// <summary>The MudTheme instance to bind to MudThemeProvider.</summary>
    public MudTheme CurrentTheme => _mode switch
    {
        ThemeMode.HighContrast => ManagerTheme.HighContrast,
        _ => ManagerTheme.Theme
    };

    /// <summary>
    /// Loads the persisted theme preference from localStorage. Called once in
    /// <c>Program.cs</c> between <c>builder.Build()</c> and <c>RunAsync()</c> — before the
    /// first render, so a dark-mode user never sees a frame of the light palette (#703).
    /// <c>wwwroot/js/splash-mode.js</c> reads <see cref="ThemeStorageKey"/> for the same
    /// reason, to ground the splash that paints before this class exists.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", ThemeStorageKey);
            _mode = stored switch
            {
                "dark" => ThemeMode.Dark,
                "highcontrast" => ThemeMode.HighContrast,
                _ => ThemeMode.Light
            };
            OnThemeChanged?.Invoke();
        }
        catch (JSException)
        {
            // localStorage unavailable (e.g., pre-render) — leave default.
        }
    }

    /// <summary>
    /// Sets the theme mode, notifies subscribers, and persists to localStorage.
    /// </summary>
    public async Task SetModeAsync(ThemeMode mode)
    {
        if (_mode == mode)
            return;

        _mode = mode;
        OnThemeChanged?.Invoke();

        try
        {
            var value = mode switch
            {
                ThemeMode.Dark => "dark",
                ThemeMode.HighContrast => "highcontrast",
                _ => "light"
            };
            await js.InvokeVoidAsync("localStorage.setItem", ThemeStorageKey, value);
        }
        catch (JSException)
        {
            // Best-effort persistence.
        }
    }
}
