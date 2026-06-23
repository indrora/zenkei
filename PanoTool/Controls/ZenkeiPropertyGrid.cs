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
        // so this append is safe in the derived constructor body.
        Factories.AddFactory(new DegreeCellFactory());
    }
}
