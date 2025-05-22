namespace PolyType.ReflectionProvider;

internal enum CollectionConstructorParameterType
{
    /// <summary>
    /// The parameter isn't a recognized type.
    /// </summary>
    Unrecognized,

    /// <summary>
    /// The parameter serves as some type of collection.
    /// </summary>
    CollectionOfT,

    /// <summary>
    /// The parameter is an <see cref="IEqualityComparer{T}"/>.
    /// </summary>
    IEqualityComparerOfT,

    /// <summary>
    /// The parameter is an <see cref="IComparer{T}"/>.
    /// </summary>
    IComparerOfT,
}
