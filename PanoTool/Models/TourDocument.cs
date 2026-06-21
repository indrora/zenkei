namespace Zenkei.Models;

public class TourDocument
{
    public TourInfo Information { get; set; } = new();
    public TourDefaults Default { get; set; } = new();

    // keyed by scene ID
    public Dictionary<string, Scene> Scenes { get; set; } = [];

    // Named icons: name → file path (relative to tour file)
    public Dictionary<string, string> Icons { get; set; } = [];

    // Runtime-only: current save path
    public string? FilePath { get; set; }
}
