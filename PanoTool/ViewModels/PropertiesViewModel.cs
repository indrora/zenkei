using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.ViewModels;

/// <summary>
/// Unified properties tool.  Whatever was last clicked — scene node, initial-POV
/// node, marker node, or canvas marker — drives the single subject shown here.
/// Replaces the old ScenePropertiesViewModel + MarkerEditorViewModel pair.
/// </summary>
public partial class PropertiesViewModel : Tool
{
    private readonly MainWindowViewModel _main;

    // Internal subject references (not all exposed as [ObservableProperty]).
    private Scene?      _scene;
    private MarkerBase? _marker;

    // Sync guards shared from old VMs.
    private bool _syncingScene;
    private bool _syncing;
    private bool _syncingNud;

    // ── View-state flags ──────────────────────────────────────────────────────

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isScene;
    [ObservableProperty] private bool _isMarker;

    // ── Scene panel ───────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScene))]
    [NotifyCanExecuteChangedFor(nameof(RenameSceneCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeImageCommand))]
    private Scene? _sceneSubject;

    [ObservableProperty] private string _editingSceneId = "";
    [ObservableProperty] private double _sceneInitialX;
    [ObservableProperty] private double _sceneInitialY;

    public bool HasScene => SceneSubject != null;

    // ── Marker panel ──────────────────────────────────────────────────────────

    [ObservableProperty] private bool _hasMarker;
    [ObservableProperty] private string _markerType = "info";
    [ObservableProperty] private double _coordsX;
    [ObservableProperty] private double _coordsY;
    [ObservableProperty] private decimal? _nudYaw;
    [ObservableProperty] private decimal? _nudPitch;

    // Bound to the marker PropertyGrid.
    [ObservableProperty] private MarkerBase? _currentMarker;

    public IReadOnlyList<string> MarkerTypes { get; } = ["link", "info", "scene"];

    // ── Construction ──────────────────────────────────────────────────────────

    public PropertiesViewModel(MainWindowViewModel main)
    {
        _main = main;
        Id       = "Properties";
        Title    = "Properties";
        CanClose = false;
        CanPin   = true;
        CanFloat = true;
    }

    // ── Public subject API (called by tree / canvas / editor VM) ─────────────

    /// <summary>Show scene properties.  Pass null to show the empty hint.</summary>
    public void SetScene(Scene? scene)
    {
        if (_marker != null)
        {
            _marker.PropertyChanged -= OnMarkerPropertyChanged;
            _marker = null;
        }
        HasMarker     = false;
        CurrentMarker = null;

        _scene       = scene;
        SceneSubject = scene;

        if (scene != null)
        {
            IsEmpty  = false;
            IsScene  = true;
            IsMarker = false;
            SyncInitialView(scene.Initial[0], scene.Initial[1]);
            EditingSceneId = scene.Id;
        }
        else
        {
            IsEmpty  = true;
            IsScene  = false;
            IsMarker = false;
            EditingSceneId = "";
        }
    }

    /// <summary>
    /// Show marker properties.  When <paramref name="marker"/> is null, falls
    /// back to showing the scene properties for <paramref name="scene"/>.
    /// </summary>
    public void SetMarker(MarkerBase? marker, Scene? scene)
    {
        if (_marker != null)
        {
            _marker.PropertyChanged -= OnMarkerPropertyChanged;
            _marker = null;
        }

        if (marker == null)
        {
            SetScene(scene);
            return;
        }

        _marker = marker;
        _scene  = scene;

        marker.PropertyChanged += OnMarkerPropertyChanged;

        SceneSubject  = scene;
        HasMarker     = true;
        CurrentMarker = marker;
        IsEmpty       = false;
        IsScene       = false;
        IsMarker      = true;

        _syncing = true;
        try
        {
            CoordsX    = marker.Coords?[0] ?? 0;
            CoordsY    = marker.Coords?[1] ?? Math.PI / 2;
            MarkerType = marker.Type;
        }
        finally { _syncing = false; }
    }

    /// <summary>
    /// Sync the initial-view NUDs without write-back (called during canvas drag
    /// and on scene selection).
    /// </summary>
    public void SyncInitialView(double yaw, double pitch)
    {
        _syncingScene = true;
        try { SceneInitialX = yaw; SceneInitialY = pitch; }
        finally { _syncingScene = false; }
    }

