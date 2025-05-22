using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET enum.
/// </summary>
public interface IEnumTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the shape of the underlying type used to represent the enum.
    /// </summary>
    ITypeShape UnderlyingType { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET enum.
/// </summary>
/// <typeparam name="TEnum">The type of .NET enum.</typeparam>
/// <typeparam name="TUnderlying">The underlying type used to represent the enum.</typeparam>
public abstract class IEnumTypeShape<TEnum, TUnderlying>(ITypeShapeProvider provider) : ITypeShape<TEnum>, IEnumTypeShape
    where TEnum : struct, Enum
{
    ITypeShape IEnumTypeShape.UnderlyingType => this.UnderlyingType;

    /// <summary>
    /// Gets the shape of the underlying type used to represent the enum.
    /// </summary>
    public virtual ITypeShape<TUnderlying> UnderlyingType => provider.Resolve<TUnderlying>();

    /// <inheritdoc/>
    public Type Type => typeof(TEnum);

    /// <inheritdoc/>
    public TypeShapeKind Kind => TypeShapeKind.Enum;

    /// <inheritdoc/>
    public ITypeShapeProvider Provider => provider;

    /// <inheritdoc/>
    public virtual ICustomAttributeProvider? AttributeProvider => typeof(TEnum);

    /// <inheritdoc/>
    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnum(this, state);

    /// <inheritdoc/>
    public abstract ITypeShape? GetAssociatedTypeShape(Type associatedType);

    /// <inheritdoc/>
    public object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
}