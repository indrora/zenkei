using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.PropertyGrid.Controls;
using Avalonia.PropertyGrid.Controls.Factories;

namespace Zenkei.PropertyGrid;

/// <summary>
/// Renders any string property decorated with <see cref="SceneIdAttribute"/>
/// as a ComboBox whose items are the scene IDs from the open document.
/// The scene list is fetched lazily from the injected delegate so it's always current.
/// Registered by <see cref="Zenkei.Controls.ZenkeiPropertyGrid"/>.
/// </summary>
public class SceneIdCellFactory : AbstractCellEditFactory
{
    private readonly Func<IEnumerable<string>?> _getSceneNames;

    public SceneIdCellFactory(Func<IEnumerable<string>?> getSceneNames)
    {
        _getSceneNames = getSceneNames;
    }

    public override int ImportPriority => 1500;

    public override Control? HandleNewProperty(PropertyCellContext context)
    {
        if (context.Property.PropertyType != typeof(string)) return null;
        if (!context.Property.Attributes.OfType<SceneIdAttribute>().Any()) return null;

        var combo = new ComboBox
        {
            PlaceholderText = "— select scene —",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        // Refresh the item list every time the drop-down opens so new scenes appear.
        combo.DropDownOpened += (_, _) =>
            combo.ItemsSource = _getSceneNames()?.ToList() ?? [];

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string selected)
                SetAndRaise(context, combo, selected, context.GetValue());
        };

        return combo;
    }

    public override bool HandlePropertyChanged(PropertyCellContext context)
    {
        if (context.CellEdit is not ComboBox combo) return false;
        if (context.Property.PropertyType != typeof(string)) return false;
        if (!context.Property.Attributes.OfType<SceneIdAttribute>().Any()) return false;

        ValidateProperty(combo, context.Property, context.Target);

        combo.ItemsSource = _getSceneNames()?.ToList() ?? [];
        var current = context.Property.GetValue(context.Target) as string;
        combo.SelectedItem = current;
        return true;
    }
}
