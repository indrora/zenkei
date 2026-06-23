using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;

namespace Zenkei.ViewModels;

/// <summary>
/// Left dock tool — shows the scene list with add / remove.
/// Selection changes are observable so ScenePropertiesViewModel can follow them.
/// </summary>
public partial class SceneListViewModel : Tool
{
    private readonly MainWindowViewModel _main;
    private Scene? _subscribedScene;

    public ObservableCollection<Scene> Scenes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedScene))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSceneCommand))]
    private Scene? _selectedScene;

    public bool HasSelectedScene => SelectedScene != null;

    public SceneListViewModel(MainWindowViewModel main)
    {
        _main = main;
        Id = "SceneList";
        Title = "Scenes";
        CanClose = false;
        CanPin = true;
        CanFloat = true;
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    partial void OnSelectedSceneChanged(Scene? value)
    {
        if (_subscribedScene != null)
            _subscribedScene.PropertyChanged -= OnScenePropertyChanged;
        _subscribedScene = value;

        if (value == null) return;
        value.PropertyChanged += OnScenePropertyChanged;
        _main.OpenScene(value);
    }

    private void OnScenePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _main.MarkDirty();
        if (e.PropertyName == nameof(Scene.Title) && SelectedScene != null)
            _main.GetOrCreateEditor(SelectedScene).RefreshTitle();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddImageAsync()
    {
        var files = await _main.PickFilesAsync(
            "Select panorama image(s)",
            ["*.jpg", "*.jpeg", "*.png", "*.webp"]);

        var projectBaseDir = _main.Document.FilePath != null
            ? Path.GetDirectoryName(Path.GetFullPath(_main.Document.FilePath))
            : null;

        foreach (var path in files)
        {
            string imagePath;
            try { imagePath = _main.RelativizeImagePath(path); }
            catch (ArgumentException ex)
            {
                await _main.ShowErrorAsync("Image rejected", ex.Message);
                continue;
            }

            var scene = CreateSceneFromImagePath(imagePath);
            scene.BaseDirectory = projectBaseDir;
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
