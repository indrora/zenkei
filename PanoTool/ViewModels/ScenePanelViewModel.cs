using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;

namespace Zenkei.ViewModels;

/// <summary>
/// Left dock tool: scene list + initial-view form + PropertyGrid for scene metadata.
/// </summary>
public partial class ScenePanelViewModel : Tool
{
    private readonly MainWindowViewModel _main;
    private Scene? _subscribedScene;
    private bool _syncingScene;

    public ObservableCollection<Scene> Scenes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedScene))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSceneCommand))]
    private Scene? _selectedScene;

    public bool HasSelectedScene => SelectedScene != null;

    // Initial view is a double[] so we keep manual NumericUpDown fields for it
    [ObservableProperty] private double _sceneInitialX;
    [ObservableProperty] private double _sceneInitialY;

    public ScenePanelViewModel(MainWindowViewModel main)
    {
        _main = main;
        Id = "ScenePanel";
        Title = "Scenes";
        CanClose = false;
        CanPin = false;
        CanFloat = false;
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    partial void OnSelectedSceneChanged(Scene? value)
    {
        if (_subscribedScene != null)
            _subscribedScene.PropertyChanged -= OnScenePropertyChanged;
        _subscribedScene = value;

        if (value == null) return;

        value.PropertyChanged += OnScenePropertyChanged;

        // Sync Initial fields — guard against dirtying on programmatic update
        _syncingScene = true;
        try
        {
            SceneInitialX = value.Initial[0];
            SceneInitialY = value.Initial[1];
        }
        finally { _syncingScene = false; }

        _main.OpenScene(value);
    }

    private void OnScenePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _main.MarkDirty();
        if (e.PropertyName == nameof(Scene.Title) && SelectedScene != null)
            _main.GetOrCreateEditor(SelectedScene).RefreshTitle();
    }

    // ── Initial view write-back ───────────────────────────────────────────────

    partial void OnSceneInitialXChanged(double value)
    {
        if (_syncingScene || _selectedScene == null) return;
        _selectedScene.Initial[0] = value;
        _main.MarkDirty();
    }

    partial void OnSceneInitialYChanged(double value)
    {
        if (_syncingScene || _selectedScene == null) return;
        _selectedScene.Initial[1] = value;
        _main.MarkDirty();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddImageAsync()
    {
        var files = await _main.PickFilesAsync(
            "Select panorama image(s)",
            ["*.jpg", "*.jpeg", "*.png", "*.webp"]);

        foreach (var path in files)
        {
            var scene = CreateSceneFromImagePath(path);
            _main.Document.Scenes[scene.Id] = scene;
            Scenes.Add(scene);
            _main.MarkDirty();
            SelectedScene = scene;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedScene))]
    private void RemoveScene()
    {
        if (SelectedScene == null) return;

        if (_subscribedScene == SelectedScene)
        {
            SelectedScene.PropertyChanged -= OnScenePropertyChanged;
            _subscribedScene = null;
        }

        var id = SelectedScene.Id;
        _main.Document.Scenes.Remove(id);
        Scenes.Remove(SelectedScene);
        _main.DockFactory?.CloseDocument(id);
        _main.MarkDirty();
        SelectedScene = Scenes.FirstOrDefault();
    }

    // ── Population ────────────────────────────────────────────────────────────

    public void LoadFromDocument(TourDocument doc)
    {
        if (_subscribedScene != null)
        {
            _subscribedScene.PropertyChanged -= OnScenePropertyChanged;
            _subscribedScene = null;
        }

        Scenes.Clear();
        foreach (var scene in doc.Scenes.Values)
            Scenes.Add(scene);
        SelectedScene = Scenes.FirstOrDefault();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static Scene CreateSceneFromImagePath(string imagePath)
    {
        var raw = Path.GetFileNameWithoutExtension(imagePath);
        var id = new string(raw.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        if (string.IsNullOrEmpty(id)) id = "Scene";
        return new Scene { Id = id, Image = imagePath, Title = id };
    }

    public static string UniqueId(string baseId, ICollection<string> existingIds)
    {
        if (!existingIds.Contains(baseId)) return baseId;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseId}_{i}";
            if (!existingIds.Contains(candidate)) return candidate;
        }
    }
}
