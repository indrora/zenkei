using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;
using Devolutions.AvaloniaTheme.DevExpress;
using Devolutions.AvaloniaTheme.MacOS;
using Zenkei.Controls;

namespace Zenkei.Services;

/// <summary>
/// Manages user preferences: loads from and saves to %LocalAppData%\Zenkei\settings.json.
/// Call Load() before creating MainWindow, then Apply() to push settings to the running app.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Zenkei", "settings.json");

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { Current = new(); }
    }

    public static void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(SettingsPath,
            JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Apply all current settings to the running Avalonia application.</summary>
    public static void Apply()
    {
        ApplyThemeVariant();
        ApplyLookAndFeel();
        BitmapCache.MaxEditorWidth = Current.BitmapCacheMaxWidth;
    }

    public static void ApplyThemeVariant()
    {
        if (Application.Current == null) return;
        Application.Current.RequestedThemeVariant = Current.ThemeVariant switch
        {
            ThemeVariantSetting.Light  => ThemeVariant.Light,
            ThemeVariantSetting.System => ThemeVariant.Default,
            _                          => ThemeVariant.Dark,
        };
    }

    /// <summary>
    /// Switches the theme package at runtime by managing Application.Styles.
    /// DockFluentTheme stays in App.axaml always — dock chrome always looks Fluent.
    /// Cupertino (MacOS) and DevExpress layers are added/removed dynamically on top.
    /// </summary>
    /// <summary>
    /// Applies Look &amp; Feel at startup only; Look &amp; Feel changes at runtime
    /// require a restart (handled by SettingsWindow).
    /// </summary>
    public static void ApplyLookAndFeel()
    {
        if (Application.Current == null) return;
        var styles = Application.Current.Styles;

        IStyle? theme = Current.LookAndFeel switch
        {
            LookAndFeel.Cupertino  => InitTheme(new DevolutionsMacOsTheme()),
            LookAndFeel.DevExpress => InitTheme(new DevolutionsDevExpressTheme()),
            _                      => null,
        };

        if (theme != null)
            styles.Add(theme);
    }

    // Devolutions themes implement ISupportInitialize; their embedded AXAML styles are
    // only added to the Styles collection inside EndInit() — calling new T() alone
    // produces an empty Styles object and nothing is applied at runtime.
    private static T InitTheme<T>(T theme) where T : ISupportInitialize, IStyle
    {
        theme.BeginInit();
        theme.EndInit();
        return theme;
    }
}
