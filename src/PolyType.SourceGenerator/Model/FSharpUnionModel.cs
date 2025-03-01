using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Model;

public sealed record FSharpUnionShapeModel : TypeShapeModel
{
    public required TypeShapeModel UnderlyingModel { get; init; }
    public required ImmutableEquatableArray<FSharpUnionCaseShapeModel> UnionCases { get; init; }
    public required string TagReader { get; set; }
    public required bool TagReaderIsMethod { get; init; }
}

public sealed record FSharpUnionCaseShapeModel(string Name, int Tag, TypeShapeModel TypeModel);

public sealed class FSharpUnionDataModel : TypeDataModel
{
    public required ImmutableArray<FSharpUnionCaseDataModel> UnionCases { get; init; }
    public required IMethodSymbol TagReader { get; init; }
}

public readonly struct FSharpUnionCaseDataModel
{
    public required string Name { get; init; }
    public required int Tag { get; init; }
    public required ObjectDataModel Type { get; init; } 
}
