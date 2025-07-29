using System.Collections;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET type that is enumerable.
/// </summary>
/// <remarks>
/// Typically covers all types implementing <see cref="IEnumerable{T}"/> or <see cref="IEnumerable"/>.
/// </remarks>
[InternalImplementationsOnly]
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
    /// Gets the kind of custom comparer (if any) that this collection may be initialized with.
    /// </summary>
    CollectionComparerOptions SupportedComparer { get; }

    /// <summary>
    /// Gets the dimensionality of the collection type.
    /// </summary>
    /// <value>
    /// <see cref="IEnumerable{T}"/>, most collections and most arrays have a rank of 1.
    /// </value>
    /// <remarks>
    /// Test for arrays by using <see cref="Type.IsArray"/> on <see cref="ITypeShape.Type"/>.
    /// </remarks>
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

    /// <summary>
    /// Indicates whether the enumerable is one of the recognized set collection types.
    /// </summary>
    bool IsSetType { get; }
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
[InternalImplementationsOnly]
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
    /// Creates a delegate for creating an empty, mutable collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A delegate for creating an empty mutable collection.</returns>
    MutableCollectionConstructor<TElement, TEnumerable> GetDefaultConstructor();

    /// <summary>
    /// Creates a delegate used for appending a <typeparamref name="TElement"/> to a mutable collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A setter delegate used for appending elements to a mutable collection.</returns>
    EnumerableAppender<TEnumerable, TElement> GetAppender();

    /// <summary>
    /// Creates a delegate for creating a collection from a span.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Parameterized"/>.</exception>
    /// <returns>A delegate constructing a collection from a span of values.</returns>
    ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> GetParameterizedConstructor();
}