using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.PropertyGrid.Controls;
using Avalonia.PropertyGrid.Controls.Factories;

namespace Zenkei.PropertyGrid;

/// <summary>
/// Renders any string property decorated with <see cref="MultilineAttribute"/>
/// as an auto-sizing multi-line TextBox (AcceptsReturn = true, min 80px tall).
/// Registered by <see cref="Zenkei.Controls.ZenkeiPropertyGrid"/>.
/// </summary>
public class MultilineTextCellFactory : AbstractCellEditFactory
{
    public override int ImportPriority => 1200;

    public override Control? HandleNewProperty(PropertyCellContext context)
    {
        if (context.Property.PropertyType != typeof(string)) return null;
        if (!context.Property.Attributes.OfType<MultilineAttribute>().Any())
            return null;

        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping  = Avalonia.Media.TextWrapping.Wrap,
            MinHeight     = 80,
            MaxHeight     = 200,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        box.TextChanged += (_, _) =>
            SetAndRaise(context, box, box.Text ?? "", context.GetValue());

        return box;
    }

    public override bool HandlePropertyChanged(PropertyCellContext context)
    {
        if (context.CellEdit is not TextBox box) return false;
        if (context.Property.PropertyType != typeof(string)) return false;
        if (!context.Property.Attributes.OfType<MultilineAttribute>().Any())
            return false;

        ValidateProperty(box, context.Property, context.Target);

        var current = context.Property.GetValue(context.Target) as string ?? "";
        if (box.Text != current)
            box.Text = current;
        return true;
    }
}
