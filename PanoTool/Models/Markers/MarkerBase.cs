using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zenkei.Models.Markers;

public abstract class MarkerBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Fires PropertyChanged for Coords and Position so the canvas and
    /// PropertyGrid both refresh after direct Coords[] mutations (e.g. drag).
    /// </summary>
    internal void NotifyCoordsChanged()
    {
        OnPropertyChanged(nameof(Coords));
        OnPropertyChanged(nameof(Position));
    }

    [Browsable(false)]
    public abstract string Type { get; }

    // Raw radian array — serialised to YAML; Position exposes it in degrees.
    [Browsable(false)]
    public YawPitch? Coords { get; set; }

    // ── Position as combined Yaw/Pitch (degrees) ───────────────────────────────
    // Rendered by YawPitchCellFactory + YawPitchControl in the PropertyGrid.

    [Category("Position"), Description("Yaw and pitch in degrees")]
    public YawPitch Position
    {
        get => Coords.HasValue
            ? new YawPitch(Coords.Value.Yaw   * 180.0 / Math.PI,
                           Coords.Value.Pitch * 180.0 / Math.PI)
            : new YawPitch(0.0, 0.0);
        set
        {
            Coords = new YawPitch(
                value.Yaw   * Math.PI / 180.0,
                value.Pitch * Math.PI / 180.0);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Coords));
        }
    }

    private string? _marker;
    [Category("Appearance"), Description("Named icon override from the tour's icon library")]
    public string? Marker { get => _marker; set { _marker = value; OnPropertyChanged(); } }
}
