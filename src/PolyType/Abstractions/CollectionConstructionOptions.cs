namespace PolyType.Abstractions;

/// <summary>
/// Options for constructing collections.
/// </summary>
/// <typeparam name="TKey">The type of keys within the collection. When collections aren't keyed, this is the element type directly.</typeparam>
/// <remarks>
/// <para>
/// Construction of any particular collection type may ignore any or all of these properties.
/// To predict whether a collection uses a particular property, check for the presence of its associated flag
/// on the <see cref="CollectionComparerOptions"/> enum, as defined by either
/// <see cref="IEnumerableTypeShape.SupportedComparers"/> or <see cref="IDictionaryTypeShape.SupportedComparers"/>.
/// </para>
/// <para>
/// When <see cref="CollectionComparerOptions.EqualityComparer"/> and <see cref="CollectionComparerOptions.Comparer"/>
/// flags are both set, initializing only one of the comparers on this struct will direct the collection to use that comparer.
/// Initializing both introduces an ambiguity which PolyType will resolve by selecting the <see cref="EqualityComparer"/>.
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

    /// <summary>
    /// Gets the initial capacity of the collection.
    /// </summary>
    public int? Capacity { get; init; }
}
