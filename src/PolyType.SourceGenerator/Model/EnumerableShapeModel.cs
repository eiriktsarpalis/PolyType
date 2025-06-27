using PolyType.Roslyn;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Model;

public sealed record EnumerableShapeModel : TypeShapeModel
{
    public required TypeId ElementType { get; init; }
    public required EnumerableKind Kind { get; init; }
    public required int Rank { get; init; }
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }
    public required ReadOnlyMemory<CollectionConstructorParameter> ConstructionParameters { get; init; }
    public required ReadOnlyMemory<CollectionConstructorParameter> ConstructionParametersWithCapacity { get; init; }
    public required bool HasConstructorWithoutComparer { get; init; }
    public required string? AddElementMethod { get; init; }
    public required string? ImplementationTypeFQN { get; init; }
    public required string? StaticFactoryMethod { get; init; }
    public required string? StaticFactoryWithComparerMethod { get; init; }
    public required bool CtorRequiresListConversion { get; init; }
    public required bool ElementTypeContainsNullableAnnotations { get; init; }
    public required bool AddMethodIsExplicitInterfaceImplementation { get; init; }
}
