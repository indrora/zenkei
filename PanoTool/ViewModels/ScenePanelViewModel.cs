using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.ViewModels;

/// <summary>
/// Left dock tool: scene list + scene metadata form.
/// </summary>
public partial class ScenePanelViewModel : Tool
{
    private readonly MainWindowViewModel _main;

    public ObservableCollection<Scene> Scenes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedScene))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSceneCommand))]
    private Scene? _selectedScene;

    public bool HasSelectedScene => SelectedScene != null;

    // ── Scene metadata fields (bound to form) ────────────────────────────────

    [ObservableProperty] private string _sceneTitle = "";
    [ObservableProperty] private string _sceneDescription = "";
    [ObservableProperty] private string _sceneImage = "";
    [ObservableProperty] private double _sceneHFov = 120;
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
        if (value == null) return;
        // Sync metadata form
        SceneTitle = value.Title;
        SceneDescription = value.Description ?? "";
        SceneImage = value.Image;
        SceneHFov = value.HFov ?? _main.Document.Default.HFov;
        SceneInitialX = value.Initial[0];
        SceneInitialY = value.Initial[1];
        // Open editor tab
        _main.OpenScene(value);
    }

    // ── Scene metadata write-back ─────────────────────────────────────────────

    partial void OnSceneTitleChanged(string value)
    {
        if (_selectedScene == null) return;
        _selectedScene.Title = value;
        _main.DockFactory?.OpenDocument(
            _main.GetOrCreateEditor(_selectedScene)); // refresh tab title
        _main.GetOrCreateEditor(_selectedScene).RefreshTitle();
        _main.MarkDirty();
    }

    partial void OnSceneDescriptionChanged(string value)
    {
        if (_selectedScene == null) return;
        _selectedScene.Description = string.IsNullOrEmpty(value) ? null : value;
        _main.MarkDirty();
    }

    partial void OnSceneHFovChanged(double value)
    {
        if (_selectedScene == null) return;
        _selectedScene.HFov = value;
        _main.MarkDirty();
    }

    partial void OnSceneInitialXChanged(double value)
    {
        if (_selectedScene == null) return;
        _selectedScene.Initial[0] = value;
        _main.MarkDirty();
    }

    partial void OnSceneInitialYChanged(double value)
    {
        if (_selectedScene == null) return;
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
        var id = SelectedScene.Id;
        _main.Document.Scenes.Remove(id);
        Scenes.Remove(SelectedScene);
        _main.DockFactory?.CloseDocument(id);
        _main.MarkDirty();
        SelectedScene = Scenes.FirstOrDefault();
    }

    // ── Population ────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the scene list from the loaded document.
    /// </summary>
    public void LoadFromDocument(TourDocument doc)
    {
        Scenes.Clear();
        foreach (var scene in doc.Scenes.Values)
            Scenes.Add(scene);
        SelectedScene = Scenes.FirstOrDefault();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static Scene CreateSceneFromImagePath(string imagePath)
    {
        var raw = Path.GetFileNameWithoutExtension(imagePath);
        // Sanitise: replace non-alphanumeric with _
        var id = new string(raw.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        if (string.IsNullOrEmpty(id)) id = "Scene";
        return new Scene
        {
            Id = id,
            Image = imagePath,
            Title = id
        };
    }

    /// <summary>
    /// Ensures scene IDs are unique within a document by appending _2, _3, etc.
    /// </summary>
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
