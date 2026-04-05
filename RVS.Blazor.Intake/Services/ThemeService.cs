using MudBlazor;

namespace RVS.Blazor.Intake.Services;

public enum ThemeMode
{
    Light,
    Dark,
    HighContrast
}

public sealed class ThemeService
{
    private ThemeMode _mode = ThemeMode.Light;

    public event Action? OnThemeChanged;

    public ThemeMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
                return;

            _mode = value;
            OnThemeChanged?.Invoke();
        }
    }

    public MudTheme CurrentTheme => _mode switch
    {
        ThemeMode.HighContrast => HighContrastTheme,
        _ => DefaultTheme
    };

    public bool IsDarkMode => _mode == ThemeMode.Dark;

    private static readonly MudTheme DefaultTheme = new()
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
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#9FA8DA",              // Indigo 200
            PrimaryDarken = "#7986CB",        // Indigo 300
            PrimaryLighten = "#E8EAF6",       // Indigo 50
            PrimaryContrastText = "#000000",
            Secondary = "#4DB6AC",            // Teal 300
            AppbarBackground = "#1A237E",     // Indigo 900
            AppbarText = "#FFFFFF",
            Background = "#121212",
            Surface = "#1E1E1E",
            DrawerBackground = "#1E1E1E",
            DrawerText = "#FFFFFFB3",
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
