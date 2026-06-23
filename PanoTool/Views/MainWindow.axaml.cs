using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Zenkei.ViewModels;

namespace Zenkei.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        vm.FilePickerDelegate = async (title, filters) =>
        {
            var opts = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = true,
                FileTypeFilter = FiltersFrom(filters)
            };
            var files = await StorageProvider.OpenFilePickerAsync(opts);
            return files.Select(f => f.TryGetLocalPath() ?? "").Where(p => !string.IsNullOrEmpty(p));
        };

        vm.OpenFilePickerDelegate = async (title, filters) =>
        {
            var opts = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = FiltersFrom(filters)
            };
            var files = await StorageProvider.OpenFilePickerAsync(opts);
            return files.Select(f => f.TryGetLocalPath() ?? "").FirstOrDefault(p => !string.IsNullOrEmpty(p));
        };

        vm.SaveFilePickerDelegate = async (title, _) =>
        {
            var opts = new FilePickerSaveOptions
            {
                Title = title,
                DefaultExtension = "yml",
                FileTypeChoices = [new FilePickerFileType("YAML Tour File") { Patterns = ["*.yml", "*.yaml"] }]
            };
            var file = await StorageProvider.SaveFilePickerAsync(opts);
            return file?.TryGetLocalPath();
        };

        vm.ShowErrorDelegate = async (title, message) =>
        {
            var dlg = new Window
            {
                Title = title,
                Width = 400, Height = 150,
                Content = new TextBlock
                {
                    Text = message,
                    Margin = new Avalonia.Thickness(16),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                }
            };
            await dlg.ShowDialog(this);
        };

        vm.InputDialogDelegate = async (title, prompt, defaultValue) =>
            await new InputDialog(title, prompt, defaultValue).ShowAsync(this);
    }

    private static FilePickerFileType[] FiltersFrom(string[] patterns)
    {
        if (patterns.Length == 0) return [];
        return [new FilePickerFileType("Files") { Patterns = patterns }];
    }
}