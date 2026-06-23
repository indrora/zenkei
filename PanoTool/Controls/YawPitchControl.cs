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

    private YawPitch _value;
    public YawPitch Value
    {
        get => _value;
        set
        {
            if (_value.Equals(value)) return;
            _value = value;
            notifyChanged();
        }
    }
    
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

        YawNud.HorizontalAlignment = HorizontalAlignment.Stretch;
        PitchNud.HorizontalAlignment = HorizontalAlignment.Stretch;


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
                        MakeLabel("Yaw", leftMargin: 4),
                        YawNud
                    }
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing     = 4,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Children    =
                    {
                        MakeLabel("Pitch", leftMargin: 4),
                        PitchNud
                    }
                }
            }
        };
    }

    public void SetYawPitch(double yaw, double pitch)
    {
        var newValue = new YawPitch(yaw, pitch);
        if (_value.Equals(newValue)) return;
        _value = newValue;
        notifyChanged(raiseEvent: false);
    }

    private void notifyChanged(bool raiseEvent = true)
    {
        
        _updating      = true;
        YawNud.Value   = (decimal)_value.Yaw;
        PitchNud.Value = (decimal)_value.Pitch;
        // MarkerLocationControl uses radians
        _map.Yaw   = _value.Yaw   * Math.PI / 180.0;
        _map.Pitch = _value.Pitch * Math.PI / 180.0;
        _updating  = false;
        if (raiseEvent)
            PositionChanged?.Invoke(this, _value);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnNudChanged()
    {
        if (_updating) return;
        if (YawNud.Value is not { } y || PitchNud.Value is not { } p) return;

        _value = new YawPitch((double)y, (double)p);
        notifyChanged();
    }

    private void OnMapChanged()
    {
        _value = new YawPitch(_map.Yaw * 180.0 / Math.PI, _map.Pitch * 180.0 / Math.PI);
        notifyChanged();
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
