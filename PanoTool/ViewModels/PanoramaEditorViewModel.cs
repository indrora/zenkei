using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using PanoTool.Models;
using PanoTool.Models.Markers;

namespace PanoTool.ViewModels;

/// <summary>
/// ViewModel for one panorama editor tab in the center DocumentDock.
/// Owns the scene being edited and the currently selected marker.
/// </summary>
public partial class PanoramaEditorViewModel : Document
{
    private readonly MainWindowViewModel _main;

    public string SceneId { get; }

    public Scene Scene { get; }

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
}
