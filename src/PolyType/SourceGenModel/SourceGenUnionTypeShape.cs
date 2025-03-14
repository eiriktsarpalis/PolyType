using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for union type shapes.
/// </summary>
/// <typeparam name="TUnion">The type whose shape is described.</typeparam>
public sealed class SourceGenUnionTypeShape<TUnion> : SourceGenTypeShape<TUnion>, IUnionTypeShape<TUnion>
{
    /// <summary>
    /// Gets the underlying type shape of the union base type.
    /// </summary>
    public required ITypeShape<TUnion> BaseType { get; init; }

    /// <summary>
    /// Gets a factory method for creating union case shapes.
    /// </summary>
    public required Func<IEnumerable<IUnionCaseShape>> CreateUnionCasesFunc { get; init; }

    /// <summary>
    /// Gets a delegate that computes the union case index for a given value.
    /// </summary>
    public required Getter<TUnion, int> GetUnionCaseIndexFunc { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Union;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnion(this, state);

    IReadOnlyList<IUnionCaseShape> IUnionTypeShape.UnionCases => _unionCases ??= CreateUnionCasesFunc().AsReadOnlyList();
    private IReadOnlyList<IUnionCaseShape>? _unionCases;

    Getter<TUnion, int> IUnionTypeShape<TUnion>.GetGetUnionCaseIndex() => GetUnionCaseIndexFunc;
    ITypeShape IUnionTypeShape.BaseType => BaseType;
}
