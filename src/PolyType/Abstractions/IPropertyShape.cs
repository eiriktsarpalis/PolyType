using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET instance property or field.
/// </summary>
public abstract class IPropertyShape(ITypeShapeProvider provider)
{
    /// <summary>
    /// Gets the type shape provider that created this object.
    /// </summary>
    public ITypeShapeProvider Provider => provider;

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the provider used for property-level attribute resolution.
    /// </summary>
    public abstract ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Gets the shape of the declaring type.
    /// </summary>
    public IObjectTypeShape DeclaringType => DeclaringTypeNonGeneric;

    /// <inheritdoc cref="DeclaringType"/>
    protected abstract IObjectTypeShape DeclaringTypeNonGeneric { get; }

    /// <summary>
    /// Gets the shape of the property type.
    /// </summary>
    public ITypeShape PropertyType => PropertyTypeNonGeneric;

    /// <inheritdoc cref="PropertyType"/>
    protected abstract ITypeShape PropertyTypeNonGeneric { get; }

    /// <summary>
    /// Gets a value indicating whether the property has an accessible getter.
    /// </summary>
    public abstract bool HasGetter { get; }

    /// <summary>
    /// Gets a value indicating whether the property has an accessible setter.
    /// </summary>
    public abstract bool HasSetter { get; }

    /// <summary>
    /// Gets a value indicating whether the shape represents a .NET field.
    /// </summary>
    public abstract bool IsField { get; }

    /// <summary>
    /// Gets a value indicating whether the property getter is declared public.
    /// </summary>
    public abstract bool IsGetterPublic { get; }

    /// <summary>
    /// Gets a value indicating whether the property setter is declared public.
    /// </summary>
    public abstract bool IsSetterPublic { get; }

    /// <summary>
    /// Gets a value indicating whether the getter returns non-null values.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="true" /> if the property type is a non-nullable struct, a non-nullable reference type
    /// or the property has been annotated with the <see cref="NotNullAttribute"/>.
    ///
    /// Conversely, it could return <see langword="false"/> if a non-nullable property
    /// has been annotated with <see cref="MaybeNullAttribute"/>.
    /// </remarks>
    public abstract bool IsGetterNonNullable { get; }

    /// <summary>
    /// Gets a value indicating whether the setter requires non-null values.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="true" /> if the property type is a non-nullable struct, a non-nullable reference type
    /// or the property has been annotated with the <see cref="DisallowNullAttribute"/>.
    ///
    /// Conversely, it could return <see langword="false"/> if a non-nullable property
    /// has been annotated with <see cref="AllowNullAttribute"/>.
    /// </remarks>
    public abstract bool IsSetterNonNullable { get; }

    /// <summary>
    /// Accepts an <see cref="TypeShapeVisitor"/> for strongly typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET instance property or field.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type of the underlying property.</typeparam>
/// <typeparam name="TPropertyType">The property type of the underlying property.</typeparam>
public abstract class IPropertyShape<TDeclaringType, TPropertyType>(ITypeShapeProvider provider) : IPropertyShape(provider)
{
    /// <summary>
    /// Gets the shape of the declaring type.
    /// </summary>
    public new virtual IObjectTypeShape<TDeclaringType> DeclaringType => (IObjectTypeShape<TDeclaringType>)Provider.Resolve<TDeclaringType>();

    /// <summary>
    /// Gets the shape of the property type.
    /// </summary>
    public new abstract ITypeShape<TPropertyType> PropertyType { get; }

    /// <inheritdoc/>
    protected override ITypeShape PropertyTypeNonGeneric => PropertyType;

    /// <inheritdoc/>
    protected override IObjectTypeShape DeclaringTypeNonGeneric => DeclaringType;

    /// <inheritdoc/>
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitProperty(this, state);

    /// <summary>
    /// Creates a getter delegate for the property, if applicable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The property has no accessible getter.</exception>
    /// <returns>A getter delegate for the property.</returns>
    public abstract Getter<TDeclaringType, TPropertyType> GetGetter();

    /// <summary>
    /// Creates a setter delegate for the property, if applicable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The property has no accessible setter.</exception>
    /// <returns>A setter delegate for the property.</returns>
    public abstract Setter<TDeclaringType, TPropertyType> GetSetter();
}