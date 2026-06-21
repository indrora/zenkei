namespace Zenkei.Models.Markers;

public abstract class MarkerBase
{
    // "link" | "info" | "scene"
    public abstract string Type { get; }

    // [yaw_rad, pitch_from_top_rad] — x in -π..π, y in 0..π
    public double[]? Coords { get; set; }

    // Name of a named icon defined in TourDocument.Icons
    public string? Marker { get; set; }
}
