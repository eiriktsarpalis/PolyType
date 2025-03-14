using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for surrogate type shapes.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
/// <typeparam name="TSurrogate">The surrogate type used by the shape.</typeparam>
public sealed class SourceGenSurrogateTypeShape<T, TSurrogate> : SourceGenTypeShape<T>, ISurrogateTypeShape<T, TSurrogate>
{
    /// <summary>
    /// Gets the marshaller to <typeparamref name="TSurrogate"/>.
    /// </summary>
    public required IMarshaller<T, TSurrogate> Marshaller { get; init; }

    /// <summary>
    /// Gets the shape of the surrogate type.
    /// </summary>
    public required ITypeShape<TSurrogate> SurrogateType { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Surrogate;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitSurrogate(this, state);

    ITypeShape ISurrogateTypeShape.SurrogateType => SurrogateType;
}