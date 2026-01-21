using PolyType.Abstractions;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    private readonly ConditionalWeakTable<Type, Entry> _cache = new();
    private readonly List<WeakReference<Type>> _keys = new();
    private readonly object _lockObject = new();

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
    public int Count
    {
        get
        {
            lock (_lockObject)
            {
                return GetLiveEntriesCore().Count();
            }
        }
    }

    /// <summary>
    /// Determines whether the cache contains a value for the specified type.
    /// </summary>
    /// <param name="type">The key type.</param>
    /// <returns><see langword="true"/> is found, or <see langword="false"/> otherwise.</returns>
    public bool ContainsKey(Type type) => _cache.TryGetValue(type, out _);

    /// <summary>
    /// Gets or sets the value associated with the specified type.
    /// </summary>
    /// <param name="type">The type to look up.</param>
    /// <returns>The value associated with the specified key.</returns>
    public object? this[Type type]
    {
        get
        {
            if (_cache.TryGetValue(type, out Entry? entry))
            {
                return entry.GetValueOrException();
            }

            throw new KeyNotFoundException($"The given key '{type}' was not present in the cache.");
        }

        set
        {
            lock (_lockObject)
            {
#if NET
                if (!_cache.TryGetValue(type, out _))
                {
                    _keys.Add(new WeakReference<Type>(type));
                }

                _cache.AddOrUpdate(type, new Entry(value));
#else
                bool isNew = !_cache.TryGetValue(type, out _);
                _cache.Remove(type);
                _cache.Add(type, new Entry(value));
                if (isNew)
                {
                    _keys.Add(new WeakReference<Type>(type));
                }
#endif
            }
        }
    }

    /// <summary>
    /// Attempts to add the value associated with the specified type to the cache.
    /// </summary>
    /// <param name="type">The type associated with the value.</param>
    /// <param name="value">The value to attempt to add.</param>
    /// <returns><see langword="true"/> if the value was added successfully, <see langword="false"/> otherwise.</returns>
    public bool TryAdd(Type type, object? value) => TryAddEntry(type, new Entry(value));

    /// <summary>
    /// Attempts to get the value associated with the specified type.
    /// </summary>
    /// <param name="type">The type key whose value to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified type, if the type is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns><see langword="true"/> if the cache contains an element with the specified type; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(Type type, out object? value)
    {
        if (_cache.TryGetValue(type, out Entry? entry))
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

        if (_cache.TryGetValue(type, out Entry? entry))
        {
            return entry.GetValueOrThrowException();
        }

        if (Provider is null)
        {
            Throw();
            [DoesNotReturn]
            static void Throw() => throw new InvalidOperationException("The current cache does not specify a Provider property.");
        }

        return AddValue(Provider.GetTypeShapeOrThrow(type));
    }

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="typeShape"/>.
    /// </summary>
    /// <param name="typeShape">The type shape representing the key type.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd(ITypeShape typeShape)
    {
        Throw.IfNull(typeShape);

        if (_cache.TryGetValue(typeShape.Type, out Entry? entry))
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
                TryAddEntry(typeShape.Type, new Entry(ExceptionDispatchInfo.Capture(ex)));
                throw;
            }

            if (context.TryCommitResults())
            {
                return value;
            }

            if (_cache.TryGetValue(typeShape.Type, out Entry? entry))
            {
                return entry.GetValueOrThrowException();
            }
        }
    }

    internal object LockObject => _lockObject;

    internal void AddUnsynchronized(Type type, object? value)
    {
        Debug.Assert(Monitor.IsEntered(_lockObject), "Must be called within a lock.");
        if (!_cache.TryGetValue(type, out Entry? existingEntry))
        {
            _cache.Add(type, new Entry(value));
            _keys.Add(new WeakReference<Type>(type));
        }
        else
        {
            Debug.Assert(ReferenceEquals(existingEntry.Value, value), "should only be pre-populated with the same value.");
        }
    }

    private bool TryAddEntry(Type type, Entry entry)
    {
        lock (_lockObject)
        {
            if (_cache.TryGetValue(type, out _))
            {
                return false;
            }

            _cache.Add(type, entry);
            _keys.Add(new WeakReference<Type>(type));
            return true;
        }
    }

    internal void ValidateProvider(ITypeShapeProvider provider)
    {
        if (Provider is not null && !ReferenceEquals(Provider, provider))
        {
            throw new ArgumentException("The specified shape provider is not valid for this cache,", nameof(provider));
        }
    }

    private IEnumerable<KeyValuePair<Type, Entry>> GetLiveEntriesCore()
    {
        Debug.Assert(Monitor.IsEntered(_lockObject), "Must be called within a lock.");
        foreach (var weakRef in _keys)
        {
            if (weakRef.TryGetTarget(out Type? type) && _cache.TryGetValue(type, out Entry? entry))
            {
                yield return new KeyValuePair<Type, Entry>(type, entry);
            }
        }
    }

    private sealed class Entry
    {
        public Entry(object? value) => Value = value;
        public Entry(ExceptionDispatchInfo exception) => Exception = exception;

        public object? Value { get; }
        public ExceptionDispatchInfo? Exception { get; }

        public object? GetValueOrThrowException()
        {
            Exception?.Throw();
            return Value;
        }

        public object? GetValueOrException() => Exception is { } e ? e : Value;
    }

    IEnumerable<Type> IReadOnlyDictionary<Type, object?>.Keys
    {
        get
        {
            lock (_lockObject)
            {
                return GetLiveEntriesCore().Select(kvp => kvp.Key).ToList();
            }
        }
    }

    IEnumerable<object?> IReadOnlyDictionary<Type, object?>.Values
    {
        get
        {
            lock (_lockObject)
            {
                return GetLiveEntriesCore().Select(e => e.Value.GetValueOrException()).ToList();
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<Type, object?>>)this).GetEnumerator();

    IEnumerator<KeyValuePair<Type, object?>> IEnumerable<KeyValuePair<Type, object?>>.GetEnumerator()
    {
        lock (_lockObject)
        {
            return GetLiveEntriesCore().Select(kvp => new KeyValuePair<Type, object?>(kvp.Key, kvp.Value.GetValueOrException())).ToList().GetEnumerator();
        }
    }
}
