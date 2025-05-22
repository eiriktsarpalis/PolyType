using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for dictionary shapes.
/// </summary>
/// <typeparam name="TDictionary">The type of the dictionary.</typeparam>
/// <typeparam name="TKey">The type of the dictionary key.</typeparam>
/// <typeparam name="TValue">The type of the dictionary value.</typeparam>
public sealed class SourceGenDictionaryTypeShape<TDictionary, TKey, TValue>(SourceGenTypeShapeProvider provider) : IDictionaryTypeShape<TDictionary, TKey, TValue>(provider)
    where TKey : notnull
{
    /// <summary>
    /// Gets the function that extracts a <see cref="IReadOnlyDictionary{TKey,TValue}"/> from an instance of the dictionary type.
    /// </summary>
    public required Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetDictionaryFunc { get; init; }

    /// <summary>
    /// Gets the type shape of the dictionary key.
    /// </summary>
    public required ITypeShape<TKey> KeyTypeSetter { private get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TKey> KeyType => KeyTypeSetter;

    /// <summary>
    /// Gets the type shape of the dictionary value.
    /// </summary>
    public required ITypeShape<TValue> ValueTypeSetter { private get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TValue> ValueType => ValueTypeSetter;

    /// <summary>
    /// Gets the construction strategy for the dictionary.
    /// </summary>
    public required CollectionConstructionStrategy ConstructionStrategySetter { private get; init; }

    /// <inheritdoc/>
    public override CollectionConstructionStrategy ConstructionStrategy => ConstructionStrategySetter;

    /// <inheritdoc cref="ComparerOptions"/>
    public required CollectionComparerOptions ComparerOptionsSetter { private get; init; }

    /// <inheritdoc/>
    public override CollectionComparerOptions ComparerOptions => ComparerOptionsSetter;

    /// <summary>
    /// Gets the function that constructs a default instance of the dictionary type.
    /// </summary>
    public Func<CollectionConstructionOptions<TKey>?, Func<TDictionary>>? DefaultConstructorFunc { get; init; }

    /// <summary>
    /// Gets the function that adds a key-value pair to the dictionary.
    /// </summary>
    public Setter<TDictionary, KeyValuePair<TKey, TValue>>? AddKeyValuePairFunc { get; init; }

    /// <summary>
    /// Gets the function that constructs a dictionary from an enumerable of key-value pairs.
    /// </summary>
    public Func<CollectionConstructionOptions<TKey>?, Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>>? EnumerableConstructorFunc { get; init; }

    /// <summary>
    /// Gets the function that constructs a dictionary from a span of key-value pairs.
    /// </summary>
    public Func<CollectionConstructionOptions<TKey>?, SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>>? SpanConstructorFunc { get; init; }

    /// <summary>
    /// Gets the shape of an associated type, by its name.
    /// </summary>
    public Func<string, ITypeShape?>? AssociatedTypeShapes { get; init; }

    /// <inheritdoc/>
    public override Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
        => GetDictionaryFunc;

    /// <inheritdoc/>
    public override Func<TDictionary> GetDefaultConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
            => DefaultConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Dictionary shape does not specify a default constructor.");

    /// <inheritdoc/>
    public override Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
            => AddKeyValuePairFunc ?? throw new InvalidOperationException("Dictionary shape does not specify an append delegate.");

    /// <inheritdoc/>
    public override Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> GetEnumerableConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
            => EnumerableConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Dictionary shape does not specify an enumerable constructor.");

    /// <inheritdoc/>
    public override SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> GetSpanConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
            => SpanConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Dictionary shape does not specify a span constructor.");

    /// <inheritdoc/>
    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, AssociatedTypeShapes, associatedType);
}