using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using PanoTool.Models;
using PanoTool.Serialization;

namespace PanoTool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // ── Dock layout ───────────────────────────────────────────────────────────

    public DockFactory? DockFactory { get; private set; }

    [ObservableProperty]
    private IRootDock? _layout;

    // ── Sub-panels ────────────────────────────────────────────────────────────

    public ScenePanelViewModel ScenePanel { get; private set; }
    public MarkerEditorViewModel MarkerEditor { get; private set; }

    // ── Document state ────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private TourDocument _document = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    public string WindowTitle => Document.FilePath != null
        ? $"{Path.GetFileName(Document.FilePath)}{(IsDirty ? "*" : "")} — PanoTool"
        : $"Untitled{(IsDirty ? "*" : "")} — PanoTool";

    // Maps sceneId → open editor (so we reuse tabs instead of duplicating)
    private readonly Dictionary<string, PanoramaEditorViewModel> _editors = new();

    // Delegate for showing file dialogs — set by the View
    public Func<string, string[], Task<IEnumerable<string>>>? FilePickerDelegate { get; set; }
    public Func<string, string[], Task<string?>>? SaveFilePickerDelegate { get; set; }
    public Func<string, string[], Task<string?>>? OpenFilePickerDelegate { get; set; }
    public Func<string, string, Task>? ShowErrorDelegate { get; set; }

    public MainWindowViewModel()
    {
        ScenePanel = new ScenePanelViewModel(this);
        MarkerEditor = new MarkerEditorViewModel(this);

        DockFactory = new DockFactory(ScenePanel, MarkerEditor);
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
        ScenePanel.LoadFromDocument(Document);
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
            ScenePanel.LoadFromDocument(doc);
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
            PannellumExporter.Export(Document, outDir);
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
        }
        catch (Exception ex)
        {
            await ShowError("Save failed", ex.Message);
        }
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
        if (ShowErrorDelegate != null)
            await ShowErrorDelegate(title, message);
    }
}
