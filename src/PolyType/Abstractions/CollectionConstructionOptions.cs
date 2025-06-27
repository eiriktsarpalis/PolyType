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
/// <see cref="IEnumerableTypeShape.SupportedComparer"/> or <see cref="IDictionaryTypeShape.SupportedComparer"/>.
/// </para>
/// </remarks>
public readonly struct CollectionConstructionOptions<TKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionConstructionOptions{TKey}"/> struct.
    /// </summary>
    /// <param name="copyFrom">A template to copy all properties from.</param>
    public CollectionConstructionOptions(CollectionConstructionOptions<TKey> copyFrom) => this = copyFrom;

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
