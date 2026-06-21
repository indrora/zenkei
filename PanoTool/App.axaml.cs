using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Markup.Xaml;
using Zenkei.ViewModels;
using Zenkei.Views;

namespace Zenkei;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };

            var args = desktop.Args ?? [];
            string? yamlPath = null;
            string? screenshotPath = null;

            // Parse args: [yamlFile] [--screenshot outPath]
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--screenshot" && i + 1 < args.Length)
                    screenshotPath = args[++i];
                else if (File.Exists(args[i]))
                    yamlPath = args[i];
            }

            if (yamlPath != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    vm.OpenDocumentFromPath(yamlPath));
            }

            if (screenshotPath != null)
            {
                // After layout settles, render the window to a PNG and exit
                var win = desktop.MainWindow!;
                var snapPath = screenshotPath;
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    // Give layout and image decode time to finish
                    await Task.Delay(2000);
                    var px = new PixelSize((int)win.Bounds.Width, (int)win.Bounds.Height);
                    using var rtb = new RenderTargetBitmap(px, new Vector(96, 96));
                    rtb.Render(win);
                    rtb.Save(snapPath);
                    desktop.Shutdown();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}