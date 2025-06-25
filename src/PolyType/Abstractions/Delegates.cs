﻿using System.Diagnostics.CodeAnalysis;

namespace PolyType.Abstractions;

/// <summary>
/// Delegate representing a property/field getter.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type from which to get a value.</typeparam>
/// <typeparam name="TPropertyType">The property type of the underlying getter.</typeparam>
/// <param name="obj">The instance from which to get the value.</param>
/// <returns>The value returned by the getter.</returns>
public delegate TPropertyType Getter<TDeclaringType, TPropertyType>(ref TDeclaringType obj);

/// <summary>
/// Delegate representing a property/field setter.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type on which to set a value.</typeparam>
/// <typeparam name="TPropertyType">The property type of the underlying setter.</typeparam>
/// <param name="obj">The instance on which to set the value.</param>
/// <param name="value">The value to be set to the instance.</param>
public delegate void Setter<TDeclaringType, TPropertyType>(ref TDeclaringType obj, TPropertyType value);

/// <summary>
/// Delegate representing a parameterized constructor.
/// </summary>
/// <typeparam name="TArgumentState">Type of the state object containing all constructor arguments.</typeparam>
/// <typeparam name="TDeclaringType">Type of the object to be constructed.</typeparam>
/// <param name="state">State object containing all constructor arguments.</param>
/// <returns>The instance created by the constructor.</returns>
public delegate TDeclaringType Constructor<TArgumentState, TDeclaringType>(ref TArgumentState state);

/// <summary>
/// Delegate representing a constructor that accepts a span of values.
/// </summary>
/// <typeparam name="TKey">The type to be compared within the <typeparamref name="TElement"/>.</typeparam>
/// <typeparam name="TElement">The element type of the span parameters.</typeparam>
/// <typeparam name="TDeclaringType">The type of the value produced by the constructor.</typeparam>
/// <param name="values">The span of values used to create the instance.</param>
/// <param name="options">An optional set of parameters used to create the collection.</param>
/// <returns>A newly constructed instance using the specified values.</returns>
public delegate TDeclaringType SpanCollectionConstructor<TKey, TElement, TDeclaringType>(ReadOnlySpan<TElement> values, in CollectionConstructionOptions<TKey> options = default);

/// <summary>
/// Delegate representing a constructor for a mutable collection.
/// </summary>
/// <typeparam name="TKey">The type of keys or elements in the collection.</typeparam>
/// <typeparam name="TDeclaringType">The type of the collection to be constructed.</typeparam>
/// <param name="options">An optional set of parameters used to create the collection.</param>
/// <returns>A newly constructed mutable collection instance.</returns>
public delegate TDeclaringType MutableCollectionConstructor<TKey, TDeclaringType>(in CollectionConstructionOptions<TKey> options = default);

/// <summary>
/// Delegate representing a constructor for a collection from an enumeration of elements.
/// </summary>
/// <typeparam name="TKey">The type of keys or elements in the collection.</typeparam>
/// <typeparam name="TElement">The element type of the enumerable.</typeparam>
/// <typeparam name="TDeclaringType">The type of the collection to be constructed.</typeparam>
/// <param name="elements">The enumerable of elements used to create the collection.</param>
/// <param name="options">An optional set of parameters used to create the collection.</param>
/// <returns>A newly constructed collection instance containing the specified elements.</returns>
public delegate TDeclaringType EnumerableCollectionConstructor<TKey, TElement, TDeclaringType>(IEnumerable<TElement> elements, in CollectionConstructionOptions<TKey> options = default);

/// <summary>
/// Delegate deconstructing an optional type.
/// </summary>
/// <typeparam name="TOptional">The optional type to deconstruct.</typeparam>
/// <typeparam name="TElement">The value encapsulated by the option type.</typeparam>
/// <param name="optional">The optional value to deconstruct.</param>
/// <param name="value">The value potentially contained in <paramref name="optional"/>.</param>
/// <returns><see langword="true"/> if the <paramref name="optional"/> contains a value, or <see langword="false"/> if it does not.</returns>
public delegate bool OptionDeconstructor<TOptional, TElement>(TOptional? optional, [MaybeNullWhen(false)] out TElement value);
