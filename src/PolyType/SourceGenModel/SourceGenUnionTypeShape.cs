using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for union type shapes.
/// </summary>
/// <typeparam name="TUnion">The type whose shape is described.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.UnionTypeShapeDebugView))]
public class SourceGenUnionTypeShape<TUnion> : SourceGenTypeShape<TUnion>, IUnionTypeShape<TUnion>
{
    /// <summary>
    /// A shape type for current source generators that stores contextual status without legacy lookup.
    /// </summary>
    public sealed class Current : SourceGenUnionTypeShape<TUnion>
    {
        /// <inheritdoc/>
        public override bool IsContextual
        {
            get => ExplicitIsContextual;
            init => base.IsContextual = value;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Values omitted by older generated code default to <see cref="UnionTypeShapeKind.Unknown"/>.
    /// </remarks>
    public required UnionTypeShapeKind UnionKind { get; init; }

    /// <summary>
    /// Gets a delayed base type shape factory for use with potentially recursive type graphs.
    /// </summary>
    public required Func<ITypeShape<TUnion>> BaseTypeFactory { get; init; }

    /// <inheritdoc/>
    [Obsolete("This member has been marked for deprecation and will be removed in the future.")]
    public ITypeShape<TUnion> BaseType
    {
        get => field ??= BaseTypeFactory.Invoke();
        init;
    }

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
#pragma warning disable CS0618 // Type or member is obsolete
    ITypeShape IUnionTypeShape.BaseType => BaseType;
#pragma warning restore CS0618
}
