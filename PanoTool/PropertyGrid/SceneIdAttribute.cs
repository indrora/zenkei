namespace Zenkei.PropertyGrid;

/// <summary>
/// Marks a string property as a scene identifier.
/// ZenkeiPropertyGrid renders it as a ComboBox populated from the open document's scene list.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SceneIdAttribute : Attribute { }
