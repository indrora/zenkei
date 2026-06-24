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

    [Browsable(false)]
    public string ResolvedImagePath =>
        string.IsNullOrEmpty(Image) || Path.IsPathRooted(Image) || string.IsNullOrEmpty(BaseDirectory)
            ? Image
            : Path.Combine(BaseDirectory, Image);

    // Radians — read/written by the canvas and serializer; degrees via InitialPosition below.
    private YawPitch _initial;
    [Browsable(false)]
    public YawPitch Initial
    {
        get => _initial;
        set
        {
            _initial = value;
            OnPropertyChanged();
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

    private double? _hFov;
    [Degrees]
    [Category("View"), Description("Horizontal field of view in degrees (overrides tour default)")]
    public double? HFov { get => _hFov; set { _hFov = value; OnPropertyChanged(); } }

    [Browsable(false)]
    public ObservableCollection<MarkerBase> Markers { get; set; } = [];
}
