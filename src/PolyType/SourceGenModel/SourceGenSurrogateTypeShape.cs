using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for surrogate type shapes.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
/// <typeparam name="TSurrogate">The surrogate type used by the shape.</typeparam>
public sealed class SourceGenSurrogateTypeShape<T, TSurrogate>(SourceGenTypeShapeProvider provider) : ISurrogateTypeShape<T, TSurrogate>(provider)
{
    /// <summary>
    /// Gets the marshaller to <typeparamref name="TSurrogate"/>.
    /// </summary>
    public required IMarshaller<T, TSurrogate> MarshallerSetter { get; init; }

    /// <inheritdoc/>
    public override IMarshaller<T, TSurrogate> Marshaller => MarshallerSetter;

    /// <summary>
    /// Gets the shape of the surrogate type.
    /// </summary>
    public required ITypeShape<TSurrogate> SurrogateTypeSetter { get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TSurrogate> SurrogateType => SurrogateTypeSetter;

    /// <summary>
    /// Gets the shape of an associated type, by its name.
    /// </summary>
    public Func<string, ITypeShape?>? AssociatedTypeShapes { get; init; }

    /// <inheritdoc/>
    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, AssociatedTypeShapes, associatedType);
}