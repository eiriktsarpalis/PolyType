namespace PolyType.SourceGenerator.Model;

public enum CollectionConstructorParameter
{
    /// <summary>
    /// This parameter expects an <see cref="IComparer{T}"/>.
    /// </summary>
    Comparer,

    /// <summary>
    /// This parameter expects an <see cref="IEqualityComparer{T}"/>.
    /// </summary>
    EqualityComparer,

    /// <summary>
    /// This parameter expects collection elements.
    /// </summary>
    Values,

    /// <summary>
    /// This parameter expects a capacity for the collection.
    /// </summary>
    Capacity,
}
