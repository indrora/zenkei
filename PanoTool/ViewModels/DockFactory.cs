using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace Zenkei.ViewModels;

/// <summary>
/// Builds the IDE-style dock layout:
///   ┌────────────────────────────────────────────┐
///   │  Left (List / Props) | Center | Right (Mk) │
///   ├────────────────────────────────────────────┤
///   │             Output (bottom)                │
///   └────────────────────────────────────────────┘
/// </summary>
public class DockFactory : Factory
{
    private readonly SceneListViewModel _sceneList;
    private readonly ScenePropertiesViewModel _sceneProperties;
    private readonly MarkerEditorViewModel _markerEditor;
    private readonly OutputViewModel _outputPanel;

    private Dock.Model.Controls.IDocumentDock? _documentDock;

    public DockFactory(
        SceneListViewModel sceneList,
        ScenePropertiesViewModel sceneProperties,
        MarkerEditorViewModel markerEditor,
        OutputViewModel outputPanel)
    {
        _sceneList = sceneList;
        _sceneProperties = sceneProperties;
        _markerEditor = markerEditor;
        _outputPanel = outputPanel;
    }

    public override IRootDock CreateLayout()
    {
        // Left area — two separate ToolDocks stacked vertically so they never tab-merge.
        // Explorer tree gets 2/3 of the vertical space; Properties panel gets 1/3.
        var sceneListDock = CreateToolDock();
        sceneListDock.Id = "SceneListDock";
        sceneListDock.Title = "Explorer";
        sceneListDock.Alignment = Alignment.Left;
        sceneListDock.Proportion = 0.65;
        sceneListDock.VisibleDockables = CreateList<IDockable>(_sceneList);
        sceneListDock.ActiveDockable = _sceneList;

        var scenePropsDock = CreateToolDock();
        scenePropsDock.Id = "ScenePropsDock";
        scenePropsDock.Title = "Scene Properties";
        scenePropsDock.Alignment = Alignment.Left;
        scenePropsDock.Proportion = 0.35;
        scenePropsDock.VisibleDockables = CreateList<IDockable>(_sceneProperties);
        scenePropsDock.ActiveDockable = _sceneProperties;

        var leftArea = CreateProportionalDock();
        leftArea.Id = "LeftArea";
        leftArea.Title = "Left";
        leftArea.Orientation = Orientation.Vertical;
        leftArea.Proportion = 0.25;   // slightly wider to show the richer tree
        leftArea.VisibleDockables = CreateList<IDockable>(
            sceneListDock,
            CreateProportionalDockSplitter(),
            scenePropsDock);

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

        // Horizontal split: Left area | Center | Right
        var mainLayout = CreateProportionalDock();
        mainLayout.Id = "MainLayout";
        mainLayout.Title = "Main";
        mainLayout.Orientation = Orientation.Horizontal;
        mainLayout.VisibleDockables = CreateList<IDockable>(
            leftArea,
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
    /// Prevents tool panes from being dragged into the center document area.
    /// Documents can still be moved freely between document docks.
    /// </summary>
    public override void MoveDockable(IDock sourceDock, IDock targetDock, IDockable sourceDockable, IDockable? targetDockable)
    {
        if (targetDock is IDocumentDock && sourceDockable is not IDocument)
            return;
        base.MoveDockable(sourceDock, targetDock, sourceDockable, targetDockable);
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
