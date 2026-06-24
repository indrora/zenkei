using Avalonia.Controls;
using Avalonia.PropertyGrid.Controls.Factories;
using Avalonia.PropertyGrid.Controls;
using Zenkei.Controls;
using Zenkei.Models;

namespace Zenkei.PropertyGrid;

/// <summary>
/// Renders a <see cref="YawPitch"/> property as a <see cref="YawPitchControl"/>
/// (equirectangular mini-map + paired degree NUDs).
/// Registered by <see cref="ZenkeiPropertyGrid"/>.
/// </summary>
public class YawPitchCellFactory : AbstractCellEditFactory
{
    public override int ImportPriority => 2000;

    public override Control? HandleNewProperty(PropertyCellContext context)
    {
        if (context.Property.PropertyType != typeof(YawPitch)) return null;

        var ctrl = new YawPitchControl();
        ctrl.LayoutUpdated += (_, _) =>
        {
            // Ensure the control is sized to fill the cell.        
            ctrl.SetValue(Grid.ColumnProperty, 0);
            ctrl.SetValue(Grid.ColumnSpanProperty, 2);
        };

        // PositionChanged fires only on user input, never during SetYawPitch.
        ctrl.PositionChanged += (_, yp) =>
            SetAndRaise(context, ctrl, yp, context.GetValue());

        return ctrl;

    }

    public override bool HandlePropertyChanged(PropertyCellContext context)
    {
        if (context.CellEdit is not YawPitchControl ctrl) return false;

        if (context.Property.PropertyType != typeof(YawPitch)) return false;

        ValidateProperty(ctrl, context.Property, context.Target);

        var yp = (YawPitch)context.Property.GetValue(context.Target)!;
        ctrl.SetYawPitch(yp.Yaw, yp.Pitch);
        return true;
    }
}
