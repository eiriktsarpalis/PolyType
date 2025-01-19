using Microsoft.CodeAnalysis;
using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record SurrogateShapeModel : TypeShapeModel
{
    public required TypeId SurrogateType { get; init; }
    public required TypeId MarshallerType { get; init; }
}

public sealed class SurrogateTypeDataModel : TypeDataModel
{
    public required ITypeSymbol SurrogateType { get; init; }
    public required ITypeSymbol MarshallerType { get; init; }
}