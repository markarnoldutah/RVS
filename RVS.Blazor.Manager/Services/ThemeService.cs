using Microsoft.JSInterop;
using MudBlazor;

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
        ThemeMode.HighContrast => HighContrastTheme,
        _ => IndigoTheme
    };

    /// <summary>
    /// Loads the persisted theme preference from localStorage.
    /// Call once in MainLayout.OnAfterRenderAsync on first render.
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

    private static readonly MudTheme IndigoTheme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#3F51B5",              // Indigo 500
            PrimaryDarken = "#303F9F",        // Indigo 700
            PrimaryLighten = "#C5CAE9",       // Indigo 100
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#00897B",            // Teal 600
            AppbarBackground = "#303F9F",     // Indigo 700
            AppbarText = "#FFFFFF",
            Background = "#FAFAFA",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#3F51B5",              // Indigo 500
            PrimaryDarken = "#303F9F",        // Indigo 700
            PrimaryLighten = "#7986CB",       // Indigo 300
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#26A69A",            // Teal 400
            AppbarBackground = "#1A237E",     // Indigo 900
            AppbarText = "#FFFFFF",
            Background = "#121212",
            Surface = "#1E1E1E",
            DrawerBackground = "#1A1A1A",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#D0D0D0",
            Divider = "#424242",
            ActionDefault = "#FFFFFF",
            ActionDisabled = "#757575"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Roboto", "Helvetica", "Arial", "sans-serif"]
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };

    private static readonly MudTheme HighContrastTheme = new()
    {
        PaletteLight = new PaletteLight
        {
            Background = "#000000",
            BackgroundGray = "#1A1A1A",
            Surface = "#000000",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#FFFFFF",
            Primary = "#FFFF00",
            PrimaryContrastText = "#000000",
            Secondary = "#00FFFF",
            SecondaryContrastText = "#000000",
            Error = "#FF4D4D",
            ErrorContrastText = "#000000",
            Success = "#00FF00",
            SuccessContrastText = "#000000",
            Divider = "#FFFFFF",
            ActionDefault = "#FFFFFF",
            ActionDisabled = "#666666",
            AppbarBackground = "#000000",
            AppbarText = "#FFFFFF",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Roboto", "Helvetica", "Arial", "sans-serif"]
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };
}
