using PolyType.Abstractions;
using PolyType.ReflectionProvider;
using PolyType.Utilities;
using Xunit;

namespace PolyType.Tests;

public static class CacheTests
{
    [Fact]
    public static void TypeGenerationContext_DefaultValue()
    {
        TypeGenerationContext context = new();
        Assert.Null(context.ParentCache);
        Assert.Empty(context);

        Assert.Throws<InvalidOperationException>(() => context.TryCommitResults());
    }

    [Fact]
    public static void TypeGenerationContext_AddValue()
    {
        TypeGenerationContext context = new();
        Assert.DoesNotContain(typeof(int), context);

        context.Add(typeof(int), "42");

        Assert.Contains(typeof(int), context);
        Assert.Single(context);
        Assert.Equal("42", context[typeof(int)]);

        Assert.Throws<InvalidOperationException>(() => context.Add(typeof(int), "43"));

        Assert.Contains(typeof(int), context);
        Assert.Single(context);
        Assert.Equal("42", context[typeof(int)]);

        context.Add(typeof(int), "43", overwrite: true);

        Assert.Contains(typeof(int), context);
        Assert.Single(context);
        Assert.Equal("43", context[typeof(int)]);

        context.Clear();
        Assert.Empty(context);
    }

    [Fact]
    public static void TypeGenerationContext_TryGetValue()
    {
        ITypeShape<int> key = Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<int>();
        TypeGenerationContext context = new();

        Assert.DoesNotContain(typeof(int), context);

        Assert.False(context.TryGetValue(key, out object? value));
        Assert.Null(value);
        Assert.Empty(context);

        Assert.False(context.TryGetValue(key, out value));
        Assert.Null(value);
        Assert.Empty(context);

        context.Add(typeof(int), "42");

        Assert.True(context.TryGetValue(key, out value));
        Assert.Equal("42", value);
        Assert.Single(context);
    }

    [Fact]
    public static void TypeGenerationContext_TryGetValue_DelayedValueFactory()
    {
        ITypeShape<int> key = Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<int>();
        TestDelayedValueFactory factory = new();
        TypeGenerationContext context = new() { DelayedValueFactory = factory };

        // First TryGetValue call returns false and the delayed value factory is not invoked.
        Assert.False(context.TryGetValue(key, out object? result));
        Assert.Null(result);
        Assert.Empty(context);
        Assert.Equal(0, factory.State);

        // Second TryGetValue call returns true and the delayed value factory is invoked.
        Assert.True(context.TryGetValue(key, out result));
        Action<int> delayedValue = Assert.IsAssignableFrom<Action<int>>(result);
        Assert.Empty(context);
        Assert.Equal(1, factory.State);

        // Calling the delayed value throws an exception.
        Assert.Throws<InvalidOperationException>(() => delayedValue(42));
        Assert.Equal(2, factory.State);

        // Updating the entry makes the delayed value work.
        context.Add(typeof(int), new Action<int>(x => factory.State = x));
        delayedValue(42);
        Assert.Equal(42, factory.State);

        // The cache entry no longer returns the delayed value.
        Assert.True(context.TryGetValue(key, out result));
        Assert.NotNull(result);
        Action<int> completedValue = Assert.IsAssignableFrom<Action<int>>(result);
        Assert.NotSame(result, delayedValue);
        Assert.Equal(42, factory.State);

        // The new value works as expected.
        completedValue(43);
        Assert.Equal(43, factory.State);
    }

    private sealed class TestDelayedValueFactory : IDelayedValueFactory
    {
        public int State { get; set; }
        public DelayedValue Create<T>(ITypeShape<T> typeShape)
        {
            return new DelayedValue<Action<int>>(self =>
            {
                Assert.False(self.IsCompleted);
                Assert.Throws<InvalidOperationException>(() => self.Result);
                State = 1;
                return x =>
                {
                    State = 2;
                    self.Result(x);
                };
            });
        }
    }

    [Fact]
    public static void TypeCache_DefaultValue()
    {
        TypeCache cache = new(provider: Witness.GeneratedTypeShapeProvider);

        Assert.Same(Witness.GeneratedTypeShapeProvider, cache.Provider);
        Assert.Null(cache.ValueBuilderFactory);
        Assert.Null(cache.DelayedValueFactory);
        Assert.False(cache.CacheExceptions);
        Assert.Empty(cache);
    }

