namespace Zenkei.Models.Markers;

public class InfoMarker : MarkerBase
{
    public override string Type => "info";
    public string Text { get; set; } = "";
}
