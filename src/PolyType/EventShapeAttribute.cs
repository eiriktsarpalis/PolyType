namespace PolyType;

/// <summary>
/// Configures the shape of an event for a given type.
/// </summary>
[AttributeUsage(AttributeTargets.Event, AllowMultiple = false, Inherited = true)]
public sealed class EventShapeAttribute : Attribute
{
    /// <summary>
    /// Gets a custom name to be used for the particular event.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the annotated event should be ignored in the shape model.
    /// </summary>
    public bool Ignore { get; init; }
}