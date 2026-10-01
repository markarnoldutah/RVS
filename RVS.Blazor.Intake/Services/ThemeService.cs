using Microsoft.JSInterop;
using MudBlazor;
using RVS.UI.Shared.Theme;

namespace RVS.Blazor.Intake.Services;

public enum ThemeMode
{
    Light,
    HighContrast
}

/// <summary>
/// Manages the active MudBlazor theme and persists the customer's choice to localStorage, so
/// high contrast survives a reload and a later visit on the same device (issue #758).
/// Registered as scoped (effectively singleton in Blazor WASM).
/// Call <see cref="InitializeAsync"/> once during app startup to restore the saved preference.
/// </summary>
public sealed class ThemeService
{
    /// <summary>The localStorage key holding the preference.</summary>
    public const string ThemeStorageKey = "rvs-intake-theme-preference";

    private const string HighContrastValue = "highcontrast";
    private const string LightValue = "light";

    private readonly IJSRuntime _js;
    private ThemeMode _mode = ThemeMode.Light;

    public ThemeService(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        _js = js;
    }

    public event Action? OnThemeChanged;

    /// <summary>The current theme mode (read-only). Use <see cref="ToggleAsync"/> to change.</summary>
    public ThemeMode Mode => _mode;

    public bool IsHighContrast => _mode == ThemeMode.HighContrast;

    public MudTheme CurrentTheme => _mode switch
    {
        ThemeMode.HighContrast => IntakeTheme.HighContrast,
        _ => IntakeTheme.Theme
    };

    /// <summary>
    /// Loads the persisted preference. Called once in <c>Program.cs</c> between
    /// <c>builder.Build()</c> and <c>RunAsync()</c> — before the first render, so a high-contrast
    /// customer never sees a frame of the brand palette.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", ThemeStorageKey);
            _mode = stored == HighContrastValue ? ThemeMode.HighContrast : ThemeMode.Light;
            OnThemeChanged?.Invoke();
        }
        catch (JSException)
        {
            // localStorage unavailable (private window, site data blocked) — leave default.
        }
    }

    /// <summary>
    /// Switches between light and high contrast, notifies subscribers, and persists the choice.
    /// </summary>
    public async Task ToggleAsync()
    {
        _mode = _mode == ThemeMode.Light ? ThemeMode.HighContrast : ThemeMode.Light;
        OnThemeChanged?.Invoke();

        try
        {
            var value = _mode == ThemeMode.HighContrast ? HighContrastValue : LightValue;
            await _js.InvokeVoidAsync("localStorage.setItem", ThemeStorageKey, value);
        }
        catch (JSException)
        {
            // Best-effort persistence: the theme still switches for this visit.
        }
    }
}
