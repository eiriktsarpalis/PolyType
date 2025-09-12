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
    /// <inheritdoc/>
    public required ITypeShape<TUnionCase> UnionCaseType { get; init; }

    /// <inheritdoc/>
    public required IMarshaler<TUnionCase, TUnion> Marshaler { get; init; }

    /// <inheritdoc/>
    public required string Name { get; init; }

    /// <inheritdoc/>
    public required int Tag { get; init; }

    /// <inheritdoc/>
    public required bool IsTagSpecified { get; init; }

    /// <inheritdoc/>
    public required int Index { get; init; }

    ITypeShape IUnionCaseShape.UnionCaseType => UnionCaseType;
    object? IUnionCaseShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitUnionCase(this, state);

    private string DebuggerDisplay => $"{{ Name = \"{Name}\", CaseType = \"{typeof(TUnionCase)}\" }}";
}
