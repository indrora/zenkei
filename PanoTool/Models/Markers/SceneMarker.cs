using System.ComponentModel;
using Zenkei.PropertyGrid;

namespace Zenkei.Models.Markers;

public class SceneMarker : MarkerBase
{
    public override string Type => "scene";

    private string _targetScene = "";
    [SceneId]
    [Category("Scene Link"), Description("ID of the target scene to navigate to")]
    public string TargetScene { get => _targetScene; set { _targetScene = value; OnPropertyChanged(); } }

    private string? _text;
    [Category("Scene Link"), Description("Optional transition label shown on the marker")]
    public string? Text { get => _text; set { _text = value; OnPropertyChanged(); } }
}
