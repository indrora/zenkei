using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Zenkei.PropertyGrid;

namespace Zenkei.Models;

/// <summary>
/// PropertyGrid subject shown when the tour root node is selected.
/// Delegates to both <see cref="TourInfo"/> and <see cref="TourDefaults"/>
/// so all tour-level properties appear in one flat list.
/// </summary>
public sealed class TourSubject : INotifyPropertyChanged
{
    private readonly TourDocument _doc;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public TourSubject(TourDocument doc) => _doc = doc;

    // ── Tour info ──────────────────────────────────────────────────────────────

    [Category("Tour"), Description("Display title of the tour")]
    public string Title
    {
        get => _doc.Information.Title;
        set { _doc.Information.Title = value; Notify(); }
    }

    [Category("Tour"), Description("Tour author name")]
    public string Author
    {
        get => _doc.Information.Author;
        set { _doc.Information.Author = value; Notify(); }
    }

    // ── Tour defaults ──────────────────────────────────────────────────────────

    [Degrees]
    [Range(10.0, 170.0)]
    [Category("Defaults"), Description("Default horizontal field of view in degrees")]
    public double DefaultHFov
    {
        get => _doc.Default.HFov;
        set { _doc.Default.HFov = value; Notify(); }
    }

    [Category("Defaults"), Description("ID of the scene shown first when the tour loads")]
    public string InitialScene
    {
        get => _doc.Default.FirstScene ?? "";
        set { _doc.Default.FirstScene = string.IsNullOrWhiteSpace(value) ? null : value; Notify(); }
    }
}
