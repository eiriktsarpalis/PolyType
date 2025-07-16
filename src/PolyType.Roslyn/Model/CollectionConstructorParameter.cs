using System.Collections.ObjectModel;

namespace PolyType.Roslyn;

/// <summary>
/// Models acceptable constructor parameters for collection types.
/// </summary>
public enum CollectionConstructorParameter
{
    /// <summary>
    /// Unrecognized constructor parameter type.
    /// </summary>
    Unrecognized,

    /// <summary>
    /// Accepts values of type <see cref="IEnumerable{T}"/>.
    /// </summary>
    Enumerable,

    /// <summary>
    /// Accepts values of type <see cref="List{T}"/>, e.g. <see cref="ReadOnlyCollection{T}"/>.
    /// </summary>
    List,

    /// <summary>
    /// Accepts values of type <see cref="HashSet{T}"/>, e.g. ReadOnlySet.
    /// </summary>
    HashSet,

    /// <summary>
    /// Accepts values of type <see cref="Dictionary{TKey, TValue}"/>, e.g. <see cref="ReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    Dictionary,

    /// <summary>
    /// Accepts enumerables of type <see cref="System.Tuple{T1, T2}"/>, e.g. F# maps.
    /// </summary>
    TupleEnumerable,

    /// <summary>
    /// Accepts values of type <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    Span,

    /// <summary>
    /// Accepts a numeric capacity parameter.
    /// </summary>
    Capacity,

    /// <summary>
    /// Accepts an optional numeric capacity parameter.
    /// </summary>
    CapacityOptional,

    /// <summary>
    /// Accepts an <see cref="IEqualityComparer{T}"/> parameter.
    /// </summary>
    EqualityComparer,

    /// <summary>
    /// Accepts an optional <see cref="IEqualityComparer{T}"/> parameter.
    /// </summary>
    EqualityComparerOptional,

    /// <summary>
    /// Accepts an <see cref="IComparer{T}"/> parameter.
    /// </summary>
    Comparer,

    /// <summary>
    /// Accepts an optional <see cref="IComparer{T}"/> parameter.
    /// </summary>
    ComparerOptional,
}
