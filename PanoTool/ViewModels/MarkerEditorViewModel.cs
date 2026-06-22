using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.ViewModels;

/// <summary>
/// Right dock tool: type-switcher + coords form on top, PropertyGrid for
/// type-specific marker fields below.
/// </summary>
public partial class MarkerEditorViewModel : Tool
{
    private readonly MainWindowViewModel _main;
    private MarkerBase? _marker;
    private Scene? _scene;
    private TourDocument? _doc;
    private bool _syncing;

    [ObservableProperty] private bool _hasMarker;
    [ObservableProperty] private string _markerType = "info";
    [ObservableProperty] private double _coordsX;
    [ObservableProperty] private double _coordsY;

    // What the PropertyGrid binds to — updated when marker or type changes
    [ObservableProperty] private MarkerBase? _currentMarker;

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
        if (_marker != null)
            _marker.PropertyChanged -= OnMarkerPropertyChanged;

        _marker = marker;
        _scene = scene;
        _doc = doc;
        HasMarker = marker != null;
        CurrentMarker = marker;

        if (marker == null) return;

        marker.PropertyChanged += OnMarkerPropertyChanged;

        _syncing = true;
        try
        {
            CoordsX = marker.Coords?[0] ?? 0;
            CoordsY = marker.Coords?[1] ?? Math.PI / 2;
            MarkerType = marker.Type;
        }
        finally { _syncing = false; }
    }

    private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _main.MarkDirty();

    // ── Coords write-back (Coords[] is [Browsable(false)], kept as manual fields) ──

    partial void OnCoordsXChanged(double value)
    {
        if (_syncing) return;
        if (_marker?.Coords is { Length: >= 2 }) { _marker.Coords[0] = value; _main.MarkDirty(); }
    }

    partial void OnCoordsYChanged(double value)
    {
        if (_syncing) return;
        if (_marker?.Coords is { Length: >= 2 }) { _marker.Coords[1] = value; _main.MarkDirty(); }
    }

    // ── Type switching — replaces the marker object in the scene ──────────────

    partial void OnMarkerTypeChanged(string value)
    {
        if (_syncing) return;
        if (_scene == null || _marker == null) return;
        var idx = _scene.Markers.IndexOf(_marker);
        if (idx < 0) return;

        _marker.PropertyChanged -= OnMarkerPropertyChanged;

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
        _main.MarkDirty();
    }
}
