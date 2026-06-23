using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zenkei.Models.Markers;
using Zenkei.PropertyGrid; // [Degrees], [Multiline]

namespace Zenkei.Models;

public class Scene : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Scene key in TourDocument.Scenes — not stored in YAML body.
    // Shown read-only in Properties; rename via right-click.
    private string _id = "";
    [Category("Scene"), ReadOnly(true), Description("Scene ID — right-click to rename")]
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    private string _image = "";
    [Category("Scene"), Description("Panorama image path — edit directly or right-click to browse")]
    public string Image { get => _image; set { _image = value; OnPropertyChanged(); } }

    private string _title = "";
    [Category("Scene"), Description("Display name for this scene")]
    public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }

    private string? _description;
    [Multiline]
    [Category("Scene"), Description("Optional description text")]
    public string? Description { get => _description; set { _description = value; OnPropertyChanged(); } }

    // Runtime-only — never written to YAML
    [Browsable(false)]
    public string? BaseDirectory { get; set; }

    /// <summary>
    /// Resolves relative Image paths against the tour file's folder.
    /// </summary>
    [Browsable(false)]
    public string ResolvedImagePath =>
        string.IsNullOrEmpty(Image) || Path.IsPathRooted(Image) || string.IsNullOrEmpty(BaseDirectory)
            ? Image
            : Path.Combine(BaseDirectory, Image);

    // Radians — read/written by the canvas and serializer; degrees via InitialPosition below.
    [Browsable(false)]
    public YawPitch Initial
    {
        get => InitialMarker.Coords ?? new YawPitch(0, 0);
        set
        {
            InitialMarker.Coords = value;
            InitialMarker.NotifyCoordsChanged();
            OnPropertyChanged();
            // Keep the browsable degree-view in sync when the canvas updates us directly.
            OnPropertyChanged(nameof(InitialPosition));
        }
    }

    /// <summary>
    /// Initial viewpoint in degrees — shown in the Properties panel and rendered by
    /// YawPitchCellFactory. Backed by <see cref="Initial"/> which stores radians.
    /// </summary>
    [Category("View"), Description("Initial camera direction (degrees)")]
    public YawPitch InitialPosition
    {
        get => new(Initial.Yaw   * 180.0 / Math.PI,
                   Initial.Pitch * 180.0 / Math.PI);
        set
        {
            Initial = new YawPitch(value.Yaw   * Math.PI / 180.0,
                                   value.Pitch * Math.PI / 180.0);
            OnPropertyChanged();
        }
    }

    public InitialMarker InitialMarker
    {
        get
        {
            var existing = Markers.OfType<InitialMarker>().FirstOrDefault();
            if (existing == null)
            {
                existing = new InitialMarker();
                Markers.Add(existing);
            }
            return existing;
        } 
        set
        {
            var existing = Markers.OfType<InitialMarker>().FirstOrDefault();
            if (existing != null)
                Markers.Remove(existing);
            Markers.Add(value);
        }
    }

    private double? _hFov;
    [Degrees]
    [Category("View"), Description("Horizontal field of view in degrees (overrides tour default)")]
    public double? HFov { get => _hFov; set { _hFov = value; OnPropertyChanged(); } }

    [Browsable(false)]
    public ObservableCollection<MarkerBase> Markers { get; set; } = [];
}
