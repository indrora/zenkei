namespace Zenkei.Models.Markers;

public class SceneMarker : MarkerBase
{
    public override string Type => "scene";
    // Key of the target scene in TourDocument.Scenes
    public string TargetScene { get; set; } = "";
    public string? Text { get; set; }
}
