using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Zenkei.Views;

/// <summary>
/// Minimal single-line text-input dialog.
/// Usage: var result = await new InputDialog(title, prompt, defaultValue).ShowAsync(ownerWindow);
/// Returns null if the user cancels.
/// </summary>
public sealed class InputDialog : Window
{
    private readonly TextBox _textBox;
    private string? _result;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        Title       = title;
        Width       = 380;
        CanResize   = false;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _textBox = new TextBox
        {
            Text   = defaultValue,
            Margin = new Thickness(12, 4, 12, 8)
        };

        var ok = new Button
        {
            Content = "OK",
            IsDefault = true,
            MinWidth = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 8, 0)
        };
        ok.Click += (_, _) => { _result = _textBox.Text; Close(); };

        var cancel = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 80
        };
        cancel.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 12),
            Children =
            {
                new TextBlock
                {
                    Text   = prompt,
                    Margin = new Thickness(12, 0, 12, 6),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                _textBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(12, 0, 12, 0),
                    Spacing = 8,
                    Children = { ok, cancel }
                }
            }
        };
    }

    public async Task<string?> ShowAsync(Window owner)
    {
        await ShowDialog(owner);
        return _result;
    }
}
