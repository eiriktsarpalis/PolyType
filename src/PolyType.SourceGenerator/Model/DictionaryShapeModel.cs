using PolyType.Roslyn;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Model;

public sealed record DictionaryShapeModel : TypeShapeModel
{
    public required TypeId KeyType { get; init; }
    public required TypeId ValueType { get; init; }
    public required DictionaryKind Kind { get; init; }
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }
    public required ImmutableEquatableArray<CollectionConstructorParameter> ConstructorParameters { get; init; }
    public required string? ImplementationTypeFQN { get; init; }
    public required string? StaticFactoryMethod { get; init; }
    public required bool KeyValueTypesContainNullableAnnotations { get; init; }
    public required DictionaryInsertionMode AvailableInsertionModes { get; init; }
}