    [Fact]
    public static void TypeCache_AddValues()
    {
        TypeCache cache = new(provider: Witness.GeneratedTypeShapeProvider);
        TypeGenerationContext generationContext = cache.CreateGenerationContext();
        Assert.Empty(generationContext);
        Assert.Same(cache, generationContext.ParentCache);
        Assert.Null(generationContext.ValueBuilder);
        Assert.Null(generationContext.DelayedValueFactory);

        // Add first set of values.
        generationContext.Add(typeof(int), "42");
        generationContext.Add(typeof(string), "43");
        Assert.Equal(2, generationContext.Count);

        Assert.True(generationContext.TryCommitResults());

        Assert.Equal(2, cache.Count);
        Assert.Contains(typeof(int), cache);
        Assert.Contains(typeof(string), cache);

        // Add conflicting values.
        generationContext = cache.CreateGenerationContext();
        generationContext.Add(typeof(int), "44");
        generationContext.Add(typeof(bool), "45");

        Assert.False(generationContext.TryCommitResults());

        Assert.DoesNotContain(typeof(bool), cache);

        // Add conflicting values which are the same as the cached ones.
        generationContext = cache.CreateGenerationContext();
        generationContext.Add(typeof(int), "42");
        generationContext.Add(typeof(bool), "45");

        Assert.True(generationContext.TryCommitResults());
        Assert.Equal(3, cache.Count);
        Assert.Contains(typeof(bool), cache);

        // TryCommitResults is idempotent.
        Assert.True(generationContext.TryCommitResults());
        Assert.Equal(3, cache.Count);
        Assert.Contains(typeof(bool), cache);
    }

    [Fact]
    public static void TypeCache_DelayedValueFactory()
    {
        TypeCache cache = new(Witness.GeneratedTypeShapeProvider) { DelayedValueFactory = new TestDelayedValueFactory() };
        TypeGenerationContext generationContext = cache.CreateGenerationContext();
        Assert.Same(cache.DelayedValueFactory, generationContext.DelayedValueFactory);

        Assert.True(generationContext.TryCommitResults());
        Assert.Empty(cache);

        // Register an incomplete value in the cache.
        ITypeShape<int> key = Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<int>();
        Assert.False(generationContext.TryGetValue(key, out object? result));
        Assert.Null(result);
        Assert.Throws<InvalidOperationException>(() => generationContext.TryCommitResults());

        // Force the creation of a delayed value in the cache.
        Assert.True(generationContext.TryGetValue(key, out result));
        Assert.NotNull(result);
        Assert.Throws<InvalidOperationException>(() => generationContext.TryCommitResults());

        // Add the completed value to the cache.
        Action<int> finalValue = x => { };
        generationContext.Add(typeof(int), finalValue);
        Assert.True(generationContext.TryCommitResults());
        Assert.Single(cache);
        Assert.Contains(typeof(int), cache);
        Assert.Same(finalValue, cache[typeof(int)]);

        // TryCommitResults is idempotent.
        Assert.True(generationContext.TryCommitResults());
        Assert.Single(cache);
        Assert.Contains(typeof(int), cache);
        Assert.Same(finalValue, cache[typeof(int)]);
    }

    [Fact]
    public static void TypeCache_NullProvider_IsSupported()
    {
        var cache = new TypeCache(provider: null);
        Assert.Null(cache.Provider);

        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(typeof(int)));

        object value = new();
        Assert.True(cache.TryAdd(typeof(int), value));

        Assert.Same(value, cache[typeof(int)]);
        Assert.Same(value, cache.GetOrAdd(typeof(int)));
        Assert.False(cache.TryAdd(typeof(int), value));

        value = new();
        cache[typeof(int)] = value;

