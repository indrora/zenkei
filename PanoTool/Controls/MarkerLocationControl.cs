using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace Zenkei.Controls;

/// <summary>
/// A compact equirectangular mini-map for picking a yaw/pitch location.
///
/// Yaw  ∈ [-π, π] maps to the X axis (left → right).
/// Pitch ∈ [0, π] maps to the Y axis (top → bottom, 0 = zenith, π = nadir).
///
/// Both properties default to TwoWay so a plain {Binding} round-trips back
/// to the ViewModel without needing an explicit Mode.
/// </summary>
public class MarkerLocationControl : Control
{
    public static readonly StyledProperty<double> YawProperty =
        AvaloniaProperty.Register<MarkerLocationControl, double>(
            nameof(Yaw), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> PitchProperty =
        AvaloniaProperty.Register<MarkerLocationControl, double>(
            nameof(Pitch), Math.PI / 2, defaultBindingMode: BindingMode.TwoWay);

    public double Yaw   { get => GetValue(YawProperty);   set => SetValue(YawProperty,   value); }
    public double Pitch { get => GetValue(PitchProperty); set => SetValue(PitchProperty, value); }

    private bool _dragging;

    static MarkerLocationControl()
    {
        AffectsRender<MarkerLocationControl>(YawProperty, PitchProperty);
        ClipToBoundsProperty.OverrideDefaultValue<MarkerLocationControl>(true);
    }

    /// <summary>Always request height = width / 2 so the control stays 2:1.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsFinite(availableSize.Width) ? availableSize.Width : 200;
        return new Size(w, w / 2.0);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // Background
        ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(24, 24, 30)), new Rect(Bounds.Size));

        // Faint grid (quarters horizontal, eighths vertical to hint at the 2:1 ratio)
        var faint = new Pen(new SolidColorBrush(Color.FromRgb(45, 45, 58)), 0.5);
        for (int i = 1; i < 4; i++)
            ctx.DrawLine(faint, new Point(w * i / 4, 0), new Point(w * i / 4, h));
        for (int i = 1; i < 4; i++)
            ctx.DrawLine(faint, new Point(0, h * i / 4), new Point(w, h * i / 4));

        // Equator (pitch = π/2) and prime meridian (yaw = 0)
        var accent = new Pen(new SolidColorBrush(Color.FromRgb(65, 65, 85)), 1);
        ctx.DrawLine(accent, new Point(0,     h / 2), new Point(w,     h / 2));
        ctx.DrawLine(accent, new Point(w / 2, 0),     new Point(w / 2, h));

        // Border
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(85, 85, 100)), 1),
                          new Rect(Bounds.Size));

        // Current position dot
        var cx = (Yaw + Math.PI) / (2 * Math.PI) * w;
        var cy = Pitch / Math.PI * h;

        // Subtle shadow
        ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null,
                        new Point(cx + 1, cy + 1), 6, 6);
        // Dot
        ctx.DrawEllipse(
            new SolidColorBrush(Color.FromRgb(255, 200, 0)),
            new Pen(new SolidColorBrush(Color.FromRgb(255, 240, 160)), 1.5),
            new Point(cx, cy), 6, 6);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragging = true;
        e.Pointer.Capture(this);
        ApplyPosition(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging) ApplyPosition(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private void ApplyPosition(Point pt)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        Yaw   = Math.Clamp(pt.X / Bounds.Width  * 2 * Math.PI - Math.PI, -Math.PI, Math.PI);
        Pitch = Math.Clamp(pt.Y / Bounds.Height * Math.PI, 0, Math.PI);
    }
}
