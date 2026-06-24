using Avalonia.Controls;
using Avalonia.Interactivity;
using Zenkei.ViewModels;

namespace Zenkei.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        ((SettingsViewModel)DataContext!).Commit();
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}
