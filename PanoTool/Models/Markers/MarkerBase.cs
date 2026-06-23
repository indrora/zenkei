using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Zenkei.PropertyGrid;

namespace Zenkei.Models.Markers;

public abstract class MarkerBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Fires PropertyChanged for Coords, Yaw, and Pitch so the canvas and
    /// PropertyGrid both refresh after direct Coords[] mutations.
    /// </summary>
    internal void NotifyCoordsChanged()
    {
        OnPropertyChanged(nameof(Coords));
        OnPropertyChanged(nameof(Yaw));
        OnPropertyChanged(nameof(Pitch));
    }

    [Browsable(false)]
    public abstract string Type { get; }

    // Raw radian array — not shown in PropertyGrid; Yaw/Pitch expose it as degrees
    [Browsable(false)]
    public double[]? Coords { get; set; }

    // Position in degrees, computed from Coords[].  Shown in PropertyGrid via
    // DegreeCellFactory and usable for drag-sync via NotifyCoordsChanged.
    [Degrees]
    [Range(-180.0, 180.0)]
    [Category("Position"), Description("Yaw in degrees (-180 … 180)")]
    public double Yaw
    {
        get => (Coords?[0] ?? 0) * 180.0 / Math.PI;
        set
        {
            if (Coords is { Length: >= 2 })
            {
                Coords[0] = value * Math.PI / 180.0;
                OnPropertyChanged();
            }
        }
    }

    [Degrees]
    [Range(0.0, 180.0)]
    [Category("Position"), Description("Pitch in degrees (0 = top, 180 = bottom)")]
    public double Pitch
    {
        get => (Coords?[1] ?? Math.PI / 2) * 180.0 / Math.PI;
        set
        {
            if (Coords is { Length: >= 2 })
            {
                Coords[1] = value * Math.PI / 180.0;
                OnPropertyChanged();
            }
        }
    }

    private string? _marker;
    [Category("Appearance"), Description("Named icon override from the tour's icon library")]
    public string? Marker { get => _marker; set { _marker = value; OnPropertyChanged(); } }
}
