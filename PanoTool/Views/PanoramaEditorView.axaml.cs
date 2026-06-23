using Avalonia.Controls;
using Avalonia.Interactivity;
using Zenkei.Controls;
using Zenkei.Models.Markers;
using Zenkei.ViewModels;

namespace Zenkei.Views;

public partial class PanoramaEditorView : UserControl
{
    private double _pendingYaw, _pendingPitch;

    // Context menu is built in code so Avalonia's auto-open-on-right-click
    // doesn't fire before we've stored the cursor coordinates.
    private readonly ContextMenu _markerContextMenu;

    public PanoramaEditorView()
    {
        InitializeComponent();

        _markerContextMenu = BuildContextMenu();

        var canvas = this.FindControl<PanoramaCanvas>("Canvas")!;
        canvas.MarkerSelected        += OnMarkerSelected;
        canvas.AddMarkerRequested    += OnAddMarkerRequested;
        canvas.MarkerMoved           += OnCanvasMarkerMoved;
        canvas.InitialViewChanged    += OnCanvasInitialViewChanged;
    }

    private ContextMenu BuildContextMenu()
    {
        var items = new[]
        {
            new MenuItem { Header = "Add Info Marker", Tag = "info"  },
            new MenuItem { Header = "Add Link Marker", Tag = "link"  },
            new MenuItem { Header = "Add Scene Link",  Tag = "scene" },
        };
        foreach (var mi in items)
            mi.Click += OnContextMenuItemClick;

        var cm = new ContextMenu();
        foreach (var mi in items)
            cm.Items.Add(mi);
        return cm;
    }

    private void OnMarkerSelected(MarkerBase? marker)
    {
        if (DataContext is PanoramaEditorViewModel vm)
            vm.SelectedMarker = marker;
    }

    private void OnCanvasMarkerMoved(MarkerBase marker, double yaw, double pitch)
    {
        if (DataContext is PanoramaEditorViewModel vm)
            vm.OnMarkerMoved(yaw, pitch);
    }

    private void OnCanvasInitialViewChanged(double yaw, double pitch)
    {
        if (DataContext is PanoramaEditorViewModel vm)
            vm.OnInitialViewChanged(yaw, pitch);
    }

    private void OnAddMarkerRequested(double yaw, double pitch)
    {
        _pendingYaw   = yaw;
        _pendingPitch = pitch;
        _markerContextMenu.Open(this);
    }

    private void OnContextMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PanoramaEditorViewModel vm) return;
        if (sender is not MenuItem mi) return;

        var type = mi.Tag as string ?? "info";
        MarkerBase newMarker = type switch
        {
            "link"  => new LinkMarker(),
            "scene" => new SceneMarker(),
            _       => new InfoMarker()
        };
        newMarker.Coords = new Zenkei.Models.YawPitch(_pendingYaw, _pendingPitch);

        vm.Scene.Markers.Add(newMarker);
        vm.SelectedMarker = newMarker;

        var canvas = this.FindControl<PanoramaCanvas>("Canvas");
        canvas?.InvalidateVisual();
    }
}
