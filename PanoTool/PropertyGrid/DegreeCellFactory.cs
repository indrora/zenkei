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
/// Renders any property decorated with <see cref="DegreesAttribute"/> as a
/// NumericUpDown with a "°" suffix.  Min/max default to 1–360 but are overridden
/// by a <see cref="RangeAttribute"/> on the same property (e.g. [Range(-180, 180)]).
/// Registered by <see cref="Zenkei.Controls.ZenkeiPropertyGrid"/>.
/// </summary>
public class DegreeCellFactory : AbstractCellEditFactory
{
    // Larger = earlier. Default is 100; NumericCellEditFactory is −9 999 900.
    public override int ImportPriority => 1000;

    public override Control? HandleNewProperty(PropertyCellContext context)
    {
        if (!context.Property.Attributes.OfType<DegreesAttribute>().Any())
            return null;

        decimal min = 1m, max = 360m;
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
            double? newValue = nud.Value is { } v ? (double)v : null;
            SetAndRaise(context, nud, newValue, context.GetValue());
        };

        return nud;
    }

    public override bool HandlePropertyChanged(PropertyCellContext context)
    {
        if (context.CellEdit is not NumericUpDown nud) return false;

        ValidateProperty(nud, context.Property, context.Target);

        var raw = context.Property.GetValue(context.Target);
        nud.Value = raw is double d ? (decimal)d : null;
        return true;
    }
}
