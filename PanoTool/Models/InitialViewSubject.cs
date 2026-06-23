using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zenkei.Models;

/// <summary>
/// Lightweight PropertyGrid subject shown when the initial viewpoint is selected
/// (e.g. via canvas click on the POV indicator).
/// Wraps Scene.Initial[] (radians) and exposes Yaw/Pitch via a combined
/// <see cref="YawPitch"/> property rendered by YawPitchCellFactory.
/// </summary>
public sealed class InitialViewSubject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    internal Scene Scene { get; }

    public InitialViewSubject(Scene scene) => Scene = scene;

    [Category("Position"), Description("Yaw and pitch of the initial view in degrees")]
    public YawPitch Position
    {
        get => new(Scene.Initial[0] * 180.0 / Math.PI,
                   Scene.Initial[1] * 180.0 / Math.PI);
        set
        {
            Scene.Initial[0] = value.Yaw   * Math.PI / 180.0;
            Scene.Initial[1] = value.Pitch * Math.PI / 180.0;
            Notify();
        }
    }

    /// <summary>
    /// Called by PropertiesViewModel.SyncInitialView when the canvas drag updates
    /// Scene.Initial[] directly, so the PropertyGrid display stays current.
    /// </summary>
    internal void NotifyPositionChanged() => Notify(nameof(Position));

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
