namespace PolyType.Abstractions;

/// <summary>
/// Options for constructing collections.
/// </summary>
/// <typeparam name="TKey">The type of keys within the collection. When collections aren't keyed, this is the element type directly.</typeparam>
/// <remarks>
/// <para>
/// Construction of any particular collection type may ignore any or all of these properties.
/// To predict whether a collection will use a particular comparer property, check the
/// <see cref="CollectionComparerOptions"/> enum, as defined by either
/// <see cref="IEnumerableTypeShape.SupportedComparers"/> or <see cref="IDictionaryTypeShape.SupportedComparers"/>.
/// </para>
/// </remarks>
public struct CollectionConstructionOptions<TKey>
{
    /// <summary>
    /// Gets an optional equality comparer for the keys or elements in the collection.
    /// </summary>
    public IEqualityComparer<TKey>? EqualityComparer { get; init; }

    /// <summary>
    /// Gets an optional comparer for the keys or elements in the collection.
    /// </summary>
    public IComparer<TKey>? Comparer { get; init; }
}
