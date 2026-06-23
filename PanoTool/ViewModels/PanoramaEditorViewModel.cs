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

    public string SceneId { get; }

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
        _main.MarkerEditor.SetMarker(value, Scene, _main.Document);
    }

    public PanoramaEditorViewModel(Scene scene, MainWindowViewModel main)
    {
        Scene = scene;
        SceneId = scene.Id;
        _main = main;
        Id = $"PanoEditor_{scene.Id}";
        Title = scene.Title;
        CanClose = true;
        CanPin = false;
        CanFloat = false;
    }

    public void RefreshTitle() => Title = Scene.Title;

    /// <summary>Relay from canvas MarkerMoved event — updates the editor panel live.</summary>
    public void OnMarkerMoved(double yaw, double pitch)
    {
        _main.MarkerEditor.UpdateCoords(yaw, pitch);
        _main.MarkDirty();
    }

    /// <summary>Relay from canvas InitialViewChanged — syncs the scene panel display.</summary>
    public void OnInitialViewChanged(double yaw, double pitch)
    {
        Scene.Initial[0] = yaw;
        Scene.Initial[1] = pitch;
        _main.SceneProperties.SyncInitialView(yaw, pitch);
        _main.MarkDirty();
    }
}
