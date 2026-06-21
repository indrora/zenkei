using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.ViewModels;

/// <summary>
/// Right dock tool: edits the currently selected marker.
/// </summary>
public partial class MarkerEditorViewModel : Tool
{
    private readonly MainWindowViewModel _main;
    private MarkerBase? _marker;
    private Scene? _scene;
    private TourDocument? _doc;

    // True while SetMarker is syncing the form to the selected marker, so the
    // field write-back handlers (esp. OnMarkerTypeChanged, which rebuilds the
    // marker) don't fire in response to programmatic updates.
    private bool _syncing;

    // ── Panel visibility (only one type group shown at a time) ────────────────

    [ObservableProperty] private bool _isLink;
    [ObservableProperty] private bool _isInfo;
    [ObservableProperty] private bool _isScene;
    [ObservableProperty] private bool _hasMarker;

    // ── Common fields ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _markerType = "info";
    [ObservableProperty] private double _coordsX;
    [ObservableProperty] private double _coordsY;
    [ObservableProperty] private string? _selectedIconName;
    [ObservableProperty] private ObservableCollection<string> _availableIcons = [];

    // ── Link fields ───────────────────────────────────────────────────────────

    [ObservableProperty] private string _linkUrl = "";

    // ── Info fields ───────────────────────────────────────────────────────────

    [ObservableProperty] private string _infoText = "";

    // ── Scene link fields ─────────────────────────────────────────────────────

    [ObservableProperty] private string? _targetSceneId;
    [ObservableProperty] private string _sceneText = "";
    [ObservableProperty] private ObservableCollection<string> _availableScenes = [];

    // ── Marker types list ─────────────────────────────────────────────────────

    public IReadOnlyList<string> MarkerTypes { get; } = ["link", "info", "scene"];

    public MarkerEditorViewModel(MainWindowViewModel main)
    {
        _main = main;
        Id = "MarkerEditor";
        Title = "Marker";
        CanClose = false;
        CanPin = false;
        CanFloat = false;
    }

    /// <summary>Called by PanoramaEditorViewModel when selection changes.</summary>
    public void SetMarker(MarkerBase? marker, Scene? scene, TourDocument? doc)
    {
        _marker = marker;
        _scene = scene;
        _doc = doc;
        HasMarker = marker != null;

        if (marker == null) return;

        _syncing = true;
        try
        {
            // Update available scenes list
            if (doc != null)
            {
                AvailableScenes = new ObservableCollection<string>(doc.Scenes.Keys);
                // Rebuild available icons
                var icons = new List<string> { "link", "info", "scene" };
                icons.AddRange(doc.Icons.Keys);
                AvailableIcons = new ObservableCollection<string>(icons);
            }

            CoordsX = marker.Coords?[0] ?? 0;
            CoordsY = marker.Coords?[1] ?? Math.PI / 2;
            SelectedIconName = marker.Marker;
            MarkerType = marker.Type;

            IsLink = marker is LinkMarker;
            IsInfo = marker is InfoMarker;
            IsScene = marker is SceneMarker;

            if (marker is LinkMarker lm) LinkUrl = lm.Url;
            if (marker is InfoMarker im) InfoText = im.Text;
            if (marker is SceneMarker sm)
            {
                TargetSceneId = sm.TargetScene;
                SceneText = sm.Text ?? "";
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    // ── Write-back when user edits fields ─────────────────────────────────────

    partial void OnCoordsXChanged(double value)
    {
        if (_syncing) return;
        if (_marker?.Coords is { Length: >= 2 }) { _marker.Coords[0] = value; Dirty(); }
    }
    partial void OnCoordsYChanged(double value)
    {
        if (_syncing) return;
        if (_marker?.Coords is { Length: >= 2 }) { _marker.Coords[1] = value; Dirty(); }
    }
    partial void OnSelectedIconNameChanged(string? value)
    {
        if (_syncing) return;
        if (_marker != null) { _marker.Marker = value; Dirty(); }
    }
    partial void OnLinkUrlChanged(string value)
    {
        if (_syncing) return;
        if (_marker is LinkMarker lm) { lm.Url = value; Dirty(); }
    }
    partial void OnInfoTextChanged(string value)
    {
        if (_syncing) return;
        if (_marker is InfoMarker im) { im.Text = value; Dirty(); }
    }
    partial void OnTargetSceneIdChanged(string? value)
    {
        if (_syncing) return;
        if (_marker is SceneMarker sm) { sm.TargetScene = value ?? ""; Dirty(); }
    }
    partial void OnSceneTextChanged(string value)
    {
        if (_syncing) return;
        if (_marker is SceneMarker sm) { sm.Text = string.IsNullOrEmpty(value) ? null : value; Dirty(); }
    }

    partial void OnMarkerTypeChanged(string value)
    {
        // Type change replaces the marker in the scene — but only when the user
        // picks a new type, not while SetMarker is syncing the form to selection.
        if (_syncing) return;
        if (_scene == null || _marker == null) return;
        var idx = _scene.Markers.IndexOf(_marker);
        if (idx < 0) return;

        var coords = _marker.Coords;
        var iconName = _marker.Marker;

        MarkerBase newMarker = value switch
        {
            "link" => new LinkMarker(),
            "scene" => new SceneMarker(),
            _ => new InfoMarker()
        };
        newMarker.Coords = coords;
        newMarker.Marker = iconName;
        _scene.Markers[idx] = newMarker;
        SetMarker(newMarker, _scene, _doc);
        Dirty();
    }

    private void Dirty() => _main.MarkDirty();
}
