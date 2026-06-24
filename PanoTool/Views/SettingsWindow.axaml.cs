using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Zenkei.Services;
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
        var vm = (SettingsViewModel)DataContext!;
        var prevLookAndFeel = SettingsService.Current.LookAndFeel;

        vm.Commit();
        Close();

        // Look & Feel is a full style-system replacement; restart for a clean apply.
        if (vm.LookAndFeel != prevLookAndFeel)
            RestartApp();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private static void RestartApp()
    {
        var exe = Environment.ProcessPath;
        if (exe != null)
        {
            // Re-launch with the same arguments (e.g. a tour file from the command line).
            var psi = new ProcessStartInfo(exe);
            foreach (var a in Environment.GetCommandLineArgs().Skip(1))
                psi.ArgumentList.Add(a);
            Process.Start(psi);
        }

        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.Shutdown();
    }
}
