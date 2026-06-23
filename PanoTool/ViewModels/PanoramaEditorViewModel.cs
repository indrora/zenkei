using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.ViewModels;

/// <summary>
/// ViewModel for one panorama editor tab in the center DocumentDock.
/// Owns the scene being edited and the currently selected marker.
/// </summary>
public partial class PanoramaEditorViewModel : Document
{
    private readonly MainWindowViewModel _main;

    /// <summary>
    /// The scene's internal dictionary key.  Settable so <see cref="MainWindowViewModel.TryRenameScene"/>
    /// can keep it in sync after a rename without recreating the editor VM.
    /// </summary>
    public string SceneId { get; set; }

    public Scene Scene { get; }

    // ── Pan/zoom state ────────────────────────────────────────────────────────
    // Null scale = "fit to window" mode; the canvas recalculates on every resize.
    // Set by the canvas on first user pan/zoom; observable so the HUD can bind.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyCanExecuteChangedFor(nameof(ResetZoomCommand))]
    private float? _panZoomScale;

    [ObservableProperty] private float _panZoomOffX;
    [ObservableProperty] private float _panZoomOffY;

    /// <summary>"Fit" when in fit-to-window mode; "×N.NN" while zoomed.</summary>
    public string ZoomLabel => PanZoomScale.HasValue ? $"×{PanZoomScale.Value:F2}" : "Fit";

    [RelayCommand(CanExecute = nameof(IsZoomed))]
    private void ResetZoom() => PanZoomScale = null;

    private bool IsZoomed => PanZoomScale.HasValue;

    [ObservableProperty]
    private MarkerBase? _selectedMarker;

    partial void OnSelectedMarkerChanged(MarkerBase? value)
    {
        _main.Properties.SetMarker(value, Scene);
    }

    /// <summary>
    /// First few words of the scene title for the tab subtitle, blank when the title
    /// matches the ID (redundant) or is empty.
    /// </summary>
    public string TabSubTitle
    {
        get
        {
            var t = Scene.Title;
            if (string.IsNullOrEmpty(t) || t == SceneId) return "";
            var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sub = string.Join(' ', words.Take(4));
            return sub.Length > 30 ? sub[..27] + "…" : sub;
        }
    }

    public PanoramaEditorViewModel(Scene scene, MainWindowViewModel main)
    {
        Scene = scene;
        SceneId = scene.Id;
        _main = main;
        Id = $"PanoEditor_{scene.Id}";
        Title = scene.Id;    // tab shows the internal name, not the human title
        CanClose = true;
        CanPin = false;
        CanFloat = false;
    }

    /// <summary>
    /// Called when Scene.Title changes externally; refreshes the tab subtitle.
    /// Title (= SceneId) is unchanged — tab labels are the internal ID.
    /// </summary>
    public void RefreshTitle() => OnPropertyChanged(nameof(TabSubTitle));

    /// <summary>Relay from canvas MarkerMoved event — updates the Properties panel live.</summary>
    public void OnMarkerMoved(double yaw, double pitch)
    {
        _main.Properties.UpdateMarkerCoords(yaw, pitch);
        _main.MarkDirty();
    }

    /// <summary>Relay from canvas InitialViewChanged — syncs the Properties panel display.</summary>
    public void OnInitialViewChanged(double yaw, double pitch)
    {
        Scene.Initial[0] = yaw;
        Scene.Initial[1] = pitch;
        _main.Properties.SyncInitialView(yaw, pitch);
        _main.MarkDirty();
    }
}
