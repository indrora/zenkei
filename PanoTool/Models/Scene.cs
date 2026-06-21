using PanoTool.Models.Markers;

namespace PanoTool.Models;

public class Scene
{
    // Scene key in the TourDocument.Scenes dictionary — not stored in YAML body
    public string Id { get; set; } = "";

    public string Image { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    // [yaw_rad, pitch_from_top_rad] initial viewing direction
    public double[] Initial { get; set; } = [0.0, Math.PI / 2];

    // Overrides TourDefaults.HFov when set
    public double? HFov { get; set; }

    public List<MarkerBase> Markers { get; set; } = [];
}
