using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Zenkei.Services;

namespace Zenkei.ViewModels;

/// <summary>
/// Holds an editable working copy of AppSettings for the Preferences dialog.
/// Writes back to SettingsService.Current and applies live only on Commit().
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private ThemeVariantSetting _themeVariant;
    [ObservableProperty] private LookAndFeel _lookAndFeel;
    // NumericUpDown.Value is decimal? — keep as decimal here, convert to int on commit.
    [ObservableProperty] private decimal _bitmapCacheMaxWidth;

    public IEnumerable<ThemeVariantSetting> ThemeVariants => Enum.GetValues<ThemeVariantSetting>();
    public IEnumerable<LookAndFeel>         LookAndFeels  => Enum.GetValues<LookAndFeel>();

    public SettingsViewModel()
    {
        var s = SettingsService.Current;
        _themeVariant       = s.ThemeVariant;
        _lookAndFeel        = s.LookAndFeel;
        _bitmapCacheMaxWidth = s.BitmapCacheMaxWidth;
    }

    /// <summary>Persist the working copy and apply to the running application.</summary>
    public void Commit()
    {
        SettingsService.Current.ThemeVariant      = ThemeVariant;
        SettingsService.Current.LookAndFeel       = LookAndFeel;
        SettingsService.Current.BitmapCacheMaxWidth = (int)BitmapCacheMaxWidth;
        SettingsService.Save();
        SettingsService.Apply();
    }
}
