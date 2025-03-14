using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for union case shapes.
/// </summary>
/// <typeparam name="TUnionCase">The type of the current union case.</typeparam>
/// <typeparam name="TUnion">The type of the base union type.</typeparam>
public sealed class SourceGenUnionCaseShape<TUnionCase, TUnion> : IUnionCaseShape<TUnionCase, TUnion>
    where TUnionCase : TUnion
{
    /// <summary>
    /// Gets the underlying type shape of the union case.
    /// </summary>
    public required ITypeShape<TUnionCase> Type { get; init; }

    /// <summary>
    /// Gets the unique string identifier for the current union case.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the unique integer identifier for the current union case.
    /// </summary>
    public required int Tag { get; init; }

    /// <inheritdoc/>
    public required bool IsTagSpecified { get; init; }

    /// <summary>
    /// Gets the unique index corresponding to the current union case.
    /// </summary>
    public required int Index { get; init; }

    ITypeShape IUnionCaseShape.Type => Type;
    object? IUnionCaseShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitUnionCase(this, state);
}
