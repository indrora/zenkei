using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.ViewModels;

/// <summary>
/// Left dock tool — shows the VS-style scene explorer tree with add / remove.
/// Selection changes are observable so PropertiesViewModel can follow them.
/// </summary>
public partial class SceneListViewModel : Tool
{
    private readonly MainWindowViewModel _main;
    private Scene? _subscribedScene;
    private ScenesFolderNode? _scenesFolder;
    private TourSubject? _tourSubject;
    // Guard against SelectedScene ↔ SelectedTreeNode sync loops.
    private bool _syncingTreeScene;

    public ObservableCollection<Scene> Scenes { get; } = [];

    /// Single-element list containing the TourRootNode; bound to TreeView.ItemsSource.
    public ObservableCollection<SceneTreeNode> TreeNodes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedScene))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSceneCommand))]
    private Scene? _selectedScene;

    [ObservableProperty]
    private SceneTreeNode? _selectedTreeNode;

    public bool HasSelectedScene => SelectedScene != null;

    public SceneListViewModel(MainWindowViewModel main)
    {
        _main = main;
        Id = "SceneList";
        Title = "Explorer";
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

        // Push scene to the unified Properties panel.
        _main.Properties.SetScene(value);

        // Sync tree highlight when SelectedScene is set programmatically (add/remove).
        if (!_syncingTreeScene)
        {
            _syncingTreeScene = true;
            try
            {
                var node = _scenesFolder?.Find(value);
                if (node != null && SelectedTreeNode != node)
                    SelectedTreeNode = node;
            }
            finally { _syncingTreeScene = false; }
        }
    }

    partial void OnSelectedTreeNodeChanged(SceneTreeNode? value)
    {
        if (_syncingTreeScene) return;
        _syncingTreeScene = true;
        try
        {
            switch (value)
            {
                case TourRootNode:
                    _tourSubject ??= new TourSubject(_main.Document);
                    _main.Properties.SetTourSubject(_tourSubject);
                    break;

                case ScenesFolderNode:
                    _main.Properties.SetScene(null);
                    break;

                case SceneItemNode sn:
                    SelectedScene = sn.Scene;
                    // Directly update Properties in case SelectedScene didn't change
                    // (OnSelectedSceneChanged is skipped when the value is the same,
                    // e.g. user was viewing a marker and then re-selects the scene node).
                    _main.Properties.SetScene(sn.Scene);
                    break;

                case InitialPovNode ipn:
                    // Open the scene tab, then override the subject with the
                    // thin InitialViewSubject (Yaw/Pitch only).
                    SelectedScene = ipn.RelatedScene;
                    _main.Properties.SetInitialPov(ipn.RelatedScene);
                    break;

                case ImageFileNode ifn:
                    // Show the scene properties (Image path now editable there).
                    SelectedScene = ifn.RelatedScene;
                    break;

                case MarkerItemNode mn:
                    SelectedScene = mn.RelatedScene;
                    // Setting SelectedMarker on the editor triggers Properties.SetMarker via
                    // PanoramaEditorViewModel.OnSelectedMarkerChanged.
                    if (mn.RelatedScene != null)
                    {
                        var editor = _main.GetOrCreateEditor(mn.RelatedScene);
                        editor.SelectedMarker = mn.Marker;
                    }
                    break;
            }
        }
        finally { _syncingTreeScene = false; }
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
            _scenesFolder?.AddScene(new SceneItemNode(scene, this));
            _main.MarkDirty();
            SelectedScene = scene;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedScene))]
    private void RemoveScene()
    {
        if (SelectedScene != null)
            DoRemoveScene(SelectedScene);
    }

    // ── Node action methods (called by tree node commands) ────────────────────

    /// <summary>Prompts for a new ID and renames the scene if valid.</summary>
    internal async Task RenameSceneAsync(Scene scene)
    {
        var newId = await _main.InputDialogAsync("Rename Scene", "New scene ID:", scene.Id);
        if (newId == null) return;
        var err = _main.TryRenameScene(scene, newId);
        if (err != null)
            await _main.ShowErrorAsync("Rename failed", err);
    }

    /// <summary>Opens a file picker and replaces the scene's image path.</summary>
    internal async Task ChangeSceneImageAsync(Scene scene)
    {
        var files = await _main.PickFilesAsync(
            "Select panorama image",
            ["*.jpg", "*.jpeg", "*.png", "*.webp"]);
        var path = files.FirstOrDefault();
        if (string.IsNullOrEmpty(path)) return;

        string imagePath;
        try { imagePath = _main.RelativizeImagePath(path); }
        catch (ArgumentException ex)
        {
            await _main.ShowErrorAsync("Image rejected", ex.Message);
            return;
        }

        scene.Image = imagePath;
        scene.BaseDirectory = _main.Document.FilePath != null
            ? Path.GetDirectoryName(Path.GetFullPath(_main.Document.FilePath))
            : null;
        _main.MarkDirty();
    }

    /// <summary>Removes a marker from its scene and resets Properties to the scene.</summary>
    internal void DeleteMarker(MarkerBase marker, Scene scene)
    {
        scene.Markers.Remove(marker);
        _main.Properties.SetScene(scene);
        _main.MarkDirty();
    }

    /// <summary>Called from SceneItemNode.RemoveCommand — removes a specific scene.</summary>
    internal void RemoveSceneFor(Scene scene) => DoRemoveScene(scene);

    private void DoRemoveScene(Scene scene)
    {
        if (_subscribedScene == scene)
        {
            scene.PropertyChanged -= OnScenePropertyChanged;
            _subscribedScene = null;
        }

        var treeNode = _scenesFolder?.Find(scene);
        var id = scene.Id;

        _main.Document.Scenes.Remove(id);
        Scenes.Remove(scene);
        if (treeNode != null) _scenesFolder?.RemoveScene(treeNode);

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

        // Invalidate the cached TourSubject so a fresh one wraps the new document.
        _tourSubject = null;

        Scenes.Clear();
        foreach (var scene in doc.Scenes.Values)
            Scenes.Add(scene);

        // Build the project tree.
        _scenesFolder = new ScenesFolderNode();
        foreach (var scene in doc.Scenes.Values)
            _scenesFolder.AddScene(new SceneItemNode(scene, this));

        var root = new TourRootNode(doc);
        root.Children.Add(_scenesFolder);

        TreeNodes.Clear();
        TreeNodes.Add(root);

        SelectedScene = Scenes.FirstOrDefault();
    }

    // ── Tree helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes the tree label for a scene node after an ID rename.
    /// Called by <see cref="MainWindowViewModel.TryRenameScene"/> on success.
    /// </summary>
    public void RefreshSceneNodeLabel(Scene scene) =>
        _scenesFolder?.Find(scene)?.RefreshLabel();

    // ── Static helpers ────────────────────────────────────────────────────────

    internal static Scene CreateSceneFromImagePath(string imagePath)
    {
        var raw = Path.GetFileNameWithoutExtension(imagePath);
        var id  = new string(raw.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
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
