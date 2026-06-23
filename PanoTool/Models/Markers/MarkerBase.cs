using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zenkei.Models.Markers;

public abstract class MarkerBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Fires PropertyChanged for Coords so that the canvas can redraw when the
    /// marker editor panel writes new coordinates into the array directly.
    /// </summary>
    internal void NotifyCoordsChanged() => OnPropertyChanged(nameof(Coords));

    // Type and Coords are managed by the marker editor header, not the PropertyGrid
    [Browsable(false)]
    public abstract string Type { get; }

    // [yaw_rad, pitch_from_top_rad] — edited via NumericUpDowns above the PropertyGrid
    [Browsable(false)]
    public double[]? Coords { get; set; }

    private string? _marker;
    [Category("Appearance"), Description("Named icon override from the tour's icon library")]
    public string? Marker { get => _marker; set { _marker = value; OnPropertyChanged(); } }
}
