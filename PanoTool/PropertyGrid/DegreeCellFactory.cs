using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.PropertyGrid.Controls;
using Avalonia.PropertyGrid.Controls.Factories;

namespace Zenkei.PropertyGrid;

/// <summary>
/// Renders any <see cref="double"/> property decorated with <see cref="DegreesAttribute"/>
/// as a NumericUpDown with a "°" suffix.  Min/max default to 0–360 but are overridden
/// by a <see cref="RangeAttribute"/> on the same property (e.g. [Range(-180, 180)]).
/// Registered by <see cref="Zenkei.Controls.ZenkeiPropertyGrid"/>.
/// </summary>
public class DegreeCellFactory : AbstractCellEditFactory
{
    // Higher = earlier. NumericCellEditFactory is −9 999 900.
    public override int ImportPriority => 1000;

    public override Control? HandleNewProperty(PropertyCellContext context)
    {
        if (!context.Property.Attributes.OfType<DegreesAttribute>().Any())
            return null;

        decimal min = 0m, max = 360m;
        var rangeAttr = context.Property.Attributes.OfType<RangeAttribute>().FirstOrDefault();
        if (rangeAttr != null)
        {
            try
            {
                min = Convert.ToDecimal(rangeAttr.Minimum);
                max = Convert.ToDecimal(rangeAttr.Maximum);
            }
            catch { /* keep defaults */ }
        }

        var nud = new NumericUpDown
        {
            Minimum      = min,
            Maximum      = max,
            Increment    = 1,
            FormatString = "F0",
            AllowSpin    = true,
        };

        nud.SetValue(TextBox.InnerRightContentProperty, new TextBlock
        {
            Text              = "°",
            Padding           = new Thickness(0, 0, 5, 0),
            Foreground        = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
        });

        nud.ValueChanged += (_, _) =>
        {
            if (nud.Value is not { } v) return;
            // Convert.ChangeType handles float/double; property type is always numeric here.
            var newValue = Convert.ChangeType((double)v, context.Property.PropertyType);
            SetAndRaise(context, nud, newValue, context.GetValue());
        };

        return nud;
    }

    public override bool HandlePropertyChanged(PropertyCellContext context)
    {
        if (context.CellEdit is not NumericUpDown nud) return false;
        if (!context.Property.Attributes.OfType<DegreesAttribute>().Any()) return false;

        ValidateProperty(nud, context.Property, context.Target);

        var raw = context.Property.GetValue(context.Target);
        nud.Value = raw is double d ? (decimal)d : null;
        return true;
    }

    /// <summary>
    /// Treat changes smaller than 1e-9 degrees as no-ops to avoid precision churn
    /// from radian ↔ degree roundtripping in the model.
    /// </summary>
    protected override bool CheckIsPropertyChanged(
        PropertyCellContext context, object? oldValue, object? newValue)
    {
        if (oldValue is double old && newValue is double nv)
            return Math.Abs(old - nv) > 1e-9;
        return base.CheckIsPropertyChanged(context, oldValue, newValue);
    }
}
