using PolyType.Roslyn;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Model;

public sealed record DictionaryShapeModel : TypeShapeModel
{
    public required TypeId KeyType { get; init; }
    public required TypeId ValueType { get; init; }
    public required DictionaryKind Kind { get; init; }
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }
    public required ImmutableArray<ImmutableArray<ConstructionParameterType>> ParameterLists { get; init; }
    public required string? ImplementationTypeFQN { get; init; }
    public required string? StaticFactoryMethod { get; init; }
    public required bool IsTupleEnumerableFactory { get; init; }
    public required bool CtorRequiresDictionaryConversion { get; init; }
    public required bool KeyValueTypesContainNullableAnnotations { get; init; }
}
