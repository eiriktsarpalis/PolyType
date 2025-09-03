using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record FunctionShapeModel : TypeShapeModel
{
    public required TypeId ReturnType { get; init; }
    public required TypeId UnderlyingReturnType { get; init; }
    public required bool ReturnsByRef { get; init; }
    public required MethodReturnTypeKind ReturnTypeKind { get; init; }
    public required ImmutableEquatableArray<ParameterShapeModel> Parameters { get; init; }
    public required ArgumentStateType ArgumentStateType { get; init; }
}
