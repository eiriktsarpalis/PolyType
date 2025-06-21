namespace PolyType.ReflectionProvider;

internal enum ConstructionSignature
{
    /// <summary>
    /// No construction accepts any kind of comparer.
    /// </summary>
    None,

    Values,

    /// <summary>
    /// The construction expects just the <see cref="IComparer{T}"/>.
    /// </summary>
    Comparer,

    /// <summary>
    /// The construction expects a <see cref="IComparer{T}"/> then the values.
    /// </summary>
    ComparerValues,

    /// <summary>
    /// The construction expects values and then a <see cref="IComparer{T}"/>.
    /// </summary>
    ValuesComparer,

    /// <summary>
    /// The construction expects just the <see cref="IEqualityComparer{T}"/>.
    /// </summary>
    EqualityComparer,

    /// <summary>
    /// The construction expects an <see cref="IEqualityComparer{T}"/> then the values.
    /// </summary>
    EqualityComparerValues,

    /// <summary>
    /// The construction expects values and then an <see cref="IEqualityComparer{T}"/>.
    /// </summary>
    ValuesEqualityComparer,
}
