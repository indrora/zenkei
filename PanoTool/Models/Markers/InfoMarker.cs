using System.ComponentModel;
using Zenkei.PropertyGrid;

namespace Zenkei.Models.Markers;

public class InfoMarker : MarkerBase
{
    public override string Type => "info";

    private string _text = "";
    [Multiline]
    [Category("Info"), Description("Text to display when this marker is clicked")]
    public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
}
