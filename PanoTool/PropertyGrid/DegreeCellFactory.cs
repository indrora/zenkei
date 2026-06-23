using System;
using System.ComponentModel;
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
/// NumericUpDown with a "°" suffix, range 1–360, step 1.
/// Registered by <see cref="Zenkei.Controls.ZenkeiPropertyGrid"/>.
/// </summary>
public class DegreeCellFactory : AbstractCellEditFactory
{
    // Larger = earlier. Default is 100; NumericCellEditFactory is −9 999 900.
    // We must run before the numeric catch-all so [Degrees] wins.
    public override int ImportPriority => 1000;

    public override Control? HandleNewProperty(PropertyCellContext context)
    {
        if (!context.Property.Attributes.OfType<DegreesAttribute>().Any())
            return null;

        var nud = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 360,
            Increment = 1,
            FormatString = "F0",
            AllowSpin = true,
        };

        // Degree symbol inlined to the right of the spinner text
        nud.SetValue(TextBox.InnerRightContentProperty, new TextBlock
        {
            Text = "°",
            Padding = new Thickness(0, 0, 5, 0),
            Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
        });

        nud.ValueChanged += (_, _) =>
        {
            // HFov is double? — map decimal? → double?
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
