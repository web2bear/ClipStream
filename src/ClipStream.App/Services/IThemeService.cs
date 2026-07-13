using ClipStream.App.Themes;

namespace ClipStream.App.Services;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    bool IsDarkTheme { get; }

    void ApplyTheme(AppTheme theme);

    void ToggleTheme();

    void LoadSavedTheme();

    event EventHandler? ThemeChanged;
}
