using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record TypeShapeProviderModel
{
    public required TypeDeclarationModel ProviderDeclaration { get; init; }
    public required ImmutableEquatableDictionary<TypeId, TypeShapeModel> ProvidedTypes { get; init; }
    public required ImmutableEquatableArray<TypeDeclarationModel> AnnotatedTypes { get; init; }
    public required bool TargetSupportsIShapeableOfT { get; init; }
}