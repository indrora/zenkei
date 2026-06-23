using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;
using Zenkei.Models.Markers;
using System.ComponentModel;

namespace Zenkei.ViewModels;

/// <summary>
/// Unified Properties tool: holds a single <see cref="Subject"/> that drives the
/// <see cref="Zenkei.Controls.ZenkeiPropertyGrid"/> in PropertiesView.
///
/// Selection pipeline:
///   SceneItemNode     → SetScene(scene)      → Subject = scene
///   InitialPovNode    → SetInitialPov(scene) → Subject = InitialViewSubject (Yaw/Pitch only)
///   MarkerItemNode /
///   canvas click      → SetMarker(marker, scene) → Subject = marker subclass
///   canvas deselect   → SetMarker(null, scene)   → Subject = scene
/// </summary>
public partial class PropertiesViewModel : Tool
{
    private readonly MainWindowViewModel _main;
    private Scene?           _scene;
    private MarkerBase?      _marker;
    private InitialViewSubject? _initialViewSubject;

    [ObservableProperty]
    private object? _subject;

    public PropertiesViewModel(MainWindowViewModel main)
    {
        _main    = main;
        Id       = "Properties";
        Title    = "Properties";
        CanClose = false;
        CanPin   = true;
        CanFloat = true;
    }

    // ── Public subject API ────────────────────────────────────────────────────

    public void SetScene(Scene? scene)
    {
        DropMarker();
        _scene  = scene;
        Subject = scene;
    }

    /// <summary>
    /// Shows tour-level metadata (title, author) when the root node is selected.
    /// </summary>
    public void SetTourInfo(TourInfo info)
    {
        DropMarker();
        _scene  = null;
        Subject = info;
    }

    /// <summary>
    /// Shows only Yaw/Pitch for the scene's initial viewpoint.
    /// Call this when the user selects the InitialPovNode in the tree.
    /// </summary>
    public void SetInitialPov(Scene? scene)
    {
        DropMarker();
        _scene = scene;
        if (scene == null) { Subject = null; return; }

        // Cache one subject per scene to avoid allocations on repeated clicks.
        if (_initialViewSubject?.Scene != scene)
            _initialViewSubject = new InitialViewSubject(scene);

        Subject = _initialViewSubject;
    }

    public void SetMarker(MarkerBase? marker, Scene? scene)
    {
        DropMarker();
        _scene = scene;

        if (marker == null)
        {
            Subject = scene;
            return;
        }

        _marker = marker;
        marker.PropertyChanged += OnMarkerChanged;
        Subject = marker;
    }

    /// <summary>
    /// Called during canvas initial-view drag.  Scene.Initial[] is already
    /// updated by PanoramaEditorViewModel; this tells the PropertyGrid subject
    /// to refresh its Yaw/Pitch cells.
    /// </summary>
    public void SyncInitialView(double yaw, double pitch)
    {
        if (Subject is InitialViewSubject ivs)
            ivs.NotifyPositionChanged();
    }

    /// <summary>
    /// Called during canvas marker drag.  Coords[] are already updated by the
    /// canvas; this fires PropertyChanged(Yaw/Pitch) so the grid refreshes.
    /// </summary>
    public void UpdateMarkerCoords(double yaw, double pitch)
    {
        _marker?.NotifyCoordsChanged();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void DropMarker()
    {
        if (_marker != null)
        {
            _marker.PropertyChanged -= OnMarkerChanged;
            _marker = null;
        }
    }

    private void OnMarkerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => _main.MarkDirty();
}