    /// <summary>
    /// Sync the marker-coord display during a live canvas drag without triggering
    /// Coords[] write-back (canvas already updated them directly).
    /// </summary>
    public void UpdateMarkerCoords(double yaw, double pitch)
    {
        _syncing = true;
        try { CoordsX = yaw; CoordsY = pitch; }
        finally { _syncing = false; }
    }

    // ── Scene commands ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasScene))]
    private async Task RenameSceneAsync()
    {
        if (_scene == null) return;
        var error = _main.TryRenameScene(_scene, EditingSceneId);
        if (error != null)
        {
            EditingSceneId = _scene.Id;
            await _main.ShowErrorAsync("Rename failed", error);
        }
    }

    [RelayCommand(CanExecute = nameof(HasScene))]
    private async Task ChangeImageAsync()
    {
        if (_scene == null) return;

        var files = await _main.PickFilesAsync(
            "Select replacement panorama image",
            ["*.jpg", "*.jpeg", "*.png", "*.webp"]);
        var path = files.FirstOrDefault();
        if (path == null) return;

        string imagePath;
        try { imagePath = _main.RelativizeImagePath(path); }
        catch (ArgumentException ex)
        {
            await _main.ShowErrorAsync("Image rejected", ex.Message);
            return;
        }

        _scene.Image = imagePath;
        _scene.BaseDirectory = _main.Document.FilePath != null
            ? Path.GetDirectoryName(Path.GetFullPath(_main.Document.FilePath))
            : null;
        _main.MarkDirty();
    }

    // ── Scene write-backs ─────────────────────────────────────────────────────

    partial void OnSceneInitialXChanged(double value)
    {
        if (_syncingScene || _scene == null) return;
        _scene.Initial[0] = value;
        _main.MarkDirty();
    }

    partial void OnSceneInitialYChanged(double value)
    {
        if (_syncingScene || _scene == null) return;
        _scene.Initial[1] = value;
        _main.MarkDirty();
    }

    // ── Marker write-backs ────────────────────────────────────────────────────

    private void OnMarkerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => _main.MarkDirty();

    partial void OnCoordsXChanged(double value)
    {
        _syncingNud = true;
        NudYaw = (decimal)Math.Round(value * 180.0 / Math.PI, 1);
        _syncingNud = false;

        if (_syncing) return;
        if (_marker?.Coords is { Length: >= 2 })
        {
            _marker.Coords[0] = value;
            _marker.NotifyCoordsChanged();
            _main.MarkDirty();
        }
    }

    partial void OnCoordsYChanged(double value)
    {
        _syncingNud = true;
        NudPitch = (decimal)Math.Round(value * 180.0 / Math.PI, 1);
        _syncingNud = false;

        if (_syncing) return;
        if (_marker?.Coords is { Length: >= 2 })
        {
            _marker.Coords[1] = value;
            _marker.NotifyCoordsChanged();
            _main.MarkDirty();
        }
    }

    partial void OnNudYawChanged(decimal? value)
    {
        if (_syncing || _syncingNud || value is null) return;
        CoordsX = (double)value * Math.PI / 180.0;
    }

    partial void OnNudPitchChanged(decimal? value)
    {
        if (_syncing || _syncingNud || value is null) return;
        CoordsY = (double)value * Math.PI / 180.0;
    }

    // ── Marker type switching ─────────────────────────────────────────────────

    partial void OnMarkerTypeChanged(string value)
    {
        if (_syncing) return;
        if (_scene == null || _marker == null) return;

        var idx = _scene.Markers.IndexOf(_marker);
        if (idx < 0) return;

        _marker.PropertyChanged -= OnMarkerPropertyChanged;

        var coords   = _marker.Coords;
        var iconName = _marker.Marker;

        MarkerBase newMarker = value switch
        {
            "link"  => new LinkMarker(),
            "scene" => new SceneMarker(),
            _       => new InfoMarker()
        };
        newMarker.Coords  = coords;
        newMarker.Marker  = iconName;
        _scene.Markers[idx] = newMarker;

        SetMarker(newMarker, _scene);
        _main.MarkDirty();
    }
}
