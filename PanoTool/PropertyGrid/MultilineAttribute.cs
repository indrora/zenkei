namespace Zenkei.PropertyGrid;

/// <summary>
/// Marks a string property for rendering as a multi-line TextBox in the PropertyGrid.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MultilineAttribute : Attribute { }
