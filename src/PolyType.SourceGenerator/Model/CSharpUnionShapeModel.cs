using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record CSharpUnionShapeModel : TypeShapeModel
{
    public required ObjectShapeModel UnderlyingModel { get; init; }
    public required ImmutableEquatableArray<CSharpUnionCaseShapeModel> UnionCases { get; init; }
    public required ImmutableEquatableArray<int> CaseDispatchOrder { get; init; }
    public required int NullableCaseIndex { get; init; }
    public required TypeId? ValuePatternType { get; init; }
}

public sealed record CSharpUnionCaseShapeModel
{
    public required TypeId Type { get; init; }
    public required TypeId PatternType { get; init; }
    public required string Name { get; init; }
    public required int Index { get; init; }
    public required bool IsNullable { get; init; }
    public required bool RequiresInArgument { get; init; }
    public required UnionCaseCreatorKind CreatorKind { get; init; }
    public required TypeId? FactoryDeclaringType { get; init; }
}

public enum UnionCaseCreatorKind
{
    Constructor,
    StaticFactory,
    ConstrainedFactory,
}
