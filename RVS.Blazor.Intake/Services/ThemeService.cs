using MudBlazor;
using RVS.UI.Shared.Theme;

namespace RVS.Blazor.Intake.Services;

public enum ThemeMode
{
    Light,
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

    public bool IsHighContrast => _mode == ThemeMode.HighContrast;

    public MudTheme CurrentTheme => _mode switch
    {
        ThemeMode.HighContrast => IntakeTheme.HighContrast,
        _ => IntakeTheme.Theme
    };

    public void Toggle()
    {
        Mode = _mode == ThemeMode.Light ? ThemeMode.HighContrast : ThemeMode.Light;
    }
}
