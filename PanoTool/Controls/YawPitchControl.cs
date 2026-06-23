using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zenkei.Controls;

/// <summary>
/// Compact control showing paired Yaw and Pitch NumericUpDowns with degree suffixes.
/// Used by <see cref="Zenkei.PropertyGrid.YawPitchCellFactory"/> for position properties.
/// </summary>
public sealed class YawPitchControl : UserControl
{
    private bool _updating;

    internal NumericUpDown YawNud   { get; }
    internal NumericUpDown PitchNud { get; }

    /// True while <see cref="SetYawPitch"/> is writing into the NUDs — factory
    /// ignores ValueChanged events during this window to avoid spurious writes.
    public bool IsUpdating => _updating;

    public YawPitchControl()
    {
        YawNud   = MakeDegreeNud(-180, 180);
        PitchNud = MakeDegreeNud(0, 180);

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing     = 4,
            Children =
            {
                MakeLabel("Y"),
                YawNud,
                MakeLabel("P", leftMargin: 4),
                PitchNud,
            }
        };
    }

    public void SetYawPitch(double yaw, double pitch)
    {
        _updating = true;
        YawNud.Value   = (decimal)yaw;
        PitchNud.Value = (decimal)pitch;
        _updating = false;
    }

    private static NumericUpDown MakeDegreeNud(decimal min, decimal max)
    {
        var nud = new NumericUpDown
        {
            Minimum      = min,
            Maximum      = max,
            Increment    = 1,
            FormatString = "F0",
            AllowSpin    = true,
            MinWidth     = 90,
        };

        nud.SetValue(TextBox.InnerRightContentProperty, new TextBlock
        {
            Text              = "°",
            Padding           = new Thickness(0, 0, 4, 0),
            Foreground        = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return nud;
    }

    private static TextBlock MakeLabel(string text, double leftMargin = 0) => new()
    {
        Text              = text,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity           = 0.65,
        FontSize          = 11,
        Margin            = new Thickness(leftMargin, 0, 0, 0),
    };
}
