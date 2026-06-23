using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Zenkei.Models;
using Zenkei.Models.Markers;
using Zenkei.Serialization;
using Zenkei.Services;

namespace Zenkei.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // ── Dock layout ───────────────────────────────────────────────────────────

    public DockFactory? DockFactory { get; private set; }

    [ObservableProperty]
    private IRootDock? _layout;

    // ── Sub-panels ────────────────────────────────────────────────────────────

    public SceneListViewModel  SceneList   { get; private set; }
    public PropertiesViewModel Properties  { get; private set; }

    // ── Document state ────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private TourDocument _document = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    public string WindowTitle => Document.FilePath != null
        ? $"{Path.GetFileName(Document.FilePath)}{(IsDirty ? "*" : "")} — Zenkei"
        : $"Untitled{(IsDirty ? "*" : "")} — Zenkei";

    // Maps sceneId → open editor (so we reuse tabs instead of duplicating)
    private readonly Dictionary<string, PanoramaEditorViewModel> _editors = new();

    // Delegate for showing file dialogs — set by the View
    public Func<string, string[], Task<IEnumerable<string>>>? FilePickerDelegate { get; set; }
    public Func<string, string[], Task<string?>>? SaveFilePickerDelegate { get; set; }
    public Func<string, string[], Task<string?>>? OpenFilePickerDelegate { get; set; }
    public Func<string, string, Task>? ShowErrorDelegate { get; set; }

    public MainWindowViewModel()
    {
        SceneList  = new SceneListViewModel(this);
        Properties = new PropertiesViewModel(this);

        DockFactory = new DockFactory(SceneList, Properties, new OutputViewModel());
        Layout = DockFactory.CreateLayout();
        DockFactory.InitLayout(Layout);
    }

    // ── Document operations ───────────────────────────────────────────────────

    [RelayCommand]
    private void NewDocument()
    {
        _editors.Clear();
        Document = new TourDocument();
        IsDirty = false;
        SceneList.LoadFromDocument(Document);
    }

    [RelayCommand]
    private async Task OpenDocumentAsync()
    {
        var path = await OpenFilePicker("Open Tour File", ["*.yml", "*.yaml"]);
        if (path == null) return;

        try
        {
            var doc = TourYamlSerializer.Load(path);
            _editors.Clear();
            Document = doc;
            IsDirty = false;
            SceneList.LoadFromDocument(doc);
            AppLog.Info($"Opened: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            await ShowError("Open failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveDocumentAsync()
    {
        if (Document.FilePath == null)
        {
            await SaveDocumentAsAsync();
            return;
        }
        await DoSaveAsync(Document.FilePath);
    }

    [RelayCommand]
    private async Task SaveDocumentAsAsync()
    {
        var path = await SaveFilePicker("Save Tour File As…", ["*.yml"]);
        if (path == null) return;
        if (!path.EndsWith(".yml") && !path.EndsWith(".yaml")) path += ".yml";
        await DoSaveAsync(path);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var outDir = await SaveFilePicker("Export to folder (choose any file in target folder)", []);
        if (outDir == null) return;
        outDir = Path.GetDirectoryName(outDir) ?? outDir;
        try
        {
            AppLog.Info($"Exporting to: {outDir}");
            PannellumExporter.Export(Document, outDir, log: msg => AppLog.Info(msg));
            AppLog.Info("Export complete.");
        }
        catch (Exception ex)
        {
            await ShowError("Export failed", ex.Message);
        }
    }

    private async Task DoSaveAsync(string path)
    {
        try
        {
            Document.FilePath = path;
            TourYamlSerializer.Save(Document, path);
            IsDirty = false;
            AppLog.Info($"Saved: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            await ShowError("Save failed", ex.Message);
        }
    }

    // ── Startup file open (command-line arg) ─────────────────────────────────

    public void OpenDocumentFromPath(string path)
    {
        try
        {
            var doc = Serialization.TourYamlSerializer.Load(path);
            _editors.Clear();
            Document = doc;
            IsDirty = false;
            SceneList.LoadFromDocument(doc);
        }
        catch { /* silently ignore bad files on startup */ }
    }

    // ── Scene / tab management ────────────────────────────────────────────────

    public void OpenScene(Scene scene)
    {
        var editor = GetOrCreateEditor(scene);
        DockFactory?.OpenDocument(editor);
    }

    public PanoramaEditorViewModel GetOrCreateEditor(Scene scene)
    {
        if (_editors.TryGetValue(scene.Id, out var existing)) return existing;
        var editor = new PanoramaEditorViewModel(scene, this);
        _editors[scene.Id] = editor;
        return editor;
    }

    public void MarkDirty() => IsDirty = true;

    // ── Scene rename ──────────────────────────────────────────────────────────

    /// <summary>
    /// Renames a scene's internal ID.
    /// Returns null on success; an error message on failure (bad chars, duplicate, empty).
    /// Updates: Scenes dict key, Scene.Id, all SceneMarker cross-references, and the
    /// open editor tab (Title, Id, SceneId) so the tab label refreshes immediately.
    /// </summary>
    public string? TryRenameScene(Scene scene, string newId)
    {
        newId = newId.Trim();
        if (string.IsNullOrEmpty(newId))
            return "ID cannot be empty.";

        if (!newId.All(c => char.IsLetterOrDigit(c) || c == '_'))
            return "ID may only contain letters, digits, and underscores.";

        if (newId == scene.Id) return null; // nothing to do

        if (Document.Scenes.ContainsKey(newId))
            return $"A scene with ID '{newId}' already exists.";

        var oldId = scene.Id;

        Document.Scenes.Remove(oldId);
        scene.Id = newId;
        Document.Scenes[newId] = scene;

        // Update all scene-link markers that point to the old ID.
        foreach (var s in Document.Scenes.Values)
            foreach (var m in s.Markers.OfType<SceneMarker>())
                if (m.TargetScene == oldId)
                    m.TargetScene = newId;

        // Update the open editor tab without requiring a re-open.
        if (_editors.TryGetValue(oldId, out var editor))
        {
            _editors.Remove(oldId);
            editor.SceneId = newId;
            editor.Title = newId;
            editor.Id = $"PanoEditor_{newId}";
            _editors[newId] = editor;
        }

        // Refresh the tree node label now that the ID has changed.
        SceneList.RefreshSceneNodeLabel(scene);

        MarkDirty();
        return null;
    }

    // ── Image path safety ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns a project-relative path for <paramref name="absoluteImagePath"/>,
    /// or the original path when the project is unsaved.
    /// Throws <see cref="ArgumentException"/> when the path escapes the project directory.
    /// </summary>
    public string RelativizeImagePath(string absoluteImagePath)
    {
        if (Document.FilePath == null) return absoluteImagePath;

        var projectDir = Path.GetDirectoryName(Path.GetFullPath(Document.FilePath))!;
        var fullImage  = Path.GetFullPath(absoluteImagePath);
        // Boundary includes the trailing separator so "projectDir_extra" doesn't pass.
        var boundary   = projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;

        if (!fullImage.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Image must be inside the project folder:\n{projectDir}");

        return Path.GetRelativePath(projectDir, fullImage);
    }

    public Task ShowErrorAsync(string title, string message) => ShowError(title, message);

    // ── File dialog helpers ───────────────────────────────────────────────────

    public async Task<IEnumerable<string>> PickFilesAsync(string title, string[] filters)
    {
        if (FilePickerDelegate != null)
            return await FilePickerDelegate(title, filters);
        return [];
    }

    private async Task<string?> OpenFilePicker(string title, string[] filters)
    {
        if (OpenFilePickerDelegate != null)
            return await OpenFilePickerDelegate(title, filters);
        return null;
    }

    private async Task<string?> SaveFilePicker(string title, string[] filters)
    {
        if (SaveFilePickerDelegate != null)
            return await SaveFilePickerDelegate(title, filters);
        return null;
    }

    private async Task ShowError(string title, string message)
    {
        AppLog.Error($"{title}: {message}");
        if (ShowErrorDelegate != null)
            await ShowErrorDelegate(title, message);
    }
}
