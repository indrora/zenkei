namespace PanoTool.Models.Markers;

public class LinkMarker : MarkerBase
{
    public override string Type => "link";
    public string Url { get; set; } = "";
}