        Assert.Same(value, cache[typeof(int)]);
        Assert.Same(value, cache.GetOrAdd(typeof(int)));
        Assert.False(cache.TryAdd(typeof(int), value));
    }

    [Fact]
    public static void TypeGenerationContext_InvalidMethodParameters_ThrowsArgumentException()
    {
        TypeCache cache = new(Witness.GeneratedTypeShapeProvider) { ValueBuilderFactory = _ => new IdBuilderFactory() };
        TypeGenerationContext generationContext = cache.CreateGenerationContext();
        ITypeShape<int> shapeFromOtherProvider = ReflectionTypeShapeProvider.Default.GetTypeShapeOrThrow<int>();
        Assert.NotNull(generationContext.ValueBuilder);

        Assert.Throws<ArgumentNullException>(() => generationContext.TryGetValue(default(ITypeShape<int>)!, out _));
        Assert.Throws<ArgumentException>(() => generationContext.TryGetValue(shapeFromOtherProvider, out _));

        Assert.Throws<ArgumentNullException>(() => generationContext.GetOrAdd(default(ITypeShape<int>)!));
        Assert.Throws<ArgumentException>(() => generationContext.GetOrAdd(shapeFromOtherProvider));

        Assert.Throws<ArgumentNullException>(() => generationContext.Add(null!, "42"));
    }

    [Fact]
    public static void TypeGenerationContext_NoValueBuilder_GetOrAddThrowsInvalidOperationException()
    {
        TypeCache cache = new(Witness.GeneratedTypeShapeProvider);
        TypeGenerationContext generationContext = cache.CreateGenerationContext();
        ITypeShape<int> key = Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<int>();

        Assert.Throws<InvalidOperationException>(() => generationContext.GetOrAdd(key));
    }

    private class IdBuilderFactory : ITypeShapeFunc
    {
        public object? Invoke<T>(ITypeShape<T> typeShape, object? state = null) => typeShape;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TypeCache_CacheExceptions(bool cacheExceptions)
    {
        TypeCache cache = new(Witness.GeneratedTypeShapeProvider) 
        { 
            CacheExceptions = cacheExceptions,
            ValueBuilderFactory = _ => new ThrowingBuilder() 
        };

        ITypeShape<int> key = Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<int>();

        var ex1 = Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(key));
        var ex2 = Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(key));

        if (cacheExceptions)
        {
            Assert.Same(ex1, ex2);
        }
        else
        {
            Assert.NotSame(ex1, ex2);
        }
    }

    private sealed class ThrowingBuilder : ITypeShapeFunc
    {
        public object? Invoke<T>(ITypeShape<T> typeShape, object? state = null) => throw new NotFiniteNumberException();
    }

    [Fact]
    public static void MultiProviderTypeCache_ResolvesExpectedValues()
    {
        MultiProviderTypeCache cache = new();

        ITypeShape sourceGenShape = Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<int>();
        ITypeShape reflectionShape = ReflectionTypeShapeProvider.Default.GetTypeShapeOrThrow<int>();

        Assert.Same(cache.GetScopedCache(sourceGenShape), cache.GetScopedCache(sourceGenShape));
        Assert.Same(cache.GetScopedCache(reflectionShape), cache.GetScopedCache(reflectionShape));
        Assert.NotSame(cache.GetScopedCache(sourceGenShape), cache.GetScopedCache(reflectionShape));
    }

    [Fact]
    public static void MultiProviderTypeCache_TypeCacheInheritsConfiguration()
    {
        MultiProviderTypeCache cache = new()
        {
            CacheExceptions = true,
            DelayedValueFactory = new TestDelayedValueFactory(),
            ValueBuilderFactory = _ => new IdBuilderFactory(),
        };

        ITypeShape sourceGenShape = Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<int>();
        TypeCache sourceGenCache = cache.GetScopedCache(sourceGenShape);
        Assert.Same(Witness.GeneratedTypeShapeProvider, sourceGenCache.Provider);
        Assert.Equal(cache.CacheExceptions, sourceGenCache.CacheExceptions);
        Assert.Same(cache.DelayedValueFactory, sourceGenCache.DelayedValueFactory);
        Assert.Same(cache.ValueBuilderFactory, sourceGenCache.ValueBuilderFactory);
    }

    [Fact]
    public static void MultiProviderTypeCache_NullParameters_ThrowsArgumentNullException()
    {
        MultiProviderTypeCache cache = new();
        Assert.Throws<ArgumentNullException>(() => cache.GetScopedCache(null!));
        Assert.Throws<ArgumentNullException>(() => cache.GetOrAdd(null!));
    }
}