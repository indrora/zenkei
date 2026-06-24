using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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

    private async void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var vm = (SettingsViewModel)DataContext!;
        var prevLookAndFeel = SettingsService.Current.LookAndFeel;

        vm.Commit();

        bool doRestart = false;
        if (vm.LookAndFeel != prevLookAndFeel)
            doRestart = await AskRestartAsync();

        Close();
        if (doRestart) RestartApp();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private async Task<bool> AskRestartAsync()
    {
        bool confirmed = false;
        var dlg = new Window
        {
            Title = "Restart required",
            Width = 380,
            Height = 130,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var restartBtn = new Button
        {
            Content = "Restart Now",
            IsDefault = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var laterBtn = new Button
        {
            Content = "Later",
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        restartBtn.Click += (_, _) => { confirmed = true; dlg.Close(); };
        laterBtn.Click   += (_, _) => dlg.Close();

        dlg.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "Look & Feel changes take effect after a restart.",
                    TextWrapping = TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { restartBtn, laterBtn },
                },
            },
        };

        await dlg.ShowDialog(this);
        return confirmed;
    }

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
