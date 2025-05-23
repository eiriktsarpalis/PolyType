namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a union case in a discriminated union type.
/// </summary>
public abstract class IUnionCaseShape(ITypeShapeProvider provider)
{
    /// <summary>
    /// Gets the type shape provider that created this object.
    /// </summary>
    public ITypeShapeProvider Provider => provider;

    /// <summary>
    /// Gets the unique string identifier for the current union case.
    /// </summary>
    /// <remarks>
    /// The value is usually the name of the derived type but can be
    /// overridden via the <see cref="DerivedTypeShapeAttribute.Name"/> property.
    /// </remarks>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the unique integer identifier for the current union case.
    /// </summary>
    /// <remarks>
    /// The value typically corresponds to the order of <see cref="DerivedTypeShapeAttribute"/> declarations
    /// but can be overridden via the <see cref="DerivedTypeShapeAttribute.Tag"/> property.
    /// </remarks>
    public abstract int Tag { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Tag"/> has been explicitly specified or inferred in a less stable way.
    /// </summary>
    public abstract bool IsTagSpecified { get; }

    /// <summary>
    /// Gets the unique index corresponding to the current union case.
    /// </summary>
    /// <remarks>
    /// Corresponds to the index of this instance in the parent <see cref="IUnionTypeShape.UnionCases"/> list.
    /// While similar to <see cref="Tag"/>, the value of the index is distinct and is not part of the data contract.
    /// </remarks>
    public abstract int Index { get; }

    /// <summary>
    /// Gets the underlying type shape of the union case.
    /// </summary>
    public ITypeShape Type => TypeNonGeneric;

    /// <inheritdoc cref="Type"/>
    protected abstract ITypeShape TypeNonGeneric { get; }

    /// <summary>
    /// Accepts an <see cref="TypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a union case in a discriminated union type.
/// </summary>
/// <typeparam name="TUnionCase">The type of the union case.</typeparam>
/// <typeparam name="TUnion">The type of the underlying union.</typeparam>
public abstract class IUnionCaseShape<TUnionCase, TUnion>(ITypeShapeProvider provider) : IUnionCaseShape(provider)
    where TUnionCase : TUnion
{
    /// <summary>
    /// Gets the underlying type shape of the union case.
    /// </summary>
    public new virtual ITypeShape<TUnionCase> Type => Provider.Resolve<TUnionCase>();

    /// <inheritdoc/>
    protected override ITypeShape TypeNonGeneric => Type;

    /// <inheritdoc/>
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnionCase(this, state);
}