using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for enumerable shapes.
/// </summary>
/// <typeparam name="TEnumerable">The type of the enumerable collection.</typeparam>
/// <typeparam name="TElement">The element type of the collection.</typeparam>
public sealed class SourceGenEnumerableTypeShape<TEnumerable, TElement> : SourceGenTypeShape<TEnumerable>, IEnumerableTypeShape<TEnumerable, TElement>
{
    /// <summary>
    /// Gets the shape of the element type.
    /// </summary>
    public required ITypeShape<TElement> ElementType { get; init; }

    /// <summary>
    /// Gets the rank of the enumerable collection.
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// Indicates whether the underlying type is an IAsyncEnumerable or not.
    /// </summary>
    public required bool IsAsyncEnumerable { get; init; }

    /// <summary>
    /// Gets the function that retrieves an enumerable from an instance of the collection.
    /// </summary>
    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerableFunc { get; init; }

    /// <inheritdoc/>
    public required CollectionComparerOptions ComparerOptions { get; init; }

    /// <summary>
    /// Gets the construction strategy for the collection.
    /// </summary>
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }

    /// <summary>
    /// Gets the function that constructs a default instance of the collection.
    /// </summary>
    public InFunc<CollectionConstructionOptions<TElement>, Func<TEnumerable>>? DefaultConstructorFunc { get; init; }

    /// <summary>
    /// Gets the function that adds an element to the collection.
    /// </summary>
    public Setter<TEnumerable, TElement>? AddElementFunc { get; init; }

    /// <summary>
    /// Gets the function that constructs a collection from an enumerable.
    /// </summary>
    public InFunc<CollectionConstructionOptions<TElement>, Func<IEnumerable<TElement>, TEnumerable>>? EnumerableConstructorFunc { get; init; }

    /// <summary>
    /// Gets the function that constructs a collection from a span.
    /// </summary>
    public InFunc<CollectionConstructionOptions<TElement>, SpanConstructor<TElement, TEnumerable>>? SpanConstructorFunc { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Enumerable;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnumerable(this, state);

    ITypeShape IEnumerableTypeShape.ElementType => ElementType;

    Func<TEnumerable, IEnumerable<TElement>> IEnumerableTypeShape<TEnumerable, TElement>.GetGetEnumerable()
        => GetEnumerableFunc;

    Func<TEnumerable> IEnumerableTypeShape<TEnumerable, TElement>.GetDefaultConstructor(in CollectionConstructionOptions<TElement> collectionConstructionOptions)
        => DefaultConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Enumerable shape does not specify a default constructor.");

    Setter<TEnumerable, TElement> IEnumerableTypeShape<TEnumerable, TElement>.GetAddElement()
        => AddElementFunc ?? throw new InvalidOperationException("Enumerable shape does not specify an append delegate.");

    Func<IEnumerable<TElement>, TEnumerable> IEnumerableTypeShape<TEnumerable, TElement>.GetEnumerableConstructor(in CollectionConstructionOptions<TElement> collectionConstructionOptions)
        => EnumerableConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Enumerable shape does not specify an enumerable constructor.");

    SpanConstructor<TElement, TEnumerable> IEnumerableTypeShape<TEnumerable, TElement>.GetSpanConstructor(in CollectionConstructionOptions<TElement> collectionConstructionOptions)
        => SpanConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Enumerable shape does not specify a span constructor.");
}
