using Avalonia.Controls;
using Avalonia.PropertyGrid.Controls.Factories;
using Avalonia.PropertyGrid.Controls;
using Zenkei.Controls;
using Zenkei.Models;

namespace Zenkei.PropertyGrid;

/// <summary>
/// Renders a <see cref="YawPitch"/> property as a paired Yaw/Pitch control.
/// Registered by <see cref="ZenkeiPropertyGrid"/>.
/// Priority is higher than <see cref="DegreeCellFactory"/> so it wins first.
/// </summary>
public class YawPitchCellFactory : AbstractCellEditFactory
{
    public override int ImportPriority => 2000;

    public override Control? HandleNewProperty(PropertyCellContext context)
    {
        if (context.Property.PropertyType != typeof(YawPitch)) return null;

        var ctrl = new YawPitchControl();

        void OnNudChanged()
        {
            // Ignore the ValueChanged events that SetYawPitch fires internally.
            if (ctrl.IsUpdating) return;
            if (ctrl.YawNud.Value is not { } y) return;
            if (ctrl.PitchNud.Value is not { } p) return;
            SetAndRaise(context, ctrl, new YawPitch((double)y, (double)p), context.GetValue());
        }

        ctrl.YawNud.ValueChanged   += (_, _) => OnNudChanged();
        ctrl.PitchNud.ValueChanged += (_, _) => OnNudChanged();

        return ctrl;
    }

    public override bool HandlePropertyChanged(PropertyCellContext context)
    {
        if (context.Property.PropertyType != typeof(YawPitch)) return false;
        if (context.CellEdit is not YawPitchControl ctrl) return false;

        ValidateProperty(ctrl, context.Property, context.Target);

        var yp = (YawPitch)context.Property.GetValue(context.Target)!;
        ctrl.SetYawPitch(yp.Yaw, yp.Pitch);
        return true;
    }
}
