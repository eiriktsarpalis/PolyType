using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record UnionShapeModel : TypeShapeModel
{
    public required TypeShapeModel UnderlyingModel { get; init; }

    /// <summary>
    /// The list of known derived types for the given type in topological order from most to least derived.
    /// </summary>
    public required ImmutableEquatableArray<UnionCaseModel> UnionCases { get; init; }
}

public sealed record UnionCaseModel
{
    public required TypeId Type { get; init; }
    public required string Name { get; init; }
    public required int Tag { get; init; }
    public required int Index { get; init; }
    public required bool IsBaseType { get; init; }
}
