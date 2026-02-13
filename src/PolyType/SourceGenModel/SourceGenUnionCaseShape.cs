using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for union case shapes.
/// </summary>
/// <typeparam name="TUnionCase">The type of the current union case.</typeparam>
/// <typeparam name="TUnion">The type of the base union type.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.UnionCaseShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class SourceGenUnionCaseShape<TUnionCase, TUnion> : IUnionCaseShape<TUnionCase, TUnion>
{
    /// <summary>
    /// Gets a delayed union case type shape factory for use with potentially recursive type graphs.
    /// </summary>
    public required Func<ITypeShape<TUnionCase>> UnionCaseTypeFactory { get; init; }

    /// <inheritdoc/>
    [Obsolete("This member has been marked for deprecation and will be removed in the future.")]
    public ITypeShape<TUnionCase> UnionCaseType
    {
        get => field ??= UnionCaseTypeFactory.Invoke();
        init;
    }

    /// <inheritdoc/>
    public required IMarshaler<TUnionCase, TUnion> Marshaler { get; init; }

    /// <inheritdoc/>
    public required string Name { get; init; }

    /// <inheritdoc/>
    public required int Tag { get; init; }

    /// <inheritdoc/>
    public bool IsTagSpecified { get; init; }

    /// <inheritdoc/>
    public required int Index { get; init; }

#pragma warning disable CS0618 // Type or member is obsolete
    ITypeShape IUnionCaseShape.UnionCaseType => UnionCaseType;
#pragma warning restore CS0618
    object? IUnionCaseShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitUnionCase(this, state);

    private string DebuggerDisplay => $"{{ Name = \"{Name}\", CaseType = \"{typeof(TUnionCase)}\" }}";
}
