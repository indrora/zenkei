using Zenkei.Models.Markers;

namespace Zenkei.Models;

public class Scene
{
    // Scene key in the TourDocument.Scenes dictionary — not stored in YAML body
    public string Id { get; set; } = "";

    public string Image { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    // Directory of the tour file this scene was loaded from. Used to resolve
    // relative Image paths. Runtime-only — never written to YAML.
    public string? BaseDirectory { get; set; }

    /// <summary>
    /// The Image path resolved to an absolute location. Relative paths are
    /// resolved against <see cref="BaseDirectory"/> (the tour file's folder).
    /// </summary>
    public string ResolvedImagePath =>
        string.IsNullOrEmpty(Image) || Path.IsPathRooted(Image) || string.IsNullOrEmpty(BaseDirectory)
            ? Image
            : Path.Combine(BaseDirectory, Image);

    // [yaw_rad, pitch_from_top_rad] initial viewing direction
    public double[] Initial { get; set; } = [0.0, Math.PI / 2];

    // Overrides TourDefaults.HFov when set
    public double? HFov { get; set; }

    public List<MarkerBase> Markers { get; set; } = [];
}
