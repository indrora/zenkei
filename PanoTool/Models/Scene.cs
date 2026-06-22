using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zenkei.Models.Markers;

namespace Zenkei.Models;

public class Scene : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Scene key in TourDocument.Scenes — not stored in YAML body, not shown in grid
    [Browsable(false)]
    public string Id { get; set; } = "";

    private string _image = "";
    [Category("Scene"), ReadOnly(true), Description("Image file path — use the toolbar button to change")]
    public string Image { get => _image; set { _image = value; OnPropertyChanged(); } }

    private string _title = "";
    [Category("Scene"), Description("Display name for this scene")]
    public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }

    private string? _description;
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

    // [yaw_rad, pitch_from_top_rad] — edited via NumericUpDowns in the scene panel, not the grid
    [Browsable(false)]
    public double[] Initial { get; set; } = [0.0, Math.PI / 2];

    private double? _hFov;
    [Category("View"), Description("Horizontal field of view in degrees (overrides tour default)")]
    public double? HFov { get => _hFov; set { _hFov = value; OnPropertyChanged(); } }

    [Browsable(false)]
    public List<MarkerBase> Markers { get; set; } = [];
}
