using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET type that employs a surrogate type.
/// </summary>
public abstract class ISurrogateTypeShape(ITypeShapeProvider provider) : ITypeShape
{
    /// <inheritdoc/>
    public ITypeShapeProvider Provider => provider;

    /// <summary>
    /// Gets the shape of the surrogate type.
    /// </summary>
    public ITypeShape SurrogateType => SurrogateTypeNonGeneric;

    /// <inheritdoc cref="SurrogateType"/>
    protected abstract ITypeShape SurrogateTypeNonGeneric { get; }

    /// <inheritdoc/>
    public TypeShapeKind Kind => TypeShapeKind.Surrogate;

    /// <inheritdoc/>
    public abstract Type Type { get; }

    /// <inheritdoc/>
    public virtual ICustomAttributeProvider? AttributeProvider => Type;

    /// <inheritdoc/>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    public abstract object? Invoke(ITypeShapeFunc func, object? state = null);

    /// <inheritdoc/>
    public abstract ITypeShape? GetAssociatedTypeShape(Type associatedType);
}

/// <summary>
/// Provides a strongly typed shape model for a .NET type that employs a surrogate type.
/// </summary>
/// <typeparam name="T">The type the shape describes.</typeparam>
/// <typeparam name="TSurrogate">The surrogate type being specified by the shape.</typeparam>
public abstract class ISurrogateTypeShape<T, TSurrogate>(ITypeShapeProvider provider) : ISurrogateTypeShape(provider), ITypeShape<T>
{
    /// <summary>
    /// Gets the bidirectional mapper between <typeparamref name="T"/> and <typeparamref name="TSurrogate"/>.
    /// </summary>
    public abstract IMarshaller<T, TSurrogate> Marshaller { get; }

    /// <summary>
    /// Gets the shape of the element type of the nullable.
    /// </summary>
    public new virtual ITypeShape<TSurrogate> SurrogateType => Provider.Resolve<TSurrogate>();

    /// <inheritdoc/>
    protected override ITypeShape SurrogateTypeNonGeneric => SurrogateType;

    /// <inheritdoc/>
    public override Type Type => typeof(T);

    /// <inheritdoc/>
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitSurrogate(this, state);

    /// <inheritdoc/>
    public sealed override object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
}