using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET type that employs a surrogate type.
/// </summary>
public interface ISurrogateTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the shape of the surrogate type.
    /// </summary>
    ITypeShape SurrogateType { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET type that employs a surrogate type.
/// </summary>
/// <typeparam name="T">The type the shape describes.</typeparam>
/// <typeparam name="TSurrogate">The surrogate type being specified by the shape.</typeparam>
public abstract class ISurrogateTypeShape<T, TSurrogate>(ITypeShapeProvider provider) : ITypeShape<T>, ISurrogateTypeShape
{
    /// <summary>
    /// Gets the bidirectional mapper between <typeparamref name="T"/> and <typeparamref name="TSurrogate"/>.
    /// </summary>
    public abstract IMarshaller<T, TSurrogate> Marshaller { get; }

    /// <summary>
    /// Gets the shape of the element type of the nullable.
    /// </summary>
    public virtual ITypeShape<TSurrogate> SurrogateType => Provider.Resolve<TSurrogate>();

    /// <inheritdoc/>
    public Type Type => typeof(T);

    /// <inheritdoc/>
    public TypeShapeKind Kind => TypeShapeKind.Surrogate;

    /// <inheritdoc/>
    public ITypeShapeProvider Provider => provider;

    /// <inheritdoc/>
    public virtual ICustomAttributeProvider? AttributeProvider => typeof(T);

    ITypeShape ISurrogateTypeShape.SurrogateType => SurrogateType;

    /// <inheritdoc/>
    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitSurrogate(this, state);

    /// <inheritdoc/>
    public abstract ITypeShape? GetAssociatedTypeShape(Type associatedType);

    /// <inheritdoc/>
    public object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
}