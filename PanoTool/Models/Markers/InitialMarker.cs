namespace Zenkei.Models.Markers;

/// <summary>
/// The inital marker is a special marker that is owned by the scene itself; this marker defines the initial view when the scene is loaded.
/// It is not a "real" marker in the sense that it doesn't "show up" in the tour but needs to be treated as a marker for the purposes of the document view.
/// </summary>
public class InitialMarker : MarkerBase
{
    public override string Type => "__initial__";
}