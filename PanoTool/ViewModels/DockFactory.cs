using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace PanoTool.ViewModels;

/// <summary>
/// Builds the three-pane IDE-style dock layout:
///   Left (Scenes) | Center (Document tabs) | Right (Marker editor)
/// </summary>
public class DockFactory : Factory
{
    private readonly ScenePanelViewModel _scenePanel;
    private readonly MarkerEditorViewModel _markerEditor;

    private Dock.Model.Controls.IDocumentDock? _documentDock;

    public DockFactory(ScenePanelViewModel scenePanel, MarkerEditorViewModel markerEditor)
    {
        _scenePanel = scenePanel;
        _markerEditor = markerEditor;
    }

    public override IRootDock CreateLayout()
    {
        // Left tool pane — scene browser + scene metadata form
        var leftDock = CreateToolDock();
        leftDock.Id = "LeftDock";
        leftDock.Title = "Left";
        leftDock.Alignment = Alignment.Left;
        leftDock.Proportion = 0.22;
        leftDock.VisibleDockables = CreateList<IDockable>(_scenePanel);
        leftDock.ActiveDockable = _scenePanel;

        // Center document area — panorama editor tabs open here
        var docDock = CreateDocumentDock();
        docDock.Id = "DocumentDock";
        docDock.Title = "Documents";
        docDock.IsCollapsable = false;
        docDock.VisibleDockables = CreateList<IDockable>();
        _documentDock = docDock;

        // Right tool pane — marker property editor
        var rightDock = CreateToolDock();
        rightDock.Id = "RightDock";
        rightDock.Title = "Right";
        rightDock.Alignment = Alignment.Right;
        rightDock.Proportion = 0.22;
        rightDock.VisibleDockables = CreateList<IDockable>(_markerEditor);
        rightDock.ActiveDockable = _markerEditor;

        // Horizontal split: Left | Center | Right
        var mainLayout = CreateProportionalDock();
        mainLayout.Id = "MainLayout";
        mainLayout.Title = "Main";
        mainLayout.Orientation = Orientation.Horizontal;
        mainLayout.VisibleDockables = CreateList<IDockable>(
            leftDock,
            CreateProportionalDockSplitter(),
            docDock,
            CreateProportionalDockSplitter(),
            rightDock);

        var root = CreateRootDock();
        root.Id = "Root";
        root.Title = "Root";
        root.VisibleDockables = CreateList<IDockable>(mainLayout);
        root.DefaultDockable = mainLayout;
        root.ActiveDockable = mainLayout;
        return root;
    }

    /// <summary>
    /// Opens a panorama editor tab. If a tab for this scene already exists, activates it.
    /// </summary>
    public void OpenDocument(PanoramaEditorViewModel doc)
    {
        if (_documentDock is null) return;

        var existing = _documentDock.VisibleDockables?
            .OfType<PanoramaEditorViewModel>()
            .FirstOrDefault(d => d.SceneId == doc.SceneId);

        if (existing != null)
        {
            _documentDock.ActiveDockable = existing;
        }
        else
        {
            _documentDock.VisibleDockables ??= CreateList<IDockable>();
            AddDockable(_documentDock, doc);
            SetActiveDockable(doc);
        }
    }

    /// <summary>
    /// Removes the editor tab for a scene that has been deleted.
    /// </summary>
    public void CloseDocument(string sceneId)
    {
        if (_documentDock?.VisibleDockables is null) return;
        var existing = _documentDock.VisibleDockables
            .OfType<PanoramaEditorViewModel>()
            .FirstOrDefault(d => d.SceneId == sceneId);
        if (existing != null)
            RemoveDockable(existing, false);
    }
}
