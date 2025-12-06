using PolyType.Roslyn;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Model;

public sealed record EnumerableShapeModel : TypeShapeModel
{
    public required TypeId ElementType { get; init; }
    public required EnumerableKind Kind { get; init; }
    public required int Rank { get; init; }
    public required bool IsSetType { get; init; }
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }
    public required ImmutableEquatableArray<CollectionConstructorParameter> ConstructorParameters { get; init; }
    public required string? AppendMethod { get; init; }
    public required string? ImplementationTypeFQN { get; init; }
    public required string? StaticFactoryMethod { get; init; }
    public required bool ElementTypeContainsNullableAnnotations { get; init; }
    public required bool AppendMethodReturnsBoolean { get; init; }
    public required EnumerableInsertionMode InsertionMode { get; init; }
    public int? Length { get; init; }
}
