using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public abstract record TypeShapeModel
{
    public required TypeId Type { get; init; }

    /// <summary>
    /// A unique identifier deriving from the type name that can be used as a valid member identifier.
    /// </summary>
    public required string SourceIdentifier { get; init; }

    /// <summary>
    /// The type name as reported by the corresponding <see cref="System.Type.Name"/> property.
    /// </summary>
    public required string ReflectionName { get; init; }

    /// <summary>
    /// The method shapes declared by the current type.
    /// </summary>
    public required ImmutableEquatableArray<MethodShapeModel> Methods { get; init; }

    /// <summary>
    /// The event shapes declared by the current type.
    /// </summary>
    public required ImmutableEquatableArray<EventShapeModel> Events { get; init; }

    /// <summary>
    /// A map of type IDs for associated types and their requirements.
    /// </summary>
    public required ImmutableEquatableSet<AssociatedTypeId> AssociatedTypes { get; init; }
}
