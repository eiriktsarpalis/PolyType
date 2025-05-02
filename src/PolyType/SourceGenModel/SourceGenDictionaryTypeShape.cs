﻿using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for dictionary shapes.
/// </summary>
/// <typeparam name="TDictionary">The type of the dictionary.</typeparam>
/// <typeparam name="TKey">The type of the dictionary key.</typeparam>
/// <typeparam name="TValue">The type of the dictionary value.</typeparam>
public sealed class SourceGenDictionaryTypeShape<TDictionary, TKey, TValue> : SourceGenTypeShape<TDictionary>, IDictionaryTypeShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Gets the type shape of the dictionary key.
    /// </summary>
    public required ITypeShape<TKey> KeyType { get; init; }

    /// <summary>
    /// Gets the type shape of the dictionary value.
    /// </summary>
    public required ITypeShape<TValue> ValueType { get; init; }

    /// <summary>
    /// Gets the function that extracts a <see cref="IReadOnlyDictionary{TKey,TValue}"/> from an instance of the dictionary type.
    /// </summary>
    public required Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetDictionaryFunc { get; init; }

    /// <summary>
    /// Gets the construction strategy for the dictionary.
    /// </summary>
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }

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

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Dictionary;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitDictionary(this, state);

    ITypeShape IDictionaryTypeShape.KeyType => KeyType;
    ITypeShape IDictionaryTypeShape.ValueType => ValueType;

    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetGetDictionary()
        => GetDictionaryFunc;

    Func<TDictionary> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetDefaultConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
        => DefaultConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Dictionary shape does not specify a default constructor.");

    Setter<TDictionary, KeyValuePair<TKey, TValue>> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetAddKeyValuePair()
        => AddKeyValuePairFunc ?? throw new InvalidOperationException("Dictionary shape does not specify an append delegate.");

    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetEnumerableConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
        => EnumerableConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Dictionary shape does not specify an enumerable constructor.");

    SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetSpanConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
        => SpanConstructorFunc?.Invoke(collectionConstructionOptions) ?? throw new InvalidOperationException("Dictionary shape does not specify a span constructor.");
}