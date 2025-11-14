using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for union type shapes.
/// </summary>
/// <typeparam name="TUnion">The type whose shape is described.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.UnionTypeShapeDebugView))]
public sealed class SourceGenUnionTypeShape<TUnion> : SourceGenTypeShape<TUnion>, IUnionTypeShape<TUnion>
{
    /// <inheritdoc/>
    public required ITypeShape<TUnion> BaseType { get; init; }

    /// <summary>
    /// Gets a factory method for creating union case shapes.
    /// </summary>
    public required Func<IEnumerable<IUnionCaseShape>> UnionCasesFactory { get; init; }

    /// <summary>
    /// Gets a delegate that computes the union case index for a given value.
    /// </summary>
    public required Getter<TUnion, int> GetUnionCaseIndex { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Union;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnion(this, state);

    IReadOnlyList<IUnionCaseShape> IUnionTypeShape.UnionCases => field ?? CommonHelpers.ExchangeIfNull(ref field, UnionCasesFactory().AsReadOnlyList());

    Getter<TUnion, int> IUnionTypeShape<TUnion>.GetGetUnionCaseIndex() => GetUnionCaseIndex;
    ITypeShape IUnionTypeShape.BaseType => BaseType;
}
