using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Zenkei.Models;
using Zenkei.Models.Markers;

namespace Zenkei.Controls;

/// <summary>
/// Renders a 2:1 equirectangular panorama image and allows placing / moving
/// markers interactively.
///
/// Rendering uses Avalonia's managed <see cref="DrawingContext"/> on the UI
/// thread. (An earlier version used a raw-Skia <c>ICustomDrawOperation</c>,
/// which forced the compositor into a continuous per-frame render loop —
/// pegging the CPU, flickering, and racing UI-thread image disposal against
/// render-thread drawing.)
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

    private Bitmap? _bitmap;
    private int _imgW, _imgH;
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

    private const float MarkerRadius = 14f;
    private const float HitRadius = 18f;

    public PanoramaCanvas()
    {
        ClipToBounds = true;
    }

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
        var path = Scene?.ResolvedImagePath;
        if (path == _loadedImagePath) return;
        _loadedImagePath = path;
        _bitmap?.Dispose();
        _bitmap = null;
        _imgW = _imgH = 0;

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                _bitmap = new Bitmap(path);
                _imgW = _bitmap.PixelSize.Width;
                _imgH = _bitmap.PixelSize.Height;
                ResetTransform();
            }
            catch { /* ignore bad images */ }
        }
    }

    private void ResetTransform()
    {
        if (_bitmap == null || Bounds.Width == 0 || Bounds.Height == 0) return;
        var scaleX = (float)(Bounds.Width / _imgW);
        var scaleY = (float)(Bounds.Height / _imgH);
        _scale = Math.Min(scaleX, scaleY);
        _offX = (float)((Bounds.Width - _imgW * _scale) / 2f);
        _offY = (float)((Bounds.Height - _imgH * _scale) / 2f);
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
        // Background
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(40, 40, 40)), new Rect(Bounds.Size));

        if (_bitmap == null)
        {
            var msg = new FormattedText(
                "No image loaded — select a scene",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default), 18, Brushes.Gray);
            context.DrawText(msg, new Point(
                (Bounds.Width - msg.Width) / 2, (Bounds.Height - msg.Height) / 2));
            return;
        }

        // Panorama image, scaled+offset by the pan/zoom transform
        var dest = new Rect(_offX, _offY, _imgW * _scale, _imgH * _scale);
        context.DrawImage(_bitmap, new Rect(0, 0, _imgW, _imgH), dest);

        if (Scene != null)
        {
            // Snapshot to a list so a concurrent edit can't disrupt enumeration
            foreach (var m in Scene.Markers.ToList())
                DrawMarker(context, m, m == SelectedMarker);

            var (cx, cy) = CoordsToCanvas(Scene.Initial[0], Scene.Initial[1]);
            DrawCrosshair(context, cx, cy);
        }
    }

    private void DrawMarker(DrawingContext context, MarkerBase m, bool selected)
    {
        if (m.Coords is not { Length: >= 2 }) return;
        var (cx, cy) = CoordsToCanvas(m.Coords[0], m.Coords[1]);
        var center = new Point(cx, cy);

        var (fill, border) = m switch
        {
            LinkMarker => (Color.FromRgb(30, 100, 220), Color.FromRgb(100, 160, 255)),
            SceneMarker => (Color.FromRgb(30, 160, 60), Color.FromRgb(80, 200, 100)),
            _ => (Color.FromRgb(120, 120, 120), Color.FromRgb(200, 200, 200))
        };

        if (selected)
        {
            context.DrawEllipse(null, new Pen(Brushes.Yellow, 3),
                center, MarkerRadius + 4, MarkerRadius + 4);
        }

        context.DrawEllipse(new SolidColorBrush(fill), new Pen(new SolidColorBrush(border), 1.5),
            center, MarkerRadius, MarkerRadius);

        var letter = m switch { LinkMarker => "L", SceneMarker => "→", _ => "i" };
        var txt = new FormattedText(letter, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, weight: m is SceneMarker ? FontWeight.Bold : FontWeight.Normal),
            14, Brushes.White);
        context.DrawText(txt, new Point(cx - txt.Width / 2, cy - txt.Height / 2));
    }

    private static void DrawCrosshair(DrawingContext context, float cx, float cy)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 200, 0)), 1.5);
        const float r = 10;
        context.DrawLine(pen, new Point(cx - r, cy), new Point(cx + r, cy));
        context.DrawLine(pen, new Point(cx, cy - r), new Point(cx, cy + r));
        context.DrawEllipse(null, pen, new Point(cx, cy), 4, 4);
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private (float x, float y) CoordsToCanvas(double yaw, double pitch)
    {
        if (_bitmap == null) return (0, 0);
        var px = (float)((yaw + Math.PI) / (2 * Math.PI) * _imgW);
        var py = (float)(pitch / Math.PI * _imgH);
        return (px * _scale + _offX, py * _scale + _offY);
    }

    private (double yaw, double pitch) CanvasToCoords(Point pt)
    {
        if (_bitmap == null) return (0, Math.PI / 2);
        var imgX = ((float)pt.X - _offX) / _scale;
        var imgY = ((float)pt.Y - _offY) / _scale;
        imgX = Math.Clamp(imgX, 0, _imgW);
        imgY = Math.Clamp(imgY, 0, _imgH);
        var yaw = imgX / _imgW * 2 * Math.PI - Math.PI;
        var pitch = imgY / _imgH * Math.PI;
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
        e.Pointer.Capture(null);

        if (_isPanning) { _isPanning = false; return; }

        if (_dragging != null)
        {
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
