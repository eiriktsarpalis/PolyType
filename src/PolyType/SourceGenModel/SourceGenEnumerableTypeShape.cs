using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for enumerable shapes.
/// </summary>
/// <typeparam name="TEnumerable">The type of the enumerable collection.</typeparam>
/// <typeparam name="TElement">The element type of the collection.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.EnumerableTypeShapeDebugView))]
public sealed class SourceGenEnumerableTypeShape<TEnumerable, TElement> : SourceGenTypeShape<TEnumerable>, IEnumerableTypeShape<TEnumerable, TElement>
{
    /// <inheritdoc/>
    public required ITypeShape<TElement> ElementType { get; init; }

    /// <inheritdoc/>
    public required int Rank { get; init; }

    /// <inheritdoc/>
    public bool IsAsyncEnumerable { get; init; }

    /// <inheritdoc/>
    public bool IsSetType { get; init; }

    /// <summary>
    /// Gets the function that retrieves an enumerable from an instance of the collection.
    /// </summary>
    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerable { get; init; }

    /// <inheritdoc/>
    public required CollectionComparerOptions SupportedComparer { get; init; }

    /// <summary>
    /// Gets the construction strategy for the collection.
    /// </summary>
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }

    /// <summary>
    /// Gets the function that constructs an empty instance of the collection.
    /// </summary>
    public MutableCollectionConstructor<TElement, TEnumerable>? DefaultConstructor { get; init; }

    /// <summary>
    /// Gets the function that appends an element to the collection.
    /// </summary>
    public EnumerableAppender<TEnumerable, TElement>? Appender { get; init; }

    /// <summary>
    /// Gets the function that constructs a collection from a span.
    /// </summary>
    public ParameterizedCollectionConstructor<TElement, TElement, TEnumerable>? ParameterizedConstructor { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Enumerable;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnumerable(this, state);

    ITypeShape IEnumerableTypeShape.ElementType => ElementType;

    Func<TEnumerable, IEnumerable<TElement>> IEnumerableTypeShape<TEnumerable, TElement>.GetGetEnumerable() => GetEnumerable;

    MutableCollectionConstructor<TElement, TEnumerable> IEnumerableTypeShape<TEnumerable, TElement>.GetDefaultConstructor() =>
        DefaultConstructor ?? throw new InvalidOperationException("Enumerable shape does not specify a default constructor.");

    EnumerableAppender<TEnumerable, TElement> IEnumerableTypeShape<TEnumerable, TElement>.GetAppender() =>
        Appender ?? throw new InvalidOperationException("Enumerable shape does not specify an append delegate.");

    ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> IEnumerableTypeShape<TEnumerable, TElement>.GetParameterizedConstructor() =>
        ParameterizedConstructor ?? throw new InvalidOperationException("Enumerable shape does not specify a span constructor.");
}
