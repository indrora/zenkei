using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace Zenkei.ViewModels;

/// <summary>
/// Builds the IDE-style dock layout:
///   ┌──────────────────────────────────────────────┐
///   │  Explorer (left) | Center | Properties (right)│
///   ├──────────────────────────────────────────────┤
///   │               Output (bottom)                 │
///   └──────────────────────────────────────────────┘
/// </summary>
public class DockFactory : Factory
{
    private readonly SceneListViewModel  _sceneList;
    private readonly PropertiesViewModel _properties;
    private readonly OutputViewModel     _outputPanel;

    private Dock.Model.Controls.IDocumentDock? _documentDock;

    public DockFactory(
        SceneListViewModel  sceneList,
        PropertiesViewModel properties,
        OutputViewModel     outputPanel)
    {
        _sceneList   = sceneList;
        _properties  = properties;
        _outputPanel = outputPanel;
    }

    public override IRootDock CreateLayout()
    {
        // Left tool pane — scene explorer tree, full height
        var sceneListDock = CreateToolDock();
        sceneListDock.Id         = "SceneListDock";
        sceneListDock.Title      = "Explorer";
        sceneListDock.Alignment  = Alignment.Left;
        sceneListDock.Proportion = 0.25;
        sceneListDock.VisibleDockables = CreateList<IDockable>(_sceneList);
        sceneListDock.ActiveDockable   = _sceneList;

        // Center document area — panorama editor tabs open here
        var docDock = CreateDocumentDock();
        docDock.Id           = "DocumentDock";
        docDock.Title        = "Documents";
        docDock.IsCollapsable = false;
        docDock.VisibleDockables = CreateList<IDockable>();
        _documentDock = docDock;

        // Right tool pane — unified Properties panel
        var propertiesDock = CreateToolDock();
        propertiesDock.Id         = "PropertiesDock";
        propertiesDock.Title      = "Properties";
        propertiesDock.Alignment  = Alignment.Right;
        propertiesDock.Proportion = 0.25;
        propertiesDock.VisibleDockables = CreateList<IDockable>(_properties);
        propertiesDock.ActiveDockable   = _properties;

        // Bottom tool pane — output log
        var bottomDock = CreateToolDock();
        bottomDock.Id         = "BottomDock";
        bottomDock.Title      = "Output";
        bottomDock.Alignment  = Alignment.Bottom;
        bottomDock.Proportion = 0.22;
        bottomDock.VisibleDockables = CreateList<IDockable>(_outputPanel);
        bottomDock.ActiveDockable   = _outputPanel;

        // Horizontal split: Explorer | Documents | Properties
        var mainLayout = CreateProportionalDock();
        mainLayout.Id          = "MainLayout";
        mainLayout.Title       = "Main";
        mainLayout.Orientation = Orientation.Horizontal;
        mainLayout.VisibleDockables = CreateList<IDockable>(
            sceneListDock,
            CreateProportionalDockSplitter(),
            docDock,
            CreateProportionalDockSplitter(),
            propertiesDock);

        // Vertical split: main on top, output at bottom
        var verticalLayout = CreateProportionalDock();
        verticalLayout.Id          = "VerticalLayout";
        verticalLayout.Title       = "Vertical";
        verticalLayout.Orientation = Orientation.Vertical;
        verticalLayout.VisibleDockables = CreateList<IDockable>(
            mainLayout,
            CreateProportionalDockSplitter(),
            bottomDock);

        var root = CreateRootDock();
        root.Id             = "Root";
        root.Title          = "Root";
        root.VisibleDockables = CreateList<IDockable>(verticalLayout);
        root.DefaultDockable = verticalLayout;
        root.ActiveDockable  = verticalLayout;
        return root;
    }

    /// <summary>
    /// Prevents tool panes from being dragged into the center document area.
    /// </summary>
    public override void MoveDockable(IDock sourceDock, IDock targetDock, IDockable sourceDockable, IDockable? targetDockable)
    {
        if (targetDock is IDocumentDock && sourceDockable is not IDocument)
            return;
        base.MoveDockable(sourceDock, targetDock, sourceDockable, targetDockable);
    }

    /// <summary>
    /// Opens a panorama editor tab.  Reuses an existing tab for the same scene.
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

    /// <summary>Removes the editor tab for a scene that has been deleted.</summary>
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
