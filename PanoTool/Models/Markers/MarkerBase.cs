using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zenkei.Models.Markers;

public abstract class MarkerBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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
