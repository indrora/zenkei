using Avalonia.Controls;
using Avalonia.Interactivity;
using PanoTool.Controls;
using PanoTool.Models.Markers;
using PanoTool.ViewModels;

namespace PanoTool.Views;

public partial class PanoramaEditorView : UserControl
{
    private double _pendingYaw, _pendingPitch;

    public PanoramaEditorView()
    {
        InitializeComponent();

        var canvas = this.FindControl<PanoramaCanvas>("Canvas")!;
        canvas.MarkerSelected += OnMarkerSelected;
        canvas.AddMarkerRequested += OnAddMarkerRequested;

        // Wire context menu items
        if (this.FindControl<ContextMenu>("CanvasContextMenu") is { } cm)
        {
            foreach (MenuItem mi in cm.Items.OfType<MenuItem>())
                mi.Click += OnContextMenuItemClick;
        }
    }

    private void OnMarkerSelected(MarkerBase? marker)
    {
        if (DataContext is PanoramaEditorViewModel vm)
            vm.SelectedMarker = marker;
    }

    private void OnAddMarkerRequested(double yaw, double pitch)
    {
        _pendingYaw = yaw;
        _pendingPitch = pitch;
        // Show context menu so user picks marker type
        if (this.FindControl<ContextMenu>("CanvasContextMenu") is { } cm)
            cm.Open(this);
    }

    private void OnContextMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PanoramaEditorViewModel vm) return;
        if (sender is not MenuItem mi) return;

        var type = mi.Tag as string ?? "info";
        MarkerBase newMarker = type switch
        {
            "link" => new LinkMarker(),
            "scene" => new SceneMarker(),
            _ => new InfoMarker()
        };
        newMarker.Coords = [_pendingYaw, _pendingPitch];

        vm.Scene.Markers.Add(newMarker);
        vm.SelectedMarker = newMarker;

        var canvas = this.FindControl<PanoramaCanvas>("Canvas");
        canvas?.InvalidateVisual();
    }
}
