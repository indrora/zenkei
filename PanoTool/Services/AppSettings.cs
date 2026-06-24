namespace Zenkei.Services;

public enum ThemeVariantSetting { Dark, Light, System }

/// <summary>
/// Look-and-feel theme package.
/// Fluent = Avalonia Fluent Design (default).
/// Cupertino = Devolutions macOS-style theme (Devolutions.AvaloniaTheme.MacOS).
/// DevExpress = Devolutions DevExpress-style theme (Devolutions.AvaloniaTheme.DevExpress).
/// </summary>
public enum LookAndFeel { Fluent, Cupertino, DevExpress }

public class AppSettings
{
    public ThemeVariantSetting ThemeVariant  { get; set; } = ThemeVariantSetting.Dark;
    public LookAndFeel         LookAndFeel   { get; set; } = LookAndFeel.Fluent;
    /// <summary>Max pixel width for editor thumbnail decode (BitmapCache).</summary>
    public int BitmapCacheMaxWidth           { get; set; } = 2048;
}
