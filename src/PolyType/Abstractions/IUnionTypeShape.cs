using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET type encoding a discriminated union.
/// </summary>
/// <remarks>
/// Typically reserved for classes or interfaces that specify derived types via the <see cref="DerivedTypeShapeAttribute"/>
/// but can also include F# discriminated unions.
/// </remarks>
public abstract class IUnionTypeShape(ITypeShapeProvider provider) : ITypeShape
{
    /// <summary>
    /// Gets the type shape provider that created this object.
    /// </summary>
    public ITypeShapeProvider Provider => provider;

    /// <inheritdoc/>
    public TypeShapeKind Kind => TypeShapeKind.Union;

    /// <summary>
    /// Gets the underlying type shape of the union base type.
    /// </summary>
    /// <remarks>
    /// Typically used as the fallback case for values not matching any of the union cases.
    /// </remarks>
    public ITypeShape BaseType => BaseTypeNonGeneric;

    /// <inheritdoc/>
    public abstract Type Type { get; }

    /// <inheritdoc/>
    public virtual ICustomAttributeProvider? AttributeProvider => Type;

    /// <summary>
    /// Gets the list of all registered union case shapes.
    /// </summary>
    public abstract IReadOnlyList<IUnionCaseShape> UnionCases { get; }

    /// <inheritdoc cref="BaseType"/>
    protected abstract ITypeShape BaseTypeNonGeneric { get; }

    /// <inheritdoc/>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    public abstract ITypeShape? GetAssociatedTypeShape(Type associatedType);

    /// <inheritdoc/>
    public abstract object? Invoke(ITypeShapeFunc func, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a .NET type encoding a discriminated union.
/// </summary>
/// <typeparam name="TUnion">The type of the union.</typeparam>
/// <remarks>
/// Typically reserved for classes or interfaces that specify derived types via the <see cref="DerivedTypeShapeAttribute"/>
/// but can also include F# discriminated unions.
/// </remarks>
public abstract class IUnionTypeShape<TUnion>(ITypeShapeProvider provider) : IUnionTypeShape(provider), ITypeShape<TUnion>
{
    /// <summary>
    /// Gets the underlying type shape of the union base type.
    /// </summary>
    /// <remarks>
    /// Typically used as the fallback case for values not matching any of the union cases.
    /// </remarks>
    public new abstract ITypeShape<TUnion> BaseType { get; }

    /// <inheritdoc/>
    public override Type Type => typeof(TUnion);

    /// <inheritdoc/>
    protected override ITypeShape BaseTypeNonGeneric => BaseType;

    /// <summary>
    /// Creates a delegate that computes the union case index for a given value.
    /// </summary>
    /// <returns>A delegate that computes the union case index for a given value.</returns>
    /// <remarks>
    /// The delegate returns an index pointing to the <see cref="IUnionTypeShape.UnionCases"/> list.
    /// It should be noted that the value of the index is distinct from the <see cref="IUnionCaseShape.Tag"/> property.
    /// </remarks>
    public abstract Getter<TUnion, int> GetGetUnionCaseIndex();

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnion(this, state);

    /// <inheritdoc/>
    public override object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
}