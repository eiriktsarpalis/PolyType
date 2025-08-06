namespace PolyType;

/// <summary>
/// Configures the shape of a method for a given type.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class MethodShapeAttribute : Attribute
{
    /// <summary>
    /// Gets a custom name to be used for the particular method.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the annotated method should be ignored in the shape model.
    /// </summary>
    public bool Ignore { get; init; }
}