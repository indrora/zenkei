using System.ComponentModel;

namespace Zenkei.Models.Markers;

public class LinkMarker : MarkerBase
{
    public override string Type => "link";

    private string _url = "";
    [Category("Link"), Description("URL to open when the marker is clicked")]
    public string Url { get => _url; set { _url = value; OnPropertyChanged(); } }
}
