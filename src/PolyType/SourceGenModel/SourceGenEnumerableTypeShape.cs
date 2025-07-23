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
    /// Indicates whether the enumerable is one of the recognized set collection types.
    /// </summary>
    public required bool IsSetType { get; init; }

    /// <summary>
    /// Gets the function that retrieves an enumerable from an instance of the collection.
    /// </summary>
    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerableFunc { get; init; }

    /// <inheritdoc/>
    public required CollectionComparerOptions SupportedComparer { get; init; }

    /// <summary>
    /// Gets the construction strategy for the collection.
    /// </summary>
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }

    /// <summary>
    /// Gets the function that constructs a default instance of the collection.
    /// </summary>
    public MutableCollectionConstructor<TElement, TEnumerable>? MutableConstructorFunc { get; init; }

    /// <summary>
    /// Gets the function that appends an element to the collection.
    /// </summary>
    public EnumerableAppender<TEnumerable, TElement>? AppenderFunc { get; init; }

    /// <summary>
    /// Gets the function that constructs a collection from a span.
    /// </summary>
    public ParameterizedCollectionConstructor<TElement, TElement, TEnumerable>? SpanConstructorFunc { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Enumerable;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnumerable(this, state);

    ITypeShape IEnumerableTypeShape.ElementType => ElementType;

    Func<TEnumerable, IEnumerable<TElement>> IEnumerableTypeShape<TEnumerable, TElement>.GetGetEnumerable()
        => GetEnumerableFunc;

    MutableCollectionConstructor<TElement, TEnumerable> IEnumerableTypeShape<TEnumerable, TElement>.GetMutableConstructor()
        => MutableConstructorFunc ?? throw new InvalidOperationException("Enumerable shape does not specify a default constructor.");

    EnumerableAppender<TEnumerable, TElement> IEnumerableTypeShape<TEnumerable, TElement>.GetAppender()
        => AppenderFunc ?? throw new InvalidOperationException("Enumerable shape does not specify an append delegate.");

    ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> IEnumerableTypeShape<TEnumerable, TElement>.GetParameterizedConstructor()
        => SpanConstructorFunc ?? throw new InvalidOperationException("Enumerable shape does not specify a span constructor.");
}
