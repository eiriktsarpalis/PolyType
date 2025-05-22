using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a property shape.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type of the property.</typeparam>
/// <typeparam name="TPropertyType">The type of the property value.</typeparam>
public sealed class SourceGenPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape<TDeclaringType, TPropertyType>
{
    /// <summary>
    /// Gets the custom attribute provider for the property.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public required string NameSetter { private get; init; }

    /// <inheritdoc/>
    public override string Name => NameSetter;

    /// <summary>
    /// Gets the shape of the declaring type.
    /// </summary>
    public required IObjectTypeShape<TDeclaringType> DeclaringTypeSetter { private get; init; }

    /// <inheritdoc/>
    public override IObjectTypeShape<TDeclaringType> DeclaringType => DeclaringTypeSetter;

    /// <summary>
    /// Gets the shape of the property type.
    /// </summary>
    public required ITypeShape<TPropertyType> PropertyTypeSetter { private get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TPropertyType> PropertyType => PropertyTypeSetter;

    /// <summary>
    /// Gets the getter delegate for the property.
    /// </summary>
    public Getter<TDeclaringType, TPropertyType>? Getter { get; init; }

    /// <summary>
    /// Gets the setter delegate for the property.
    /// </summary>
    public Setter<TDeclaringType, TPropertyType>? Setter { get; init; }

    /// <summary>
    /// Gets a value indicating whether the getter is declared public.
    /// </summary>
    public required bool IsGetterPublicSetter { get; init; }

    /// <inheritdoc/>
    public override bool IsGetterPublic => IsGetterPublicSetter;

    /// <summary>
    /// Gets a value indicating whether the setter is declared public.
    /// </summary>
    public required bool IsSetterPublicSetter { get; init; }

    /// <inheritdoc/>
    public override bool IsSetterPublic => IsSetterPublicSetter;

    /// <summary>
    /// Gets a value indicating whether the getter is non-nullable.
    /// </summary>
    public required bool IsGetterNonNullableSetter { get; init; }

    /// <inheritdoc/>
    public override bool IsGetterNonNullable => IsGetterNonNullableSetter;

    /// <summary>
    /// Gets a value indicating whether the setter is non-nullable.
    /// </summary>
    public required bool IsSetterNonNullableSetter { get; init; }

    /// <inheritdoc/>
    public override bool IsSetterNonNullable => IsSetterNonNullableSetter;

    /// <summary>
    /// Gets a value indicating whether the shape represents a field.
    /// </summary>
    public bool IsFieldSetter { get; init; }

    /// <inheritdoc/>
    public override bool IsField => IsFieldSetter;

    /// <inheritdoc/>
    public override Getter<TDeclaringType, TPropertyType> GetGetter()
        => Getter is { } getter ? getter : throw new InvalidOperationException("Property shape does not specify a getter.");

    /// <inheritdoc/>
    public override Setter<TDeclaringType, TPropertyType> GetSetter()
        => Setter is { } setter ? setter : throw new InvalidOperationException("Property shape does not specify a setter.");

    /// <inheritdoc/>
    public override bool HasGetter => Getter is not null;

    /// <inheritdoc/>
    public override bool HasSetter => Setter is not null;

    /// <inheritdoc/>
    public override ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc?.Invoke();
}
