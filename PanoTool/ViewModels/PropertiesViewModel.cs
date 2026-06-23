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

        _marker = marker;
        marker.PropertyChanged += OnMarkerChanged;
        Subject = marker;
    }

    /// <summary>
    /// Called during canvas initial-view drag.  Scene.Initial[] is already updated
    /// by PanoramaEditorViewModel; this tells the PropertyGrid subject to refresh.
    /// </summary>
    public void SyncInitialView(double yaw, double pitch)
    {
        if (Subject is InitialViewSubject ivs)
            ivs.NotifyPositionChanged();
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
