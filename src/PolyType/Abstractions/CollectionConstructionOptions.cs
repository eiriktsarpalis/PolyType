namespace PolyType.Abstractions;

/// <summary>
/// Options for constructing collections.
/// </summary>
/// <typeparam name="TKey">The type of keys within the collection. When collections aren't keyed, this is the element type directly.</typeparam>
public readonly struct CollectionConstructionOptions<TKey>
{
    /// <summary>
    /// Gets an optional comparer for keys or elements in the collection.
    /// </summary>
    public IEqualityComparer<TKey>? EqualityComparer { get; init; }
}