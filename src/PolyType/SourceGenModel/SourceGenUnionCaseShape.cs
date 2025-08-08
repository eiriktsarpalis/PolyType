using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for union case shapes.
/// </summary>
/// <typeparam name="TUnionCase">The type of the current union case.</typeparam>
/// <typeparam name="TUnion">The type of the base union type.</typeparam>
public sealed class SourceGenUnionCaseShape<TUnionCase, TUnion> : IUnionCaseShape<TUnionCase, TUnion>
{
    /// <inheritdoc/>
    public required ITypeShape<TUnionCase> Type { get; init; }

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

    ITypeShape IUnionCaseShape.Type => Type;
    object? IUnionCaseShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitUnionCase(this, state);
}
