using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.Controls;

/// <summary>
/// Renders a 2:1 equirectangular panorama image using Skia and allows
/// placing / moving markers interactively.
///
/// Coordinate system:
///   Internal: (yaw_rad, pitch_rad) where yaw ∈ [-π, π], pitch ∈ [0, π] top-to-bottom.
///   Pixel:    (px, py) in image space.
///   Canvas:   pixel position after applying the pan/zoom transform.
/// </summary>
public class PanoramaCanvas : Control
{
    // ── Avalonia properties ──────────────────────────────────────────────────

    public static readonly StyledProperty<Scene?> SceneProperty =
        AvaloniaProperty.Register<PanoramaCanvas, Scene?>(nameof(Scene));

    public static readonly StyledProperty<MarkerBase?> SelectedMarkerProperty =
        AvaloniaProperty.Register<PanoramaCanvas, MarkerBase?>(nameof(SelectedMarker));

    public Scene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public MarkerBase? SelectedMarker
    {
        get => GetValue(SelectedMarkerProperty);
        set => SetValue(SelectedMarkerProperty, value);
    }

    // ── Events ───────────────────────────────────────────────────────────────

    public event Action<MarkerBase?>? MarkerSelected;
    public event Action<double, double>? AddMarkerRequested; // (yaw, pitch)

    // ── Private state ─────────────────────────────────────────────────────────

    private SKBitmap? _bitmap;
    private string? _loadedImagePath;

    // pan+zoom — image pixel (x,y) → canvas (x*_scale + _offX, y*_scale + _offY)
    private float _scale = 1f;
    private float _offX, _offY;

    private bool _isPanning;
    private Point _panStart;
    private float _panOffXStart, _panOffYStart;
    private bool _spaceHeld;

    private MarkerBase? _dragging;
    private bool _dragMoved;
    private Point _dragStart;

    // Pre-rendered marker icons (24×24)
    private readonly Dictionary<string, SKBitmap> _iconCache = new();

    private const float MarkerRadius = 14f;
    private const float HitRadius = 18f;

