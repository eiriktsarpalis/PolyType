using System.Collections;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET type that is a dictionary.
/// </summary>
/// <remarks>
/// Typically covers types implementing interfaces such as <see cref="IDictionary{TKey, TValue}"/>,
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="IDictionary"/>.
/// </remarks>
public abstract class IDictionaryTypeShape(ITypeShapeProvider provider) : ITypeShape
{
    /// <summary>
    /// Gets the shape of the underlying key type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns the shape for <see cref="object"/>.
    /// </remarks>
    public ITypeShape KeyType => KeyTypeNonGeneric;

    /// <summary>
    /// Gets the shape of the underlying value type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns the shape for <see cref="object"/>.
    /// </remarks>
    public ITypeShape ValueType => ValueTypeNonGeneric;

    /// <summary>
    /// Gets the construction strategy for the given collection.
    /// </summary>
    public abstract CollectionConstructionStrategy ConstructionStrategy { get; }

    /// <summary>
    /// Gets the kind of custom comparer (if any) that this collection may be initialized with.
    /// </summary>
    public abstract CollectionComparerOptions ComparerOptions { get; }

    /// <inheritdoc/>
    public abstract Type Type { get; }

    /// <inheritdoc/>
    public TypeShapeKind Kind => TypeShapeKind.Dictionary;

    /// <inheritdoc/>
    public ITypeShapeProvider Provider => provider;

    /// <inheritdoc/>
    public virtual ICustomAttributeProvider? AttributeProvider => Type;

    /// <inheritdoc cref="KeyType"/>
    protected abstract ITypeShape KeyTypeNonGeneric { get; }

    /// <inheritdoc cref="ValueType"/>
    protected abstract ITypeShape ValueTypeNonGeneric { get; }

    /// <inheritdoc/>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    public abstract ITypeShape? GetAssociatedTypeShape(Type associatedType);

    /// <inheritdoc/>
    public abstract object? Invoke(ITypeShapeFunc func, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a .NET type that is a dictionary.
/// </summary>
/// <typeparam name="TDictionary">The type of the underlying dictionary.</typeparam>
/// <typeparam name="TKey">The type of the underlying key.</typeparam>
/// <typeparam name="TValue">The type of the underlying value.</typeparam>
/// <remarks>
/// Typically covers types implementing interfaces such as <see cref="IDictionary{TKey, TValue}"/>,
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="IDictionary"/>.
/// </remarks>
public abstract class IDictionaryTypeShape<TDictionary, TKey, TValue>(ITypeShapeProvider provider) : IDictionaryTypeShape(provider), ITypeShape<TDictionary>
    where TKey : notnull
{
    /// <summary>
    /// Gets the shape of the underlying key type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns the shape for <see cref="object"/>.
    /// </remarks>
    public new virtual ITypeShape<TKey> KeyType => Provider.Resolve<TKey>();

    /// <summary>
    /// Gets the shape of the underlying value type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns the shape for <see cref="object"/>.
    /// </remarks>
    public new virtual ITypeShape<TValue> ValueType => Provider.Resolve<TValue>();

    /// <inheritdoc/>
    public override Type Type => typeof(TDictionary);

    /// <inheritdoc/>
    protected override ITypeShape KeyTypeNonGeneric => KeyType;

    /// <inheritdoc/>
    protected override ITypeShape ValueTypeNonGeneric => ValueType;

    /// <summary>
    /// Creates a delegate used for getting a <see cref="IReadOnlyDictionary{TKey, TValue}"/> view of the dictionary.
    /// </summary>
    /// <returns>
    /// A delegate accepting a <typeparamref name="TDictionary"/> and
    /// returning an <see cref="IReadOnlyDictionary{TKey, TValue}"/> view of the instance.
    /// </returns>
    public abstract Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary();

    /// <summary>
    /// Creates a delegate wrapping a parameterless constructor of a mutable collection.
    /// </summary>
    /// <param name="collectionConstructionOptions">Options that control how the collection is constructed. Use <see cref="IDictionaryTypeShape.ComparerOptions" /> to predict which properties may be worth initializing.</param>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A delegate wrapping a default constructor.</returns>
    public abstract Func<TDictionary> GetDefaultConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions = null);

    /// <summary>
    /// Creates a setter delegate used for appending a <see cref="KeyValuePair{TKey, TValue}"/> to a mutable dictionary.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A setter delegate used for appending entries to a mutable dictionary.</returns>
    public abstract Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair();

    /// <summary>
    /// Creates a constructor delegate for creating a collection from a span.
    /// </summary>
    /// <param name="collectionConstructionOptions">Options that control how the collection is constructed. Use <see cref="IDictionaryTypeShape.ComparerOptions" /> to predict which properties may be worth initializing.</param>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Span"/>.</exception>
    /// <returns>A delegate constructing a collection from a span of values.</returns>
    public abstract SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> GetSpanConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions = null);

    /// <summary>
    /// Creates a constructor delegate for creating a collection from an enumerable.
    /// </summary>
    /// <param name="collectionConstructionOptions">Optional options that control how the collection is constructed. Use <see cref="IDictionaryTypeShape.ComparerOptions" /> to predict which properties may be worth initializing.</param>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Enumerable"/>.</exception>
    /// <returns>A delegate constructing a collection from an enumerable of values.</returns>
    public abstract Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> GetEnumerableConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions = null);

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitDictionary(this, state);

    /// <inheritdoc/>
    public override object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
}
