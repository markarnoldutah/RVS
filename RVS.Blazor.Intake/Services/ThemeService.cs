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
            Primary = "#3F51B5",
            Secondary = "#00897B",
            AppbarBackground = "#303F9F",
            AppbarText = "#FFFFFF",
            Background = "#FAFAFA",
            Surface = "#FFFFFF",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7986CB",
            Secondary = "#4DB6AC",
            AppbarBackground = "#1A237E",
            AppbarText = "#FFFFFF",
            Background = "#121212",
            Surface = "#1E1E1E",
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
