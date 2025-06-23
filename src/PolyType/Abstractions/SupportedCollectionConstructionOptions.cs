using System.Collections.ObjectModel;

namespace PolyType.Abstractions;

/// <summary>
/// The kinds of comparers that may be supplied to a collection's constructor.
/// </summary>
/// <remarks>
/// <para>
/// Each of these flags correspond to a property on the <see cref="CollectionConstructionOptions{TKey}"/> type.
/// </para>
/// <para>
/// The <see cref="EqualityComparer"/> and <see cref="Comparer"/> flags are usually mutually exclusive.
/// However some types such as <see cref="IReadOnlyDictionary{TKey, TValue}"/>, <see cref="ReadOnlyDictionary{TKey, TValue}"/> and <see cref="ISet{T}"/>
/// are capable of being initialized with either an <see cref="IEqualityComparer{T}"/> or an <see cref="IComparer{T}"/>.
/// When both of these flags are set, the <see cref="CollectionConstructionOptions{TKey}"/> struct may determine which
/// is used by initializing just one of <see cref="CollectionConstructionOptions{TKey}.EqualityComparer"/> and <see cref="CollectionConstructionOptions{TKey}.Comparer"/>.
/// If both are set, the collection will be initialized with the <see cref="EqualityComparer"/>.
/// This is the same behavior as if neither property was set, when such a collection is initialized with <see cref="EqualityComparer{T}.Default"/>
/// (or whatever the default is for the collection type).
/// </para>
/// </remarks>
[Flags]
public enum SupportedCollectionConstructionOptions
{
    /// <summary>
    /// The collection has no recognizable support for custom comparers.
    /// </summary>
    None,

    /// <summary>
    /// The collection accepts a custom <see cref="IEqualityComparer{T}"/> for hashing and/or equality checks.
    /// </summary>
    /// <seealso cref="CollectionConstructionOptions{TKey}.EqualityComparer"/>
    EqualityComparer,

    /// <summary>
    /// The collection accepts a custom <see cref="IComparer{T}"/> for sorting and/or equality checks.
    /// </summary>
    /// <seealso cref="CollectionConstructionOptions{TKey}.Comparer"/>
    Comparer,
}
