using PolyType.Roslyn;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Model;

public abstract record TypeShapeModel
{
    public required TypeId Type { get; init; }

    /// <summary>
    /// A unique identifier deriving from the type name that can be used as a valid member identifier.
    /// </summary>
    public required string SourceIdentifier { get; init; }

    /// <summary>
    /// A map of type IDs for associated types and their requirements.
    /// </summary>
    public required ImmutableEquatableDictionary<AssociatedTypeId, EquatableEnum<TypeShapeDepth>> AssociatedTypes { get; init; }
}
