using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Zenkei.Models;

namespace Zenkei.ViewModels;

/// <summary>
/// Left dock tool — shows metadata and the initial-viewpoint controls for the
/// currently selected scene.  Follows SceneListViewModel.SelectedScene.
/// </summary>
public partial class ScenePropertiesViewModel : Tool
{
    private readonly MainWindowViewModel _main;
    private bool _syncingScene;

    /// <summary>The scene bound to the PropertyGrid.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameSceneCommand))]
    private Scene? _selectedScene;

    // Initial view is a double[] so we keep manual fields rather than exposing
    // the array directly to the PropertyGrid.
    [ObservableProperty] private double _sceneInitialX;
    [ObservableProperty] private double _sceneInitialY;

    /// <summary>Editable copy of the scene ID shown in the rename TextBox.</summary>
    [ObservableProperty] private string _editingSceneId = "";

    public bool HasSelectedScene => SelectedScene != null;

    public ScenePropertiesViewModel(MainWindowViewModel main, SceneListViewModel sceneList)
    {
        _main = main;
        Id = "SceneProperties";
        Title = "Scene";
        CanClose = false;
        CanPin = true;
        CanFloat = true;

        // Mirror the list selection so we always show the active scene's data.
        sceneList.PropertyChanged += OnListPropertyChanged;
    }

    private void OnListPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SceneListViewModel.SelectedScene)) return;
        var scene = (sender as SceneListViewModel)?.SelectedScene;
        SelectedScene = scene;
        OnPropertyChanged(nameof(HasSelectedScene));
        if (scene != null)
        {
            SyncInitialView(scene.Initial[0], scene.Initial[1]);
            EditingSceneId = scene.Id;
        }
        else
        {
            EditingSceneId = "";
        }
    }

    /// <summary>
    /// Syncs the NumericUpDown fields without triggering write-back to Initial[].
    /// Called both when a scene is selected and when the canvas initial-view marker
    /// is dragged.
    /// </summary>
    public void SyncInitialView(double yaw, double pitch)
    {
        _syncingScene = true;
        try { SceneInitialX = yaw; SceneInitialY = pitch; }
        finally { _syncingScene = false; }
    }

    // ── Rename scene ID ───────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelectedScene))]
    private async Task RenameSceneAsync()
    {
        if (SelectedScene == null) return;

        var error = _main.TryRenameScene(SelectedScene, EditingSceneId);
        if (error != null)
        {
            EditingSceneId = SelectedScene.Id; // revert to current ID
            await _main.ShowErrorAsync("Rename failed", error);
        }
        // On success EditingSceneId already matches the new scene.Id
    }

    // ── Change image ──────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelectedScene))]
    private async Task ChangeImageAsync()
    {
        if (SelectedScene == null) return;

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

        SelectedScene.Image = imagePath;
        // Keep BaseDirectory in sync so ResolvedImagePath stays correct.
        SelectedScene.BaseDirectory = _main.Document.FilePath != null
            ? Path.GetDirectoryName(Path.GetFullPath(_main.Document.FilePath))
            : null;
        _main.MarkDirty();
    }

    // ── Write-back when user edits the NumericUpDowns ─────────────────────────

    partial void OnSceneInitialXChanged(double value)
    {
        if (_syncingScene || SelectedScene == null) return;
        SelectedScene.Initial[0] = value;
        _main.MarkDirty();
    }

    partial void OnSceneInitialYChanged(double value)
    {
        if (_syncingScene || SelectedScene == null) return;
        SelectedScene.Initial[1] = value;
        _main.MarkDirty();
    }
}
