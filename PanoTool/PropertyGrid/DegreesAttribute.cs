namespace Zenkei.PropertyGrid;

/// <summary>
/// Marks a numeric property as a degree value.
/// ZenkeiPropertyGrid renders it as a NumericUpDown with a "°" suffix,
/// range 1–360, instead of the raw double editor.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DegreesAttribute : Attribute { }
