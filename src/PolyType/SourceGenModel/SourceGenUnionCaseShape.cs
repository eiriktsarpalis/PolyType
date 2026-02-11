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
    public Func<ITypeShape<TUnionCase>>? UnionCaseTypeFunc { get; init; }

    /// <inheritdoc/>
    [Obsolete("Use UnionCaseTypeFunc for delayed initialization to avoid stack overflows with recursive types.")]
    public ITypeShape<TUnionCase> UnionCaseType
    {
        get => _unionCaseType ??= UnionCaseTypeFunc?.Invoke() ?? throw new InvalidOperationException("UnionCaseTypeFunc has not been initialized.");
        init => _unionCaseType = value;
    }

    private ITypeShape<TUnionCase>? _unionCaseType;

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

#pragma warning disable CS0618 // Type or member is obsolete -- used internally for interface implementation
    ITypeShape IUnionCaseShape.UnionCaseType => UnionCaseType;
#pragma warning restore CS0618

    object? IUnionCaseShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitUnionCase(this, state);

    private string DebuggerDisplay => $"{{ Name = \"{Name}\", CaseType = \"{typeof(TUnionCase)}\" }}";
}
