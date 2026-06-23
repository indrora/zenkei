using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.ViewModels;

/// <summary>
/// Unified Properties tool: holds a single <see cref="Subject"/> that drives the
/// <see cref="Zenkei.Controls.ZenkeiPropertyGrid"/> in PropertiesView.
///
/// Selection pipeline:
///   TourRootNode      → SetTourSubject(TourSubject)       → Subject = TourSubject
///   ScenesFolderNode  → SetScene(null)                    → Subject = null
///   SceneItemNode     → SetScene(scene)                   → Subject = scene
///   InitialPovNode /
///   canvas POV click  → SetInitialPov(scene)              → Subject = InitialViewSubject
///   MarkerItemNode /
///   canvas click      → SetMarker(marker, scene)          → Subject = marker subclass
///   canvas deselect   → SetMarker(null, scene)            → Subject = scene
/// </summary>
public partial class PropertiesViewModel : Tool
{
    private readonly MainWindowViewModel _main;
    private Scene?              _scene;
    private MarkerBase?         _marker;
    private TourSubject?        _tourSubject;
    private InitialViewSubject? _initialViewSubject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteMarker))]
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

    /// <summary>
    /// Live scene-ID collection for <see cref="Zenkei.Controls.ZenkeiPropertyGrid.SceneNames"/>.
    /// Returns null when no document is open.
    /// </summary>
    public IEnumerable<string>? DocumentSceneNames => _main.Document?.Scenes.Keys;

    // ── Public subject API ────────────────────────────────────────────────────

    /// <summary>Shows tour-level metadata (title, author, defaults) for the root node.</summary>
    public void SetTourSubject(TourSubject subject)
    {
        DropAll();
        _tourSubject = subject;
        subject.PropertyChanged += OnTourSubjectChanged;
        Subject = subject;
    }

    public void SetScene(Scene? scene)
    {
        DropAll();
        _scene  = scene;
        Subject = scene;
    }

    /// <summary>
    /// Shows only Position for the scene's initial viewpoint.
    /// Called when the user clicks the POV indicator on the canvas, or selects
    /// the (now hidden) InitialPovNode programmatically.
    /// </summary>
    public void SetInitialPov(Scene? scene)
    {
        DropAll();
        _scene = scene;
        if (scene == null) { Subject = null; return; }

        // Cache one subject per scene to avoid allocations on repeated clicks.
        if (_initialViewSubject?.Scene != scene)
            _initialViewSubject = new InitialViewSubject(scene);

        Subject = _initialViewSubject;
    }

    public void SetMarker(MarkerBase? marker, Scene? scene)
    {
        DropAll();
        _scene = scene;

        if (marker == null) { Subject = scene; return; }

        // The InitialMarker is synthetic; show the initial-view subject instead.
        if (marker is InitialMarker) { SetInitialPov(scene); return; }

        _marker = marker;
        marker.PropertyChanged += OnMarkerChanged;
        Subject = marker;
    }

    public bool CanDeleteMarker => _marker != null;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void DeleteSelectedMarker()
    {
        if (_marker == null || _scene == null) return;
        _scene.Markers.Remove(_marker);
        SetScene(_scene);
        _main.MarkDirty();
    }

    /// <summary>
    /// Called on every frame of a canvas initial-view drag.
    /// On the first call, switches the Properties panel to show the initial POV subject.
    /// On subsequent calls (same scene), just refreshes the Position cell.
    /// Scene.Initial[] is already updated by the canvas before this is called.
    /// </summary>
    public void SyncInitialView(double yaw, double pitch)
    {
        if (Subject is InitialViewSubject ivs && ivs.Scene == _scene)
        {
            // Already showing this scene's initial POV — refresh the position cell.
            ivs.NotifyPositionChanged();
        }
        else if (_scene != null)
        {
            // First drag event (or Properties was showing something else).
            // Switch to InitialViewSubject; the grid rebuild reads Scene.Initial[]
            // which the canvas already updated, so the cell shows the right value.
            SetInitialPov(_scene);
        }
    }

    /// <summary>
    /// Called during canvas marker drag.  Coords[] are already updated by the
    /// canvas; this fires PropertyChanged(Position) so the grid refreshes.
    /// </summary>
    public void UpdateMarkerCoords(double yaw, double pitch)
        => _marker?.NotifyCoordsChanged();

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Unsubscribes from all tracked subjects and clears fields.</summary>
    private void DropAll()
    {
        if (_marker != null)
        {
            _marker.PropertyChanged -= OnMarkerChanged;
            _marker = null;
        }

        if (_tourSubject != null)
        {
            _tourSubject.PropertyChanged -= OnTourSubjectChanged;
            _tourSubject = null;
        }
    }

    private void OnMarkerChanged(object? sender, PropertyChangedEventArgs e)
        => _main.MarkDirty();

    private void OnTourSubjectChanged(object? sender, PropertyChangedEventArgs e)
        => _main.MarkDirty();
}
