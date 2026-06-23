using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zenkei.Models;

namespace Zenkei.Controls;

/// <summary>
/// Compact control combining a <see cref="MarkerLocationControl"/> equirectangular
/// mini-map with paired Yaw/Pitch NumericUpDowns.  Dragging on the map or typing
/// in the NUDs updates both views in sync.
/// Used by <see cref="Zenkei.PropertyGrid.YawPitchCellFactory"/> for position properties.
/// </summary>
public sealed class YawPitchControl : UserControl
{
    private bool _updating;
    private readonly MarkerLocationControl _map;

    internal NumericUpDown YawNud   { get; }
    internal NumericUpDown PitchNud { get; }

    /// <summary>
    /// Raised whenever the user changes the position — either by dragging the map
    /// or by editing a NUD.  Not raised during programmatic <see cref="SetYawPitch"/> calls.
    /// </summary>
    public event EventHandler<YawPitch>? PositionChanged;

    public YawPitchControl()
    {
        YawNud   = MakeDegreeNud(-180, 180);
        PitchNud = MakeDegreeNud(0, 180);
        _map     = new MarkerLocationControl();

        YawNud.ValueChanged   += (_, _) => OnNudChanged();
        PitchNud.ValueChanged += (_, _) => OnNudChanged();

        // Sync map drag → NUDs
        _map.PropertyChanged += (_, e) =>
        {
            if (_updating) return;
            if (e.Property == MarkerLocationControl.YawProperty ||
                e.Property == MarkerLocationControl.PitchProperty)
                OnMapChanged();
        };

        Content = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                _map,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing     = 4,
                    Children    =
                    {
                        MakeLabel("Y"),
                        YawNud,
                        MakeLabel("P", leftMargin: 4),
                        PitchNud,
                    }
                }
            }
        };
    }

    /// <summary>
    /// Sets both the map and the NUDs without raising <see cref="PositionChanged"/>.
    /// Called by the cell factory to display the current model value.
    /// </summary>
    public void SetYawPitch(double yaw, double pitch)
    {
        _updating      = true;
        YawNud.Value   = (decimal)yaw;
        PitchNud.Value = (decimal)pitch;
        // MarkerLocationControl uses radians
        _map.Yaw   = yaw   * Math.PI / 180.0;
        _map.Pitch = pitch * Math.PI / 180.0;
        _updating  = false;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnNudChanged()
    {
        if (_updating) return;
        if (YawNud.Value is not { } y || PitchNud.Value is not { } p) return;

        // Sync map (without triggering OnMapChanged again)
        _updating  = true;
        _map.Yaw   = (double)y * Math.PI / 180.0;
        _map.Pitch = (double)p * Math.PI / 180.0;
        _updating  = false;

        PositionChanged?.Invoke(this, new YawPitch((double)y, (double)p));
    }

    private void OnMapChanged()
    {
        // Sync NUDs (without triggering OnNudChanged again)
        _updating      = true;
        YawNud.Value   = (decimal)(_map.Yaw   * 180.0 / Math.PI);
        PitchNud.Value = (decimal)(_map.Pitch * 180.0 / Math.PI);
        _updating      = false;

        PositionChanged?.Invoke(this, new YawPitch(
            _map.Yaw   * 180.0 / Math.PI,
            _map.Pitch * 180.0 / Math.PI));
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
