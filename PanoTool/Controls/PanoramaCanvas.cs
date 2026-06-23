using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Zenkei.Models;
using Zenkei.Models.Markers;
using Zenkei.ViewModels;

namespace Zenkei.Controls;

/// <summary>
/// Renders a 2:1 equirectangular panorama image and allows placing / moving
/// markers interactively.
///
/// Pan/zoom state is persisted in <see cref="PanoramaEditorViewModel.PanZoomScale"/>,
/// <see cref="PanoramaEditorViewModel.PanZoomOffX"/>, and
/// <see cref="PanoramaEditorViewModel.PanZoomOffY"/> so that it survives
/// Dock.Avalonia recreating the view on every tab switch.  A null PanZoomScale
/// means "fit to window" mode; the canvas recalculates on every resize in that
/// mode and does not persist the computed scale back to the ViewModel.
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

    /// <summary>Fired on every frame while a marker is being dragged.</summary>
    public event Action<MarkerBase, double, double>? MarkerMoved; // (marker, yaw, pitch)

    /// <summary>Fired on every frame while the initial viewpoint is being dragged.</summary>
    public event Action<double, double>? InitialViewChanged; // (yaw, pitch)

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

    // dragging the initial-viewpoint special marker
    private bool _draggingInitial;

    private const float MarkerRadius = 14f;
    private const float HitRadius = 18f;
    // circle radius of the initial-viewpoint glyph + tick extensions
    private const float InitialCircleR = 12f;
    private const float InitialTickExt = 7f;
    private const float InitialHitRadius = InitialCircleR + InitialTickExt;

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
            // Unsubscribe old scene, subscribe new — so image path changes redraw us.
            if (change.OldValue is Scene oldScene) oldScene.PropertyChanged -= OnScenePropertyChanged;
            if (change.NewValue is Scene newScene) newScene.PropertyChanged += OnScenePropertyChanged;
            LoadSceneImage();
            InvalidateVisual();
        }
        else if (change.Property == SelectedMarkerProperty)
        {
            // Unsubscribe old, subscribe new — so editor-side coord changes redraw us.
            if (change.OldValue is MarkerBase oldM) oldM.PropertyChanged -= OnSelectedMarkerPropertyChanged;
            if (change.NewValue is MarkerBase newM) newM.PropertyChanged += OnSelectedMarkerPropertyChanged;
            InvalidateVisual();
        }
        else if (change.Property == DataContextProperty)
        {
            // Unsubscribe old VM, subscribe new — so external PanZoomScale=null triggers re-fit.
            if (change.OldValue is PanoramaEditorViewModel oldVm) oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (change.NewValue is PanoramaEditorViewModel newVm) newVm.PropertyChanged += OnVmPropertyChanged;
            // DataContext (re-)bound on every tab switch — restore saved transform.
            if (_bitmap != null)
                RestoreFromDoc();
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
                // Restore from doc (which may call FitToWindow if PanZoomScale is null).
                // Returns early if the control isn't sized yet; OnSizeChanged handles it.
                RestoreFromDoc();
            }
            catch { /* ignore bad images */ }
        }
    }

    // ── Transform helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Reads pan/zoom state from the document ViewModel. If PanZoomScale is null,
    /// falls back to FitToWindow. Safe to call before layout (guards on Bounds size).
    /// </summary>
    private void RestoreFromDoc()
    {
        if (_bitmap == null || Bounds.Width == 0 || Bounds.Height == 0) return;

        var vm = DataContext as PanoramaEditorViewModel;
        if (vm?.PanZoomScale is float scale)
        {
            _scale = scale;
            _offX  = vm.PanZoomOffX;
            _offY  = vm.PanZoomOffY;
        }
        else
        {
            // No saved state → fit to window (stays in fit mode until user zooms).
            FitToWindow();
        }
    }

    /// <summary>
    /// Scales and centers the image to fill the canvas. Does NOT persist the
    /// computed values back to the ViewModel, so PanZoomScale stays null and the
    /// canvas keeps recalculating on every resize.
    /// </summary>
    private void FitToWindow()
    {
        if (_bitmap == null || Bounds.Width == 0 || Bounds.Height == 0) return;
        var scaleX = (float)(Bounds.Width  / _imgW);
        var scaleY = (float)(Bounds.Height / _imgH);
        _scale = Math.Min(scaleX, scaleY);
        _offX  = (float)((Bounds.Width  - _imgW * _scale) / 2f);
        _offY  = (float)((Bounds.Height - _imgH * _scale) / 2f);
    }

    /// <summary>
    /// Writes the current transform to the ViewModel so it persists across tab
    /// switches.  Calling this marks the document as having a user-set zoom (i.e.
    /// PanZoomScale becomes non-null, disabling fit-to-window mode).
    /// </summary>
    private void SaveToDoc()
    {
        if (DataContext is not PanoramaEditorViewModel vm) return;
        vm.PanZoomScale = _scale;
        vm.PanZoomOffX  = _offX;
        vm.PanZoomOffY  = _offY;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // Always restore on size change: if PanZoomScale is null this recalculates
        // the fit; if it's non-null this re-applies the saved transform (no-op for
        // the common case where _scale/_offX/_offY already match).
        if (_bitmap != null && Bounds.Width > 0 && Bounds.Height > 0)
            RestoreFromDoc();
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

            var (cx, cy) = CoordsToCanvas(Scene.Initial.Yaw, Scene.Initial.Pitch);
            DrawInitialMarker(context, cx, cy);
        }
    }

    private void DrawMarker(DrawingContext context, MarkerBase m, bool selected)
    {
        if (m is InitialMarker || !m.Coords.HasValue) return;
        var (cx, cy) = CoordsToCanvas(m.Coords.Value.Yaw, m.Coords.Value.Pitch);
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

    /// <summary>
    /// Draws the initial-viewpoint marker: a circle with cardinal tick marks
    /// extending outward, indicating it can be dragged.
    /// </summary>
    private static void DrawInitialMarker(DrawingContext context, float cx, float cy)
    {
        var color = Color.FromArgb(210, 255, 200, 0);
        var pen   = new Pen(new SolidColorBrush(color), 1.5);
        var fill  = new SolidColorBrush(Color.FromArgb(55, 255, 200, 0));
        const float r   = InitialCircleR;
        const float ext = InitialTickExt;

        // Semi-transparent circle (signals "this is the view cone")
        context.DrawEllipse(fill, pen, new Point(cx, cy), r, r);

        // Cardinal tick marks beyond the circle (signals "draggable")
        context.DrawLine(pen, new Point(cx - r - ext, cy), new Point(cx - r, cy));
        context.DrawLine(pen, new Point(cx + r,       cy), new Point(cx + r + ext, cy));
        context.DrawLine(pen, new Point(cx, cy - r - ext), new Point(cx, cy - r));
        context.DrawLine(pen, new Point(cx, cy + r),       new Point(cx, cy + r + ext));

        // Center dot
        context.DrawEllipse(new SolidColorBrush(color), null, new Point(cx, cy), 3f, 3f);
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
            if (m is InitialMarker || !m.Coords.HasValue) continue;
            var (cx, cy) = CoordsToCanvas(m.Coords.Value.Yaw, m.Coords.Value.Pitch);
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
        SaveToDoc();
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
                // Start potential marker drag
                _dragging = hit;
                _dragMoved = false;
                _dragStart = pt;
                e.Pointer.Capture(this);
                SelectedMarker = hit;
                MarkerSelected?.Invoke(hit);
            }
            else
            {
                // Check for initial-viewpoint drag (only when no marker is hit)
                if (Scene != null && _bitmap != null)
                {
                    var (icx, icy) = CoordsToCanvas(Scene.Initial.Yaw, Scene.Initial.Pitch);
                    var idx = (float)pt.X - icx;
                    var idy = (float)pt.Y - icy;
                    if (idx * idx + idy * idy <= InitialHitRadius * InitialHitRadius)
                    {
                        _draggingInitial = true;
                        e.Pointer.Capture(this);
                        return;
                    }
                }

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
            SaveToDoc();
            InvalidateVisual();
            return;
        }

        // Initial-viewpoint drag
        if (_draggingInitial && Scene != null)
        {
            var (yaw, pitch) = CanvasToCoords(pt);
            Scene.Initial = new YawPitch(yaw, pitch);
            InitialViewChanged?.Invoke(yaw, pitch);
            InvalidateVisual();
            return;
        }

        // Marker drag
        if (_dragging != null && _dragging.Coords.HasValue)
        {
            var dx = pt.X - _dragStart.X;
            var dy = pt.Y - _dragStart.Y;
            if (Math.Sqrt(dx * dx + dy * dy) > 3) _dragMoved = true;

            if (_dragMoved)
            {
                var (yaw, pitch) = CanvasToCoords(pt);
                _dragging.Coords = new YawPitch(yaw, pitch);
                MarkerMoved?.Invoke(_dragging, yaw, pitch);
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);

        if (_isPanning) { _isPanning = false; return; }
        if (_draggingInitial) { _draggingInitial = false; return; }

        if (_dragging != null)
        {
            _dragging = null;
            _dragMoved = false;
            return;
        }

        // Right-click on empty canvas space → fire AddMarkerRequested at the
        // cursor's equirectangular position.  The view's context menu then
        // lets the user pick the marker type.
        if (e.InitialPressMouseButton == MouseButton.Right
            && HitTest(e.GetPosition(this)) == null)
        {
            var (yaw, pitch) = CanvasToCoords(e.GetPosition(this));
            AddMarkerRequested?.Invoke(yaw, pitch);
        }
    }

    private void OnScenePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Scene.Image))
        {
            LoadSceneImage();
            InvalidateVisual();
        }
        else if (e.PropertyName == nameof(Scene.Initial))
        {
            InvalidateVisual();
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PanoramaEditorViewModel.PanZoomScale))
        {
            RestoreFromDoc();
            InvalidateVisual();
        }
    }

    private void OnSelectedMarkerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MarkerBase.Coords))
            InvalidateVisual();
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        var pt = e.GetPosition(this);
        if (HitTest(pt) != null) return; // double-click on existing marker = ignore
        var (yaw, pitch) = CanvasToCoords(pt);
        AddMarkerRequested?.Invoke(yaw, pitch);
    }

    // ── External zoom API (called by the zoom toolbar) ────────────────────────

    /// <summary>
    /// Multiplies the current scale by <paramref name="factor"/>, keeping the
    /// canvas centre pinned to the same image pixel.
    /// </summary>
    public void ZoomBy(float factor)
    {
        if (_bitmap == null) return;
        // When in fit mode PanZoomScale is null; compute the fit values first so
        // _scale/_offX/_offY are meaningful before we multiply.
        if ((DataContext as PanoramaEditorViewModel)?.PanZoomScale is null)
            FitToWindow();

        var cx = (float)(Bounds.Width  / 2);
        var cy = (float)(Bounds.Height / 2);
        var newScale = _scale * factor;
        _offX = cx - (cx - _offX) * (newScale / _scale);
        _offY = cy - (cy - _offY) * (newScale / _scale);
        _scale = newScale;
        SaveToDoc();
        InvalidateVisual();
    }

    /// <summary>
    /// Sets an absolute scale (1.0 = one screen pixel per image pixel), centred
    /// on the middle of the image.
    /// </summary>
    public void ZoomAbsolute(float scale)
    {
        if (_bitmap == null) return;
        var cx = (float)(Bounds.Width  / 2);
        var cy = (float)(Bounds.Height / 2);
        _scale = scale;
        _offX  = cx - (_imgW / 2f) * _scale;
        _offY  = cy - (_imgH / 2f) * _scale;
        SaveToDoc();
        InvalidateVisual();
    }

    /// <summary>Resets to fit-to-window mode (clears the saved zoom state).</summary>
    public void ZoomFit()
    {
        if (DataContext is PanoramaEditorViewModel vm)
            vm.PanZoomScale = null;
        FitToWindow();
        InvalidateVisual();
    }
}
