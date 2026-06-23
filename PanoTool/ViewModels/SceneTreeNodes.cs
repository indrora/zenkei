using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.ViewModels;

/// <summary>
/// Abstract base for all VS-style solution-explorer tree nodes
/// displayed in the scene panel TreeView.
/// </summary>
public abstract class SceneTreeNode : ObservableObject
{
    /// Short icon glyph rendered before the label.
    public abstract string Icon  { get; }
    public abstract string Label { get; }
    /// Non-null for expandable nodes; null for leaves (TreeView omits the expand arrow).
    public virtual ObservableCollection<SceneTreeNode>? Children => null;
    // Helpers used by the tree selection handler.
    public virtual Scene?      RelatedScene  => null;
    public virtual MarkerBase? RelatedMarker => null;
}

// ── Root: the tour ─────────────────────────────────────────────────────────────

/// <summary>Top-level project node that shows the tour title.</summary>
public sealed class TourRootNode : SceneTreeNode
{
    private readonly TourDocument _doc;
    public override string Icon  => "◆";
    public override string Label => _doc.Information.Title is { Length: > 0 } t ? t : "Untitled Tour";
    public override ObservableCollection<SceneTreeNode> Children { get; } = [];

    public TourRootNode(TourDocument doc) => _doc = doc;
}

// ── "Scenes" folder ────────────────────────────────────────────────────────────

/// <summary>"Scenes (N)" container node directly under the tour root.</summary>
public sealed class ScenesFolderNode : SceneTreeNode
{
    public override string Icon  => "";
    public override string Label => $"Scenes ({Children.Count})";
    public override ObservableCollection<SceneTreeNode> Children { get; } = [];

    public void AddScene(SceneItemNode node)
    {
        Children.Add(node);
        OnPropertyChanged(nameof(Label));
    }

    public void RemoveScene(SceneItemNode node)
    {
        Children.Remove(node);
        OnPropertyChanged(nameof(Label));
    }

    /// Finds the tree node for a given scene, or null.
    public SceneItemNode? Find(Scene scene) =>
        Children.OfType<SceneItemNode>().FirstOrDefault(n => n.Scene == scene);
}

// ── Scene item ─────────────────────────────────────────────────────────────────

/// <summary>One scene entry, expandable to its image file and marker list.</summary>
public sealed class SceneItemNode : SceneTreeNode
{
    public Scene Scene { get; }
    public override string Icon => ""; // rendered as a small coloured square in AXAML

    // Manual override because [ObservableProperty] cannot produce an override accessor.
    private string _label;
    public override string Label => _label;

    public override ObservableCollection<SceneTreeNode> Children { get; } = [];
    public override Scene? RelatedScene => Scene;

    public SceneItemNode(Scene scene)
    {
        Scene  = scene;
        _label = scene.Id;

        // Child 0: initial POV, Child 1: image file, then markers
        Children.Add(new InitialPovNode(scene));
        Children.Add(new ImageFileNode(scene));

        scene.PropertyChanged           += OnScenePropertyChanged;
        scene.Markers.CollectionChanged += OnMarkersChanged;

        foreach (var m in scene.Markers)
            Children.Add(new MarkerItemNode(m, scene));
    }

    /// Called by SceneListViewModel after a rename to keep the label current.
    public void RefreshLabel()
    {
        _label = Scene.Id;
        OnPropertyChanged(nameof(Label));
    }

    private void OnScenePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Scene.Image)) return;
        Children.OfType<ImageFileNode>().FirstOrDefault()?.RefreshLabel();
    }

    private void OnMarkersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                foreach (MarkerBase m in e.NewItems)
                    Children.Add(new MarkerItemNode(m, Scene));
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                foreach (MarkerBase m in e.OldItems)
                {
                    var node = Children.OfType<MarkerItemNode>()
                        .FirstOrDefault(n => n.Marker == m);
                    if (node != null) Children.Remove(node);
                }
                break;

            default:
                // Replace (marker type switch), Reset, Move — full rebuild.
                foreach (var mn in Children.OfType<MarkerItemNode>().ToList())
                    Children.Remove(mn);
                foreach (var m in Scene.Markers)
                    Children.Add(new MarkerItemNode(m, Scene));
                break;
        }
    }
}

// ── Initial POV synthetic node ─────────────────────────────────────────────────

/// <summary>
/// Synthetic leaf representing the scene's initial viewpoint.
/// Selecting it shows the scene properties (including initial-view NUDs).
/// Not serialised as a YAML marker.
/// </summary>
public sealed class InitialPovNode : SceneTreeNode
{
    private readonly Scene _scene;
    public override string Icon  => "◎";
    public override string Label => "Initial POV";
    public override Scene? RelatedScene => _scene;
    public InitialPovNode(Scene scene) => _scene = scene;
}

// ── Image file item ────────────────────────────────────────────────────────────

/// <summary>Leaf node showing the scene's image filename.</summary>
public sealed class ImageFileNode : SceneTreeNode
{
    private readonly Scene _scene;
    private string _label;

    public override string Icon => "▣";
    public override string Label => _label;
    public override Scene? RelatedScene => _scene;

    public ImageFileNode(Scene scene)
    {
        _scene = scene;
        _label = GetFilename();
    }

    public void RefreshLabel()
    {
        _label = GetFilename();
        OnPropertyChanged(nameof(Label));
    }

    private string GetFilename() =>
        string.IsNullOrEmpty(_scene.Image) ? "(no image)" : Path.GetFileName(_scene.Image);
}

// ── Marker item ────────────────────────────────────────────────────────────────

/// <summary>Leaf node for one marker; icon and colour reflect the marker type.</summary>
public sealed class MarkerItemNode : SceneTreeNode
{
    public  MarkerBase Marker { get; }
    private readonly Scene _scene;

    public override string Icon => Marker switch
    {
        InfoMarker  => "ⓘ",
        SceneMarker => "⇒",
        LinkMarker  => "↗",
        _            => "●"
    };

    // Static brushes — avoids allocation per render pass.
    private static readonly IBrush InfoBrush  = new SolidColorBrush(Color.Parse("#9E9E9E"));
    private static readonly IBrush SceneBrush = new SolidColorBrush(Color.Parse("#66BB6A"));
    private static readonly IBrush LinkBrush  = new SolidColorBrush(Color.Parse("#42A5F5"));

    public IBrush IconBrush => Marker switch
    {
        InfoMarker  => InfoBrush,
        SceneMarker => SceneBrush,
        LinkMarker  => LinkBrush,
        _            => InfoBrush
    };

    public override string Label => Marker switch
    {
        InfoMarker  im => Trunc(im.Text  ?? "", 40),
        SceneMarker sm => $"→ {sm.TargetScene}",
        LinkMarker  lm => $"↗ {TruncUrl(lm.Url ?? "")}",
        _              => "Marker"
    };

    public override Scene?      RelatedScene  => _scene;
    public override MarkerBase? RelatedMarker => Marker;

    public MarkerItemNode(MarkerBase marker, Scene scene) { Marker = marker; _scene = scene; }

    private static string Trunc(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string TruncUrl(string url)
    {
        try { return new Uri(url).Host; }
        catch { return Trunc(url, 40); }
    }
}
