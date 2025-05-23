using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET enum.
/// </summary>
public abstract class IEnumTypeShape(ITypeShapeProvider provider) : ITypeShape
{
    /// <summary>
    /// Gets the shape of the underlying type used to represent the enum.
    /// </summary>
    public ITypeShape UnderlyingType => UnderlyingTypeNonGeneric;

    /// <inheritdoc/>
    public abstract Type Type { get; }

    /// <inheritdoc/>
    public TypeShapeKind Kind => TypeShapeKind.Enum;

    /// <inheritdoc/>
    public ITypeShapeProvider Provider => provider;

    /// <inheritdoc/>
    public virtual ICustomAttributeProvider? AttributeProvider => Type;

    /// <inheritdoc cref="UnderlyingType"/>
    protected abstract ITypeShape UnderlyingTypeNonGeneric { get; }

    /// <inheritdoc/>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    public abstract ITypeShape? GetAssociatedTypeShape(Type associatedType);

    /// <inheritdoc/>
    public abstract object? Invoke(ITypeShapeFunc func, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a .NET enum.
/// </summary>
/// <typeparam name="TEnum">The type of .NET enum.</typeparam>
/// <typeparam name="TUnderlying">The underlying type used to represent the enum.</typeparam>
public abstract class IEnumTypeShape<TEnum, TUnderlying>(ITypeShapeProvider provider) : IEnumTypeShape(provider), ITypeShape<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Gets the shape of the underlying type used to represent the enum.
    /// </summary>
    public new virtual ITypeShape<TUnderlying> UnderlyingType => Provider.Resolve<TUnderlying>();

    /// <inheritdoc/>
    public override Type Type => typeof(TEnum);

    /// <inheritdoc/>
    protected override ITypeShape UnderlyingTypeNonGeneric => UnderlyingType;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnum(this, state);

    /// <inheritdoc/>
    public override object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
}