    // ── Property change reactions ─────────────────────────────────────────────

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SceneProperty)
        {
            LoadSceneImage();
            InvalidateVisual();
        }
        else if (change.Property == SelectedMarkerProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focusable = true;
        LoadSceneImage();
    }

    private void LoadSceneImage()
    {
        var path = Scene?.Image;
        if (path == _loadedImagePath) return;
        _loadedImagePath = path;
        _bitmap?.Dispose();
        _bitmap = null;

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                _bitmap = SKBitmap.Decode(path);
                ResetTransform();
            }
            catch { /* ignore bad images */ }
        }
    }

    private void ResetTransform()
    {
        if (_bitmap == null || Bounds.Width == 0 || Bounds.Height == 0) return;
        var scaleX = (float)(Bounds.Width / _bitmap.Width);
        var scaleY = (float)(Bounds.Height / _bitmap.Height);
        _scale = Math.Min(scaleX, scaleY);
        _offX = (float)((Bounds.Width - _bitmap.Width * _scale) / 2f);
        _offY = (float)((Bounds.Height - _bitmap.Height * _scale) / 2f);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_bitmap != null) ResetTransform();
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        context.Custom(new PanoramaDrawOp(this, new Rect(Bounds.Size)));
        // Request continuous repaint so dragging stays smooth
        Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual, Avalonia.Threading.DispatcherPriority.Background);
    }

    internal void RenderSkia(SKCanvas canvas)
    {
        canvas.Clear(new SKColor(40, 40, 40));

        if (_bitmap == null)
        {
            using var noImgFont = new SKFont(SKTypeface.Default, 18);
            using var noImgPaint = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
            canvas.DrawText("No image loaded — select a scene",
                (float)(Bounds.Width / 2), (float)(Bounds.Height / 2),
                SKTextAlign.Center, noImgFont, noImgPaint);
            return;
        }

        // Draw panorama image
        canvas.Save();
        canvas.Translate(_offX, _offY);
        canvas.Scale(_scale);
        canvas.DrawBitmap(_bitmap, SKPoint.Empty);
        canvas.Restore();

        // Draw markers
        if (Scene != null)
        {
            foreach (var m in Scene.Markers)
                DrawMarker(canvas, m, m == SelectedMarker);

            // Draw initial-view crosshair
            var (cx, cy) = CoordsToCanvas(Scene.Initial[0], Scene.Initial[1]);
            DrawCrosshair(canvas, cx, cy);
        }
    }

    private void DrawMarker(SKCanvas canvas, MarkerBase m, bool selected)
    {
        if (m.Coords is not { Length: >= 2 }) return;
        var (cx, cy) = CoordsToCanvas(m.Coords[0], m.Coords[1]);

        var (fill, border) = m switch
        {
            LinkMarker => (new SKColor(30, 100, 220), new SKColor(100, 160, 255)),
            SceneMarker => (new SKColor(30, 160, 60), new SKColor(80, 200, 100)),
            _ => (new SKColor(120, 120, 120), new SKColor(200, 200, 200))
        };

        // Outer ring for selected
        if (selected)
        {
            using var selPaint = new SKPaint { Color = SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
            canvas.DrawCircle(cx, cy, MarkerRadius + 4, selPaint);
        }

        // Filled circle
        using var fillPaint = new SKPaint { Color = fill, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawCircle(cx, cy, MarkerRadius, fillPaint);

        // Border
        using var borderPaint = new SKPaint { Color = border, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        canvas.DrawCircle(cx, cy, MarkerRadius, borderPaint);

        // Letter label
        var letter = m switch { LinkMarker => "L", SceneMarker => "→", _ => "i" };
        using var txtFont = new SKFont(SKTypeface.Default, 14) { Embolden = m is SceneMarker };
        using var txtPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(letter, cx, cy + 5, SKTextAlign.Center, txtFont, txtPaint);
    }

    private static void DrawCrosshair(SKCanvas canvas, float cx, float cy)
    {
        using var paint = new SKPaint { Color = new SKColor(255, 200, 0, 180), StrokeWidth = 1.5f, IsAntialias = true };
        const float r = 10;
        canvas.DrawLine(cx - r, cy, cx + r, cy, paint);
        canvas.DrawLine(cx, cy - r, cx, cy + r, paint);
        canvas.DrawCircle(cx, cy, 4, paint);
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private (float x, float y) CoordsToCanvas(double yaw, double pitch)
    {
        if (_bitmap == null) return (0, 0);
        var px = (float)((yaw + Math.PI) / (2 * Math.PI) * _bitmap.Width);
        var py = (float)(pitch / Math.PI * _bitmap.Height);
        return (px * _scale + _offX, py * _scale + _offY);
    }

    private (double yaw, double pitch) CanvasToCoords(Point pt)
    {
        if (_bitmap == null) return (0, Math.PI / 2);
        var imgX = ((float)pt.X - _offX) / _scale;
        var imgY = ((float)pt.Y - _offY) / _scale;
        imgX = Math.Clamp(imgX, 0, _bitmap.Width);
        imgY = Math.Clamp(imgY, 0, _bitmap.Height);
        var yaw = imgX / _bitmap.Width * 2 * Math.PI - Math.PI;
        var pitch = imgY / _bitmap.Height * Math.PI;
        return (yaw, pitch);
    }

    private MarkerBase? HitTest(Point pt)
    {
        if (Scene == null) return null;
        foreach (var m in Scene.Markers)
        {
            if (m.Coords is not { Length: >= 2 }) continue;
            var (cx, cy) = CoordsToCanvas(m.Coords[0], m.Coords[1]);
            var dx = (float)pt.X - cx;
            var dy = (float)pt.Y - cy;
            if (dx * dx + dy * dy <= HitRadius * HitRadius) return m;
        }
        return null;
    }

    // ── Input handling ────────────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space) _spaceHeld = true;
        if (e.Key == Key.Delete && SelectedMarker != null && Scene != null)
        {
            Scene.Markers.Remove(SelectedMarker);
            SelectedMarker = null;
            MarkerSelected?.Invoke(null);
            InvalidateVisual();
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space) _spaceHeld = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var factor = e.Delta.Y > 0 ? 1.15f : 1f / 1.15f;
        var mx = (float)e.GetPosition(this).X;
        var my = (float)e.GetPosition(this).Y;
        var oldScale = _scale;
        _scale = Math.Clamp(_scale * factor, 0.1f, 20f);
        _offX = mx - (mx - _offX) * (_scale / oldScale);
        _offY = my - (my - _offY) * (_scale / oldScale);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pt = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;

        // Middle mouse or Space+left → pan
        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && _spaceHeld))
        {
            _isPanning = true;
            _panStart = pt;
            _panOffXStart = _offX;
            _panOffYStart = _offY;
            e.Pointer.Capture(this);
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            var hit = HitTest(pt);
            if (hit != null)
            {
                // Start potential drag
                _dragging = hit;
                _dragMoved = false;
                _dragStart = pt;
                e.Pointer.Capture(this);
                SelectedMarker = hit;
                MarkerSelected?.Invoke(hit);
            }
            else
            {
                SelectedMarker = null;
                MarkerSelected?.Invoke(null);
                _dragging = null;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);

        if (_isPanning)
        {
            _offX = _panOffXStart + (float)(pt.X - _panStart.X);
            _offY = _panOffYStart + (float)(pt.Y - _panStart.Y);
            InvalidateVisual();
            return;
        }

        if (_dragging?.Coords is { Length: >= 2 })
        {
            var dx = pt.X - _dragStart.X;
            var dy = pt.Y - _dragStart.Y;
            if (Math.Sqrt(dx * dx + dy * dy) > 3) _dragMoved = true;

            if (_dragMoved)
            {
                var (yaw, pitch) = CanvasToCoords(pt);
                _dragging.Coords[0] = yaw;
                _dragging.Coords[1] = pitch;
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pt = e.GetPosition(this);
        e.Pointer.Capture(null);

        if (_isPanning) { _isPanning = false; return; }

        if (_dragging != null)
        {
            if (!_dragMoved)
            {
                // It was a click, not drag — selection already set in OnPointerPressed
            }
            _dragging = null;
            _dragMoved = false;
        }
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        var pt = e.GetPosition(this);
        if (HitTest(pt) != null) return; // double-click on existing marker = ignore
        var (yaw, pitch) = CanvasToCoords(pt);
        AddMarkerRequested?.Invoke(yaw, pitch);
    }
}

// ── Custom Skia draw operation ─────────────────────────────────────────────────

internal sealed class PanoramaDrawOp : ICustomDrawOperation
{
    private readonly PanoramaCanvas _canvas;
    public Rect Bounds { get; }

    public PanoramaDrawOp(PanoramaCanvas canvas, Rect bounds)
    {
        _canvas = canvas;
        Bounds = bounds;
    }

    public bool HitTest(Point p) => Bounds.Contains(p);

    public void Render(ImmediateDrawingContext context)
    {
        var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null) return;
        using var lease = feature.Lease();
        _canvas.RenderSkia(lease.SkCanvas);
    }

    public bool Equals(ICustomDrawOperation? other) => false;
    public void Dispose() { }
}
