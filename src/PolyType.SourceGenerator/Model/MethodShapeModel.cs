using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record MethodShapeModel
{
    public required string Name { get; init; }
    public required string UnderlyingMethodName { get; init; }
    public required TypeId DeclaringType { get; init; }
    public required TypeId ReturnType { get; init; }
    public required TypeId UnderlyingReturnType { get; init; }
    public required int Position { get; init; }
    public required bool IsPublic { get; init; }
    public required bool IsAccessible { get; init; }
    public required bool CanUseUnsafeAccessors { get; init; }
    public required bool IsStatic { get; init; }
    public required bool ReturnsByRef { get; init; }
    public required bool RequiresDisambiguation { get; init; }
    public required MethodReturnTypeKind ReturnTypeKind { get; init; }
    public required ImmutableEquatableArray<ParameterShapeModel> Parameters { get; init; }
    public required ArgumentStateType ArgumentStateType { get; init; }
}