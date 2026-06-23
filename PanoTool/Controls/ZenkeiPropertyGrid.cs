using PGControl = Avalonia.PropertyGrid.Controls.PropertyGrid;
using Zenkei.PropertyGrid;

namespace Zenkei.Controls;

/// <summary>
/// PropertyGrid subclass that pre-registers Zenkei-specific cell factories.
/// Use this instead of <c>pg:PropertyGrid</c> everywhere in the project.
/// </summary>
public class ZenkeiPropertyGrid : PGControl
{
    public ZenkeiPropertyGrid()
    {
        // Factories is initialised in PropertyGrid() before InitializeComponent,
        // so appending in the derived constructor body is safe.
        // YawPitchCellFactory (priority 2000) must be registered before
        // DegreeCellFactory (priority 1000) so it wins for YawPitch properties.
        Factories.AddFactory(new YawPitchCellFactory());
        Factories.AddFactory(new DegreeCellFactory());
    }
}
