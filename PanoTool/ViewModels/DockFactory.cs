using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace Zenkei.ViewModels;

/// <summary>
/// Builds the IDE-style dock layout:
///   ┌──────────────────────────────────────┐
///   │  Left (Scenes) | Center | Right (Mk) │
///   ├──────────────────────────────────────┤
///   │           Output (bottom)            │
///   └──────────────────────────────────────┘
/// </summary>
public class DockFactory : Factory
{
    private readonly ScenePanelViewModel _scenePanel;
    private readonly MarkerEditorViewModel _markerEditor;
    private readonly OutputViewModel _outputPanel;

    private Dock.Model.Controls.IDocumentDock? _documentDock;

    public DockFactory(
        ScenePanelViewModel scenePanel,
        MarkerEditorViewModel markerEditor,
        OutputViewModel outputPanel)
    {
        _scenePanel = scenePanel;
        _markerEditor = markerEditor;
        _outputPanel = outputPanel;
    }

    public override IRootDock CreateLayout()
    {
        // Left tool pane — scene browser
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

        // Right tool pane — marker editor
        var rightDock = CreateToolDock();
        rightDock.Id = "RightDock";
        rightDock.Title = "Right";
        rightDock.Alignment = Alignment.Right;
        rightDock.Proportion = 0.22;
        rightDock.VisibleDockables = CreateList<IDockable>(_markerEditor);
        rightDock.ActiveDockable = _markerEditor;

        // Bottom tool pane — output log
        var bottomDock = CreateToolDock();
        bottomDock.Id = "BottomDock";
        bottomDock.Title = "Output";
        bottomDock.Alignment = Alignment.Bottom;
        bottomDock.Proportion = 0.22;
        bottomDock.VisibleDockables = CreateList<IDockable>(_outputPanel);
        bottomDock.ActiveDockable = _outputPanel;

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

        // Vertical split: main layout on top, output at bottom
        var verticalLayout = CreateProportionalDock();
        verticalLayout.Id = "VerticalLayout";
        verticalLayout.Title = "Vertical";
        verticalLayout.Orientation = Orientation.Vertical;
        verticalLayout.VisibleDockables = CreateList<IDockable>(
            mainLayout,
            CreateProportionalDockSplitter(),
            bottomDock);

        var root = CreateRootDock();
        root.Id = "Root";
        root.Title = "Root";
        root.VisibleDockables = CreateList<IDockable>(verticalLayout);
        root.DefaultDockable = verticalLayout;
        root.ActiveDockable = verticalLayout;
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
