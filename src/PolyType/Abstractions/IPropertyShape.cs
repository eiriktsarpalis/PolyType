﻿using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET instance property or field.
/// </summary>
public interface IPropertyShape
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the provider used for property-level attribute resolution.
    /// </summary>
    ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Gets the shape of the declaring type.
    /// </summary>
    IObjectTypeShape DeclaringType { get; }

    /// <summary>
    /// Gets the shape of the property type.
    /// </summary>
    ITypeShape PropertyType { get; }

    /// <summary>
    /// Gets a value indicating whether the property has an accessible getter.
    /// </summary>
    bool HasGetter { get; }

    /// <summary>
    /// Gets a value indicating whether the property has an accessible setter.
    /// </summary>
    bool HasSetter { get; }

    /// <summary>
    /// Gets a value indicating whether the shape represents a .NET field.
    /// </summary>
    bool IsField { get; }

    /// <summary>
    /// Gets a value indicating whether the property getter is declared public.
    /// </summary>
    bool IsGetterPublic { get; }

    /// <summary>
    /// Gets a value indicating whether the property setter is declared public.
    /// </summary>
    bool IsSetterPublic { get; }

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
    bool IsGetterNonNullable { get; }

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
    bool IsSetterNonNullable { get; }

    /// <summary>
    /// Accepts an <see cref="TypeShapeVisitor"/> for strongly typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(TypeShapeVisitor visitor, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET instance property or field.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type of the underlying property.</typeparam>
/// <typeparam name="TPropertyType">The property type of the underlying property.</typeparam>
public interface IPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape
{
    /// <summary>
    /// Gets the shape of the declaring type.
    /// </summary>
    new IObjectTypeShape<TDeclaringType> DeclaringType { get; }

    /// <summary>
    /// Gets the shape of the property type.
    /// </summary>
    new ITypeShape<TPropertyType> PropertyType { get; }

    /// <summary>
    /// Creates a getter delegate for the property, if applicable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The property has no accessible getter.</exception>
    /// <returns>A getter delegate for the property.</returns>
    Getter<TDeclaringType, TPropertyType> GetGetter();

    /// <summary>
    /// Creates a setter delegate for the property, if applicable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The property has no accessible setter.</exception>
    /// <returns>A setter delegate for the property.</returns>
    Setter<TDeclaringType, TPropertyType> GetSetter();
}