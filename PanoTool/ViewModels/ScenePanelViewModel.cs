using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;

namespace Zenkei.ViewModels;

/// <summary>
/// Left dock tool — shows the VS-style scene explorer tree with add / remove.
/// Selection changes are observable so ScenePropertiesViewModel can follow them.
/// </summary>
public partial class SceneListViewModel : Tool
{
    private readonly MainWindowViewModel _main;
    private Scene? _subscribedScene;
    private ScenesFolderNode? _scenesFolder;
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
                case SceneItemNode sn:
                    SelectedScene = sn.Scene;
                    break;

                case InitialPovNode ipn:
                    // Open the scene tab, then override the subject with the
                    // thin InitialViewSubject (Yaw/Pitch only).
                    SelectedScene = ipn.RelatedScene;
                    _main.Properties.SetInitialPov(ipn.RelatedScene);
                    break;

                case ImageFileNode ifn:
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
            _scenesFolder?.AddScene(new SceneItemNode(scene));
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

        var treeNode = _scenesFolder?.Find(SelectedScene);
        var id = SelectedScene.Id;

        _main.Document.Scenes.Remove(id);
        Scenes.Remove(SelectedScene);
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

        Scenes.Clear();
        foreach (var scene in doc.Scenes.Values)
            Scenes.Add(scene);

        // Build the project tree.
        _scenesFolder = new ScenesFolderNode();
        foreach (var scene in doc.Scenes.Values)
            _scenesFolder.AddScene(new SceneItemNode(scene));

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
