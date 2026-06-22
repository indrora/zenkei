using System.ComponentModel;

namespace Zenkei.Models.Markers;

public class InfoMarker : MarkerBase
{
    public override string Type => "info";

    private string _text = "";
    [Category("Info"), Description("Text to display when this marker is clicked")]
    public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
}
