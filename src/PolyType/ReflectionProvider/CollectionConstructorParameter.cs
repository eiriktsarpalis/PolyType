using PolyType.Abstractions;
using System.Collections.ObjectModel;

namespace PolyType.ReflectionProvider;

internal enum CollectionConstructorParameter
{
    /// <summary>
    /// Unrecognized constructor parameter type.
    /// </summary>
    Unrecognized,

    /// <summary>
    /// Accepts values of type <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    Span,

    /// <summary>
    /// Accepts parameters assignable from <see cref="List{T}"/>, e.g. IList, IReadOnlyList, etc.
    /// </summary>
    List,

    /// <summary>
    /// Accepts parameters assignable from <see cref="HashSet{T}"/>, e.g. ISet, IReadOnlySet, etc.
    /// </summary>
    HashSet,

    /// <summary>
    /// Accepts parameters assignable from <see cref="Dictionary{TKey, TValue}"/>, e.g. IReadOnlyDictionary, IDictionary, etc.
    /// </summary>
    Dictionary,

    /// <summary>
    /// Accepts a class tuple of key-value pairs, reserved for the FSharp map constructor.
    /// </summary>
    TupleEnumerable,

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