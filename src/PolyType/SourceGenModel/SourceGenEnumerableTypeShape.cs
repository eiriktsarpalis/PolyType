using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for enumerable shapes.
/// </summary>
/// <typeparam name="TEnumerable">The type of the enumerable collection.</typeparam>
/// <typeparam name="TElement">The element type of the collection.</typeparam>
public sealed class SourceGenEnumerableTypeShape<TEnumerable, TElement>(SourceGenTypeShapeProvider provider) : IEnumerableTypeShape<TEnumerable, TElement>(provider)
{
    /// <summary>
    /// Gets the function that retrieves an enumerable from an instance of the collection.
    /// </summary>
    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerableFunc { get; init; }

    /// <summary>
    /// Gets the shape of the element type.
    /// </summary>
    public required ITypeShape<TElement> ElementTypeSetter { private get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TElement> ElementType => ElementTypeSetter;

    /// <summary>
    /// Gets the rank of the enumerable collection.
    /// </summary>
    public required int RankSetter { private get; init; }

    /// <inheritdoc/>
    public override int Rank => RankSetter;

    /// <summary>
    /// Indicates whether the underlying type is an IAsyncEnumerable or not.
    /// </summary>
    public required bool IsAsyncEnumerableSetter { private get; init; }

    /// <inheritdoc/>
    public override bool IsAsyncEnumerable => IsAsyncEnumerableSetter;

    /// <inheritdoc cref="ComparerOptions"/>
    public required CollectionComparerOptions ComparerOptionsSetter { private get; init; }

    /// <inheritdoc/>
    public override CollectionComparerOptions ComparerOptions => ComparerOptionsSetter;

    /// <summary>
    /// Gets the construction strategy for the collection.
    /// </summary>
    public required CollectionConstructionStrategy ConstructionStrategySetter { private get; init; }

    /// <inheritdoc/>
    public override CollectionConstructionStrategy ConstructionStrategy => ConstructionStrategySetter;

    /// <summary>
    /// Gets the function that constructs a default instance of the collection.
    /// </summary>
    public Func<CollectionConstructionOptions<TElement>?, Func<TEnumerable>>? DefaultConstructorFunc { get; init; }

    /// <summary>
    /// Gets the function that adds an element to the collection.
    /// </summary>
    public Setter<TEnumerable, TElement>? AddElementFunc { get; init; }

    /// <summary>
    /// Gets the function that constructs a collection from an enumerable.
    /// </summary>
    public Func<CollectionConstructionOptions<TElement>?, Func<IEnumerable<TElement>, TEnumerable>>? EnumerableConstructorFunc { get; init; }

    /// <summary>
    /// Gets the function that constructs a collection from a span.
    /// </summary>
    public Func<CollectionConstructionOptions<TElement>?, SpanConstructor<TElement, TEnumerable>>? SpanConstructorFunc { get; init; }

    /// <summary>
    /// Gets the shape of an associated type, by its name.
    /// </summary>
    public Func<string, ITypeShape?>? AssociatedTypeShapes { get; init; }

    /// <inheritdoc/>
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => GetEnumerableFunc;

    /// <inheritdoc/>
    public override Func<TEnumerable> GetDefaultConstructor(CollectionConstructionOptions<TElement>? collectionConstructionOptions)
        => DefaultConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Enumerable shape does not specify a default constructor.");

    /// <inheritdoc/>
    public override Setter<TEnumerable, TElement> GetAddElement()
        => AddElementFunc ?? throw new InvalidOperationException("Enumerable shape does not specify an append delegate.");

    /// <inheritdoc/>
    public override Func<IEnumerable<TElement>, TEnumerable> GetEnumerableConstructor(CollectionConstructionOptions<TElement>? collectionConstructionOptions)
        => EnumerableConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Enumerable shape does not specify an enumerable constructor.");

    /// <inheritdoc/>
    public override SpanConstructor<TElement, TEnumerable> GetSpanConstructor(CollectionConstructionOptions<TElement>? collectionConstructionOptions)
        => SpanConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Enumerable shape does not specify a span constructor.");

    /// <inheritdoc/>
    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, AssociatedTypeShapes, associatedType);
}
