using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Zenkei.PropertyGrid;

namespace Zenkei.Models;

/// <summary>
/// Lightweight PropertyGrid subject shown when the "Initial POV" tree node is
/// selected.  Wraps Scene.Initial[] (radians) and exposes Yaw/Pitch in degrees.
/// No other properties — intentionally minimal.
/// </summary>
public sealed class InitialViewSubject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    internal Scene Scene { get; }

    public InitialViewSubject(Scene scene) => Scene = scene;

    [Degrees]
    [Range(-180.0, 180.0)]
    [Category("Position"), Description("Yaw of initial view in degrees (-180 … 180)")]
    public double Yaw
    {
        get => Scene.Initial[0] * 180.0 / Math.PI;
        set { Scene.Initial[0] = value * Math.PI / 180.0; Notify(); }
    }

    [Degrees]
    [Range(0.0, 180.0)]
    [Category("Position"), Description("Pitch of initial view in degrees (0 = top, 180 = bottom)")]
    public double Pitch
    {
        get => Scene.Initial[1] * 180.0 / Math.PI;
        set { Scene.Initial[1] = value * Math.PI / 180.0; Notify(); }
    }

    /// <summary>
    /// Called by PropertiesViewModel.SyncInitialView when the canvas drag updates
    /// Scene.Initial[] directly, so the PropertyGrid display stays current.
    /// </summary>
    internal void NotifyPositionChanged()
    {
        Notify(nameof(Yaw));
        Notify(nameof(Pitch));
    }

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
