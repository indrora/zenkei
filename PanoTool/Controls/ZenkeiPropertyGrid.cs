using System.Collections.Generic;
using PGControl = Avalonia.PropertyGrid.Controls.PropertyGrid;
using Zenkei.PropertyGrid;

namespace Zenkei.Controls;

/// <summary>
/// PropertyGrid subclass that pre-registers Zenkei-specific cell factories.
/// Use this instead of <c>pg:PropertyGrid</c> everywhere in the project.
/// Set <see cref="SceneNames"/> to the open document's scene ID collection so that
/// <see cref="SceneIdCellFactory"/> can populate its ComboBox.
/// </summary>
public class ZenkeiPropertyGrid : PGControl
{
    /// <summary>
    /// Scene IDs available for <see cref="SceneIdCellFactory"/> to populate its ComboBox.
    /// Set from <see cref="Zenkei.Views.PropertiesView"/> when DataContext changes.
    /// </summary>
    public IEnumerable<string>? SceneNames { get; set; }

    public ZenkeiPropertyGrid()
    {
        // Factories is initialised in PropertyGrid() before InitializeComponent,
        // so appending in the derived constructor body is safe.
        // YawPitchCellFactory (priority 2000) must be registered before
        // DegreeCellFactory (priority 1000) so it wins for YawPitch properties.
        Factories.AddFactory(new YawPitchCellFactory());
        Factories.AddFactory(new DegreeCellFactory());
        Factories.AddFactory(new MultilineTextCellFactory());
        // SceneIdCellFactory reads SceneNames lazily via the lambda so it always sees the current list.
        Factories.AddFactory(new SceneIdCellFactory(() => SceneNames));
    }
}
