namespace Zenkei.Models;

public class TourDefaults
{
    public double HFov { get; set; } = 120.0;

    /// <summary>Scene ID shown first when the tour loads; null = use the first scene in document order.</summary>
    public string? FirstScene { get; set; }
}
