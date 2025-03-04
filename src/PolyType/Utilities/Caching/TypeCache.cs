﻿using PolyType.Abstractions;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace PolyType.Utilities;

/// <summary>
/// Defines a thread-safe cache that keys values on <see cref="ITypeShape"/> instances.
/// </summary>
/// <remarks>
/// Facilitates workflows common to generating values during type graph traversal,
/// including support delayed value creation in case of recursive types.
/// </remarks>
public sealed class TypeCache : IReadOnlyDictionary<Type, object?>
{
    private readonly ConcurrentDictionary<Type, Entry> _cache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeCache"/> class.
    /// </summary>
    /// <param name="provider">The shape provider associated with the current cache.</param>
    public TypeCache(ITypeShapeProvider? provider = null)
    {
        Provider = provider;
    }

    internal TypeCache(ITypeShapeProvider provider, MultiProviderTypeCache multiProviderCache)
    {
        Provider = provider;
        MultiProviderCache = multiProviderCache;
        ValueBuilderFactory = multiProviderCache.ValueBuilderFactory;
        DelayedValueFactory = multiProviderCache.DelayedValueFactory;
        CacheExceptions = multiProviderCache.CacheExceptions;
    }

    /// <summary>
    /// Gets the <see cref="ITypeShapeProvider"/> associated with the current cache.
    /// </summary>
    public ITypeShapeProvider? Provider { get; }

    /// <summary>
    /// Gets a factory method governing the creation of values when invoking the <see cref="GetOrAdd(ITypeShape)" /> method.
    /// </summary>
    /// <remarks>
    /// This factory takes a newly created <see cref="TypeGenerationContext"/> to construct an <see cref="ITypeShapeFunc"/>
    /// that is responsible for generating the value associated with a given type shape. The generation context wraps the
    /// created <see cref="ITypeShapeFunc"/> and can be used to recursively look up and cache values for nested types,
    /// including handling potentially cyclic type graphs.
    ///
    /// Because the generation context implements <see cref="ITypeShapeFunc"/>, this factory can effectively be seen as
    /// a Func&lt;<see cref="ITypeShapeFunc"/>, <see cref="ITypeShapeFunc"/>&gt; where the resultant function is being passed a reference to
    /// itself for the purpose of handling recursive calls. This makes it a specialized form of the Y-combinator.
    /// </remarks>
    public Func<TypeGenerationContext, ITypeShapeFunc>? ValueBuilderFactory { get; init; }

    /// <summary>
    /// Gets a factory method governing value initialization in case of recursive types.
    /// </summary>
    public IDelayedValueFactory? DelayedValueFactory { get; init; }

    /// <summary>
    /// Gets a value indicating whether exceptions should be cached.
    /// </summary>
    public bool CacheExceptions { get; init; }

    /// <summary>
    /// Gets the global cache to which this instance belongs.
    /// </summary>
    public MultiProviderTypeCache? MultiProviderCache { get; }

    /// <summary>
    /// Creates a new <see cref="TypeGenerationContext"/> instance for the cache.
    /// </summary>
    /// <returns>A new <see cref="TypeGenerationContext"/> instance for the cache.</returns>
    public TypeGenerationContext CreateGenerationContext() => new(this);

    /// <summary>
    /// Gets the total number of entries in the cache.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Determines whether the cache contains a value for the specified type.
    /// </summary>
    /// <param name="type">The key type.</param>
    /// <returns><see langword="true"/> is found, or <see langword="false"/> otherwise.</returns>
    public bool ContainsKey(Type type) => _cache.ContainsKey(type);

    /// <summary>
    /// Gets or sets the value associated with the specified type.
    /// </summary>
    /// <param name="type">The type to look up.</param>
    /// <returns>The value associated with the specified key.</returns>
    public object? this[Type type]
    {
        get => _cache[type].GetValueOrException();
        set
        {
            lock (LockObject)
            {
                _cache[type] = new Entry(value);
            }
        }
    }

