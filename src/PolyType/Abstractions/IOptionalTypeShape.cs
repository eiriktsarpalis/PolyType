using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for optional types.
/// </summary>
/// <remarks>
/// Examples of optional types include <see cref="Nullable{T}"/> or the F# option types.
/// </remarks>
public abstract class IOptionalTypeShape(ITypeShapeProvider provider) : ITypeShape
{
    /// <summary>
    /// Gets the shape of underlying value type.
    /// </summary>
    public ITypeShape ElementType => ElementTypeNonGeneric;

    /// <inheritdoc/>
    public TypeShapeKind Kind => TypeShapeKind.Optional;

    /// <inheritdoc/>
    public ITypeShapeProvider Provider => provider;

    /// <inheritdoc/>
    public virtual ICustomAttributeProvider? AttributeProvider => Type;

    /// <inheritdoc/>
    public abstract Type Type { get; }

    /// <inheritdoc cref="ElementType"/>
    protected abstract ITypeShape ElementTypeNonGeneric { get; }

    /// <inheritdoc/>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    public abstract ITypeShape? GetAssociatedTypeShape(Type associatedType);

    /// <inheritdoc/>
    public abstract object? Invoke(ITypeShapeFunc func, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for optional types.
/// </summary>
/// <typeparam name="TOptional">The optional type described by the shape.</typeparam>
/// <typeparam name="TElement">The value encapsulated by the option type.</typeparam>
/// <remarks>
/// Examples of optional types include <see cref="Nullable{T}"/> or the F# option types.
/// </remarks>
public abstract class IOptionalTypeShape<TOptional, TElement>(ITypeShapeProvider provider) : IOptionalTypeShape(provider), ITypeShape<TOptional>
{
    /// <summary>
    /// Gets the shape of the underlying value type.
    /// </summary>
    public new virtual ITypeShape<TElement> ElementType => Provider.Resolve<TElement>();

    /// <inheritdoc/>
    public override Type Type => typeof(TOptional);

    /// <inheritdoc/>
    protected override ITypeShape ElementTypeNonGeneric => ElementType;

    /// <summary>
    /// Gets a constructor for creating empty (aka 'None') instances of <typeparamref name="TOptional"/>.
    /// </summary>
    /// <returns>A delegate for creating empty (aka 'None') instances of <typeparamref name="TOptional"/>.</returns>
    public abstract Func<TOptional> GetNoneConstructor();

    /// <summary>
    /// Gets a constructor for creating populated (aka 'Some') instances of <typeparamref name="TOptional"/>.
    /// </summary>
    /// <returns>A delegate for creating populated (aka 'Some') instances of <typeparamref name="TOptional"/>.</returns>
    public abstract Func<TElement, TOptional> GetSomeConstructor();

    /// <summary>
    /// Gets a deconstructor delegate for <typeparamref name="TOptional"/> instances.
    /// </summary>
    /// <returns>A delegate for deconstructing <typeparamref name="TOptional"/> instances.</returns>
    public abstract OptionDeconstructor<TOptional, TElement> GetDeconstructor();

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitOptional(this, state);

    /// <inheritdoc/>
    public override object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
}