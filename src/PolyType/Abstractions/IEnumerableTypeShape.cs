﻿using System.Collections;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET type that is enumerable.
/// </summary>
/// <remarks>
/// Typically covers all types implementing <see cref="IEnumerable{T}"/> or <see cref="IEnumerable"/>.
/// </remarks>
public interface IEnumerableTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the shape of the underlying element type.
    /// </summary>
    /// <remarks>
    /// For non-generic <see cref="IEnumerable"/> this returns the shape for <see cref="object"/>.
    /// </remarks>
    ITypeShape ElementType { get; }

    /// <summary>
    /// Gets the construction strategy for the given collection.
    /// </summary>
    CollectionConstructionStrategy ConstructionStrategy { get; }

    /// <summary>
    /// Gets the rank of the enumerable, if a multidimensional array.
    /// </summary>
    int Rank { get; }

    /// <summary>
    /// Indicates whether the underlying type is an IAsyncEnumerable.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="IEnumerableTypeShape{TEnumerable, TElement}.GetGetEnumerable"/> on async enumerable instances
    /// will result in an exception being thrown to prevent accidental sync-over-async. Users should manually cast
    /// instances to IAsyncEnumerable and enumerate elements asynchronously.
    /// </remarks>
    bool IsAsyncEnumerable { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET type that is enumerable.
/// </summary>
/// <typeparam name="TEnumerable">The type of underlying enumerable.</typeparam>
/// <typeparam name="TElement">The type of underlying element.</typeparam>
/// <remarks>
/// Typically covers all types implementing <see cref="IEnumerable{T}"/> or <see cref="IEnumerable"/>.
///
/// For non-generic collections, <typeparamref name="TElement"/> is instantiated to <see cref="object"/>.
/// </remarks>
public interface IEnumerableTypeShape<TEnumerable, TElement> : ITypeShape<TEnumerable>, IEnumerableTypeShape
{
    /// <summary>
    /// Gets the shape of the underlying element type.
    /// </summary>
    /// <remarks>
    /// For non-generic <see cref="IEnumerable"/> this returns the shape for <see cref="object"/>.
    /// </remarks>
    new ITypeShape<TElement> ElementType { get; }

    /// <summary>
    /// Creates a delegate used for getting an <see cref="IEnumerable{TElement}"/>
    /// view of the enumerable.
    /// </summary>
    /// <returns>
    /// A delegate accepting a <typeparamref name="TEnumerable"/> and
    /// returning an <see cref="IEnumerable{TElement}"/> view of the instance.
    /// </returns>
    Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();

    /// <summary>
    /// Creates a delegate wrapping a parameterless constructor of a mutable collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A delegate wrapping a default constructor.</returns>
    Func<TEnumerable> GetDefaultConstructor();

    /// <summary>
    /// Creates a setter delegate used for appending a <typeparamref name="TElement"/> to a mutable collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A setter delegate used for appending elements to a mutable collection.</returns>
    Setter<TEnumerable, TElement> GetAddElement();

    /// <summary>
    /// Creates a constructor delegate for creating a collection from a span.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Span"/>.</exception>
    /// <returns>A delegate constructing a collection from a span of values.</returns>
    SpanConstructor<TElement, TEnumerable> GetSpanConstructor();

    /// <summary>
    /// Creates a constructor delegate for creating a collection from an enumerable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Enumerable"/>.</exception>
    /// <returns>A delegate constructing a collection from an enumerable of values.</returns>
    Func<IEnumerable<TElement>, TEnumerable> GetEnumerableConstructor();
}