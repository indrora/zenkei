using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Zenkei.Models;

namespace Zenkei.Controls;

/// <summary>
/// Compact control combining a <see cref="MarkerLocationControl"/> equirectangular
/// mini-map with paired Yaw/Pitch NumericUpDowns.  Dragging on the map or typing
/// in the NUDs updates both views in sync.
/// Used by <see cref="Zenkei.PropertyGrid.YawPitchCellFactory"/> for position properties.
/// </summary>
public partial class YawPitchControl : UserControl
{
    private bool _updating;
    private MarkerLocationControl _map = null!;
    private NumericUpDown _yawNud   = null!;
    private NumericUpDown _pitchNud = null!;

    private YawPitch _value;
    public YawPitch Value
    {
        get => _value;
        set
        {
            if (_value.Equals(value)) return;
            _value = value;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Raised whenever the user changes the position — either by dragging the map
    /// or by editing a NUD.  Not raised during programmatic <see cref="SetYawPitch"/> calls.
    /// </summary>
    public event EventHandler<YawPitch>? PositionChanged;

    public YawPitchControl()
    {
        InitializeComponent();

        _map     = this.FindControl<MarkerLocationControl>("Map")!;
        _yawNud  = this.FindControl<NumericUpDown>("YawNud")!;
        _pitchNud = this.FindControl<NumericUpDown>("PitchNud")!;

        // InnerRightContentProperty is a TextBox styled property not expressible in
        // AXAML on NumericUpDown — must be set in code.
        AddDegreeSuffix(_yawNud);
        AddDegreeSuffix(_pitchNud);

        _yawNud.ValueChanged  += (_, _) => OnNudChanged();
        _pitchNud.ValueChanged += (_, _) => OnNudChanged();

        _map.PropertyChanged += (_, e) =>
        {
            if (_updating) return;
            if (e.Property == MarkerLocationControl.YawProperty ||
                e.Property == MarkerLocationControl.PitchProperty)
                OnMapChanged();
        };
    }

    public void SetYawPitch(double yaw, double pitch)
    {
        var newValue = new YawPitch(yaw, pitch);
        if (_value.Equals(newValue)) return;
        _value = newValue;
        NotifyChanged(raiseEvent: false);
    }

    private void NotifyChanged(bool raiseEvent = true)
    {
        _updating       = true;
        _yawNud.Value   = (decimal)_value.Yaw;
        _pitchNud.Value = (decimal)_value.Pitch;
        // MarkerLocationControl uses radians
        _map.Yaw   = _value.Yaw   * Math.PI / 180.0;
        _map.Pitch = _value.Pitch * Math.PI / 180.0;
        _updating  = false;
        if (raiseEvent)
            PositionChanged?.Invoke(this, _value);
    }

    private void OnNudChanged()
    {
        if (_updating) return;
        if (_yawNud.Value is not { } y || _pitchNud.Value is not { } p) return;

        _value = new YawPitch((double)y, (double)p);
        NotifyChanged();
    }

    private void OnMapChanged()
    {
        _value = new YawPitch(_map.Yaw * 180.0 / Math.PI, _map.Pitch * 180.0 / Math.PI);
        NotifyChanged();
    }

    private static void AddDegreeSuffix(NumericUpDown nud) =>
        nud.SetValue(TextBox.InnerRightContentProperty, new TextBlock
        {
            Text              = "°",
            Padding           = new Thickness(0, 0, 4, 0),
            Foreground        = Brushes.Gray,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        });
}
