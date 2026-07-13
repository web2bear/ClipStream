using System.IO;
using System.Text.Json;
using System.Windows;
using ClipStream.App.Themes;

namespace ClipStream.App.Services;

public sealed class ThemeService : IThemeService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClipStream",
        "settings.json");

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public bool IsDarkTheme => CurrentTheme == AppTheme.Dark;

    public event EventHandler? ThemeChanged;

    public void ApplyTheme(AppTheme theme)
    {
        if (CurrentTheme == theme && HasThemeDictionary())
        {
            return;
        }

        var app = System.Windows.Application.Current;
        var merged = app.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Colors.", StringComparison.OrdinalIgnoreCase) == true);

        if (existing is not null)
        {
            merged.Remove(existing);
        }

        merged.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"Themes/Colors.{theme}.xaml", UriKind.Relative)
        });

        CurrentTheme = theme;
        SaveSettings();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleTheme() => ApplyTheme(IsDarkTheme ? AppTheme.Light : AppTheme.Dark);

    public void LoadSavedTheme()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                ApplyTheme(AppTheme.Dark);
                return;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            var theme = settings?.Theme == nameof(AppTheme.Light) ? AppTheme.Light : AppTheme.Dark;
            ApplyTheme(theme);
        }
        catch
        {
            ApplyTheme(AppTheme.Dark);
        }
    }

    private static bool HasThemeDictionary() =>
        System.Windows.Application.Current.Resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source?.OriginalString.Contains("Colors.", StringComparison.OrdinalIgnoreCase) == true);

    private void SaveSettings()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);

            var settings = new AppSettings
            {
                Theme = CurrentTheme.ToString()
            };

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // Preference persistence is best-effort.
        }
    }

    private sealed class AppSettings
    {
        public string Theme { get; set; } = nameof(AppTheme.Dark);
    }
}
