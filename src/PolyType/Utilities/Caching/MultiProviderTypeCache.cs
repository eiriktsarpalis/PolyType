using PolyType.Abstractions;
using System.Runtime.CompilerServices;

namespace PolyType.Utilities;

/// <summary>
/// Stores weakly referenced <see cref="TypeCache"/> instances keyed on <see cref="ITypeShapeProvider"/>.
/// </summary>
public sealed class MultiProviderTypeCache
{
    private readonly ConditionalWeakTable<ITypeShapeProvider, TypeCache> _providerCaches = new();
    private readonly ConditionalWeakTable<ITypeShapeProvider, TypeCache>.CreateValueCallback _createProviderCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiProviderTypeCache"/> class.
    /// </summary>
    public MultiProviderTypeCache()
    {
        _createProviderCache = provider => new(provider, this);
    }

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
    /// Gets a factory method governing delayed value initialization in case of recursive types.
    /// </summary>
    public IDelayedValueFactory? DelayedValueFactory { get; init; }

    /// <summary>
    /// Gets a value indicating whether exceptions should be cached.
    /// </summary>
    public bool CacheExceptions { get; init; }

    /// <summary>
    /// Gets or creates a cache scoped to the specified <paramref name="shapeProvider"/>.
    /// </summary>
    /// <param name="shapeProvider">The shape provider key.</param>
    /// <returns>A <see cref="TypeCache"/> scoped to <paramref name="shapeProvider"/>.</returns>
    public TypeCache GetScopedCache(ITypeShapeProvider shapeProvider)
    {
        Throw.IfNull(shapeProvider);
        return _providerCaches.GetValue(shapeProvider, _createProviderCache);
    }

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="typeShape"/>.
    /// </summary>
    /// <param name="typeShape">The type shape representing the key type.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd(ITypeShape typeShape)
    {
        Throw.IfNull(typeShape);
        TypeCache cache = GetScopedCache(typeShape.Provider);
        return cache.GetOrAdd(typeShape);
    }

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="provider"/>.
    /// </summary>
    /// <param name="type">The type representing the key type.</param>
    /// <param name="provider">The type shape provider used to resolve the type shape.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd(Type type, ITypeShapeProvider provider)
    {
        TypeCache cache = GetScopedCache(provider);
        return cache.GetOrAdd(type);
    }
}