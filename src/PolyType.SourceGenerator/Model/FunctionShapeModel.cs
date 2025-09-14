using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Model;

public sealed record FunctionShapeModel : TypeShapeModel
{
    public required TypeId ReturnType { get; init; }
    public required TypeId UnderlyingReturnType { get; init; }
    public required bool ReturnsByRef { get; init; }
    public required MethodReturnTypeKind ReturnTypeKind { get; init; }
    public required ImmutableEquatableArray<ParameterShapeModel> Parameters { get; init; }
    public required ArgumentStateType ArgumentStateType { get; init; }
    public required bool IsFsharpFunc { get; init; }
}

public sealed class FSharpFunctionDataModel : TypeDataModel
{
    public const MethodReturnTypeKind FSharpUnitReturnTypeKind = (MethodReturnTypeKind)9999;

    public required ITypeSymbol ReturnType { get; init; }
    public required ITypeSymbol? ReturnedValueType { get; init; }
    public required MethodReturnTypeKind ReturnTypeKind { get; init; }
    public required ImmutableArray<ITypeSymbol> Parameters { get; init; }
}