    /// <summary>
    /// Attempts to add the value associated with the specified type to the cache.
    /// </summary>
    /// <param name="type">The type associated with the value.</param>
    /// <param name="value">The value to attempt to add.</param>
    /// <returns><see langword="true"/> if the value was added successfully, <see langword="false"/> otherwise.</returns>
    public bool TryAdd(Type type, object? value) => TryAdd(type, new Entry(value));

    /// <summary>
    /// Attempts to get the value associated with the specified type.
    /// </summary>
    /// <param name="type">The type key whose value to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified type, if the type is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns><see langword="true"/> if the cache contains an element with the specified type; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(Type type, out object? value)
    {
        if (_cache.TryGetValue(type, out Entry entry))
        {
            value = entry.GetValueOrThrowException();
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The key type.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd(Type type)
    {
        Throw.IfNull(type);

        if (_cache.TryGetValue(type, out Entry entry))
        {
            return entry.GetValueOrThrowException();
        }

        if (Provider is null)
        {
            Throw();
            [DoesNotReturn]
            static void Throw() => throw new InvalidOperationException("The current cache does not specify a Provider property.");
        }

        return AddValue(Provider.Resolve(type));
    }

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="typeShape"/>.
    /// </summary>
    /// <param name="typeShape">The type shape representing the key type.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd(ITypeShape typeShape)
    {
        Throw.IfNull(typeShape);

        if (_cache.TryGetValue(typeShape.Type, out Entry entry))
        {
            return entry.GetValueOrThrowException();
        }

        return AddValue(typeShape);
    }

    private object? AddValue(ITypeShape typeShape)
    {
        ValidateProvider(typeShape.Provider);

        // Uses optimistic concurrency when committing values to the cache.
        // If conflicting entries are found in the cache, the value is re-evaluated.
        while (true)
        {
            TypeGenerationContext context = CreateGenerationContext();
            object? value;
            try
            {
                value = typeShape.Invoke(context);
            }
            catch (Exception ex) when (CacheExceptions)
            {
                TryAdd(typeShape.Type, new Entry(ExceptionDispatchInfo.Capture(ex)));
                throw;
            }

            if (context.TryCommitResults())
            {
                return value;
            }

            if (_cache.TryGetValue(typeShape.Type, out Entry entry))
            {
                return entry.GetValueOrThrowException();
            }
        }
    }

    internal object LockObject => _cache;
    internal void AddUnsynchronized(Type type, object? value)
    {
        Debug.Assert(Monitor.IsEntered(LockObject), "Must be called within a lock.");
        bool result = _cache.TryAdd(type, new Entry(value));
        Debug.Assert(result || ReferenceEquals(_cache[type].Value, value), "should only be pre-populated with the same value.");
    }

    private bool TryAdd(Type type, Entry entry)
    {
        lock (LockObject)
        {
            return _cache.TryAdd(type, entry);
        }
    }

    internal void ValidateProvider(ITypeShapeProvider provider)
    {
        if (Provider is not null && !ReferenceEquals(Provider, provider))
        {
            throw new ArgumentException("The specified shape provider is not valid for this cache,", nameof(provider));
        }
    }

    private readonly struct Entry
    {
        public readonly object? Value;
        public readonly ExceptionDispatchInfo? Exception;
        public Entry(object? value) => Value = value;
        public Entry(ExceptionDispatchInfo exception) => Exception = exception;
        public object? GetValueOrThrowException()
        {
            Exception?.Throw();
            return Value;
        }

        public object? GetValueOrException() => Exception is { } e ? e : Value;
    }

    IEnumerable<Type> IReadOnlyDictionary<Type, object?>.Keys => _cache.Keys;
    IEnumerable<object?> IReadOnlyDictionary<Type, object?>.Values => _cache.Values.Select(e => e.GetValueOrException());
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<Type, object?>>)this).GetEnumerator();
    IEnumerator<KeyValuePair<Type, object?>> IEnumerable<KeyValuePair<Type, object?>>.GetEnumerator() =>
        _cache.Select(kvp => new KeyValuePair<Type, object?>(kvp.Key, kvp.Value.GetValueOrException())).GetEnumerator();
}
