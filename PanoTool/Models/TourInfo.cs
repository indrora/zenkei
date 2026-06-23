using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zenkei.Models;

public class TourInfo : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _title = "Untitled Tour";
    [Category("Tour"), Description("Display title of the tour")]
    public string Title { get => _title; set { _title = value; Notify(); } }

    private string _author = "";
    [Category("Tour"), Description("Tour author name")]
    public string Author { get => _author; set { _author = value; Notify(); } }
}
