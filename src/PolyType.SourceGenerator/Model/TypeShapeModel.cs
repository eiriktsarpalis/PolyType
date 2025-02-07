using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public abstract record TypeShapeModel
{
    public required TypeId Type { get; init; }

    public TypeId UnboundGenericType { get; init; }

    public ImmutableEquatableArray<TypeId> TypeArguments { get; init; } = ImmutableEquatableArray<TypeId>.Empty;

    /// <summary>
    /// A unique identifier deriving from the type name that can be used as a valid member identifier.
    /// </summary>
    public required string SourceIdentifier { get; init; }
}
