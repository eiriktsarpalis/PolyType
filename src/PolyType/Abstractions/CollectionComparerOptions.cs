namespace PolyType.Abstractions;

/// <summary>
/// The kinds of comparers that may be supplied to a collection's constructor.
/// </summary>
public enum CollectionComparerOptions
{
    /// <summary>
    /// The collection has no recognizable support for custom comparers.
    /// </summary>
    None,

    /// <summary>
    /// The collection accepts a custom <see cref="IEqualityComparer{T}"/> for hashing and/or equality checks.
    /// </summary>
    EqualityComparer,

    /// <summary>
    /// The collection accepts a custom <see cref="IComparer{T}"/> for sorting and/or equality checks.
    /// </summary>
    Comparer,
}
