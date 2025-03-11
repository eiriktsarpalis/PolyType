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
    /// An array of type IDs for associated types.
    /// </summary>
    public required ImmutableEquatableArray<(TypeId Open, TypeId Closed)> AssociatedTypes { get; init; }
}
