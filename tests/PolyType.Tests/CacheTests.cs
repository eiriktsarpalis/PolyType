using PolyType.Abstractions;
using PolyType.ReflectionProvider;
using PolyType.SourceGenModel;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TypeGenerationContext_ContextualLookupsThrowWithoutRegisteringDelayedEntries(bool enableDelays)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        var factory = new TestDelayedValueFactory();
        var cache = new TypeCache(provider)
        {
            DelayedValueFactory = enableDelays ? factory : null,
            ValueBuilderFactory = _ => new DelegateBuilder((_, state) => state),
        };
        TypeGenerationContext context = cache.CreateGenerationContext();
        var shape = CreateContextualShape(provider);
        object state = new();

        Assert.Throws<InvalidOperationException>(() => context.TryGetValue(shape, out _));
        Assert.Throws<InvalidOperationException>(() => context.TryGetValue(shape, out _));
        Assert.Same(state, context.GetOrAdd(shape, state));
        Assert.Same(state, shape.Invoke(context, state));
        Assert.Same(state, ((ITypeShapeFunc)context).Invoke(shape, state));
        Assert.Equal(0, factory.CallCount);
        Assert.Empty(context);
        Assert.True(context.TryCommitResults());
        Assert.Empty(cache);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public static void TypeGenerationContext_ContextualEvaluationDoesNotCompleteOrdinaryEntry(int priorLookups, bool nullResult)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        ITypeShape<int> key = provider.GetTypeShapeOrThrow<int>();
        var factory = new TestDelayedValueFactory();
        object result = new();
        var context = new TypeGenerationContext
        {
            DelayedValueFactory = factory,
            ValueBuilder = new DelegateBuilder((_, _) => result),
        };
        object? pending = null;
        for (int i = 0; i < priorLookups; i++)
        {
            Assert.Equal(i > 0, context.TryGetValue(key, out pending));
        }

        var shape = CreateContextualShape(provider);
        Assert.Same(result, context.GetOrAdd(shape));
        Assert.Throws<InvalidOperationException>(() => context.TryGetValue(shape, out _));
        Assert.Empty(context);
        Assert.Equal(priorLookups == 2 ? 1 : 0, factory.CallCount);

        Action<int>? completed = nullResult ? null : value => factory.State = value + 1;
        context.Add(typeof(int), completed);
        Assert.Same(completed, context[typeof(int)]);
        Assert.Same(result, context.GetOrAdd(shape));
        Assert.Throws<InvalidOperationException>(() => context.TryGetValue(shape, out _));
        Assert.Same(completed, context.GetOrAdd(key));
        Assert.Single(context);
        if (pending is not null && !nullResult)
        {
            Assert.IsType<Action<int>>(pending)(42);
            Assert.Equal(43, factory.State);
        }
    }

    [Theory]
    [InlineData(CachedRoot.Missing)]
    [InlineData(CachedRoot.Value)]
    [InlineData(CachedRoot.Null)]
    [InlineData(CachedRoot.Exception)]
    public static void TypeGenerationContext_ContextualLookupRejectsAllParentEntryStates(CachedRoot entryState)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        ITypeShape<int> key = provider.GetTypeShapeOrThrow<int>();
        var cache = new TypeCache(provider)
        {
            CacheExceptions = true,
            ValueBuilderFactory = _ => new DelegateBuilder((_, _) => entryState switch
            {
                CachedRoot.Null => null,
                CachedRoot.Exception => throw new NotFiniteNumberException(),
                _ => new object(),
            }),
        };
        if (entryState is CachedRoot.Exception)
        {
            Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(key));
        }
        else if (entryState is not CachedRoot.Missing)
        {
            cache.GetOrAdd(key);
        }

        TypeGenerationContext context = cache.CreateGenerationContext();
        Assert.Throws<InvalidOperationException>(() => context.TryGetValue(CreateContextualShape(provider), out _));
        Assert.Empty(context);
        Assert.True(context.TryCommitResults());
        Assert.Equal(entryState is CachedRoot.Missing ? 0 : 1, cache.Count);
    }

    private sealed class TestDelayedValueFactory : IDelayedValueFactory
    {
        public int State { get; set; }
        public int CallCount { get; private set; }

        public DelayedValue Create<T>(ITypeShape<T> typeShape)
        {
            CallCount++;
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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void Caches_UnionAndUnderlyingShapeRemainDistinct(bool useReflection, bool underlyingFirst)
    {
        ITypeShapeProvider provider = useReflection ? ReflectionTypeShapeProvider.Default : Witness.GeneratedTypeShapeProvider;
        var unionShape = (IUnionTypeShape<PolymorphicClass>)provider.GetTypeShapeOrThrow<PolymorphicClass>();
        ITypeShape first = underlyingFirst ? unionShape.BaseType : unionShape;
        ITypeShape second = underlyingFirst ? unionShape : unionShape.BaseType;

        TypeCache cache = new(provider) { ValueBuilderFactory = _ => new IdBuilderFactory() };
        MultiProviderTypeCache multiProviderCache = new() { ValueBuilderFactory = _ => new IdBuilderFactory() };
        TypeGenerationContext context = new() { ValueBuilder = new IdBuilderFactory() };

        Assert.False(unionShape.IsContextual);
        Assert.True(unionShape.BaseType.IsContextual);
        Assert.Same(first, cache.GetOrAdd(first));
        Assert.Same(second, cache.GetOrAdd(second));
        Assert.Same(first, multiProviderCache.GetOrAdd(first));
        Assert.Same(second, multiProviderCache.GetOrAdd(second));
        Assert.Same(first, context.GetOrAdd(first, state: new object()));
        Assert.Same(second, context.GetOrAdd(second, state: new object()));
        Assert.Same(unionShape, cache[unionShape.Type]);
        Assert.Same(unionShape, multiProviderCache.GetScopedCache(unionShape)[unionShape.Type]);
        Assert.Same(unionShape, context[unionShape.Type]);
        Assert.Single(cache);
        Assert.Single(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TypeGenerationContext_DirectUnderlyingTraversalPreservesCachedUnion(bool useReflection)
    {
        ITypeShapeProvider provider = useReflection ? ReflectionTypeShapeProvider.Default : Witness.GeneratedTypeShapeProvider;
        var unionShape = (IUnionTypeShape<PolymorphicClass>)provider.GetTypeShapeOrThrow<PolymorphicClass>();
        TypeCache cache = new(provider) { ValueBuilderFactory = _ => new IdBuilderFactory() };
        TypeGenerationContext context = cache.CreateGenerationContext();

        Assert.Same(unionShape, context.GetOrAdd(unionShape));
        Assert.Same(unionShape.BaseType, unionShape.BaseType.Invoke(context.ValueBuilder!));
        Assert.Same(unionShape, context.GetOrAdd(unionShape));
        Assert.Single(context);
        Assert.True(context.TryCommitResults());
        Assert.Same(unionShape, cache.GetOrAdd(unionShape));
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void TypeCache_ContextualResultsBypassColdAndWarmCaches(bool warmCache, bool nullResult)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        ITypeShape<int> key = provider.GetTypeShapeOrThrow<int>();
        var shape = CreateContextualShape(provider);
        object? canonicalResult = nullResult ? null : new object();
        int invocations = 0;
        var cache = new TypeCache(provider)
        {
            ValueBuilderFactory = _ => new DelegateBuilder((typeShape, _) =>
            {
                if (!typeShape.IsContextual)
                {
                    return canonicalResult;
                }

                invocations++;
                return nullResult ? null : new object();
            }),
        };

        if (warmCache)
        {
            Assert.Same(canonicalResult, cache.GetOrAdd(key));
        }

        object? first = cache.GetOrAdd(shape);
        object? second = cache.GetOrAdd(shape);
        Assert.Equal(2, invocations);
        Assert.Equal(warmCache ? 1 : 0, cache.Count);
        if (nullResult)
        {
            Assert.Null(first);
            Assert.Null(second);
        }
        else
        {
            Assert.NotSame(first, second);
            Assert.NotSame(canonicalResult, first);
        }

        Assert.Same(canonicalResult, cache.GetOrAdd(typeof(int)));
        Assert.Same(canonicalResult, cache[typeof(int)]);
        Assert.Single(cache);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(10, false)]
    [InlineData(0, true)]
    [InlineData(10, true)]
    public static void TypeCache_ContextualBodySharesOrdinaryRecursionContext(int depth, bool contextualRoot)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        ITypeShape<int> key = provider.GetTypeShapeOrThrow<int>();
        var shape = CreateContextualShape(provider);
        var factory = new TestDelayedValueFactory();
        int invocations = 0;
        var cache = new TypeCache(provider)
        {
            DelayedValueFactory = factory,
            ValueBuilderFactory = context => new DelegateBuilder((typeShape, _) =>
            {
                if (!typeShape.IsContextual)
                {
                    var bodyAction = (Action<int>)context.GetOrAdd(shape)!;
                    return new Action<int>(remaining =>
                    {
                        if (remaining > 0)
                        {
                            bodyAction(remaining);
                        }
                    });
                }

                var recursiveAction = (Action<int>)context.GetOrAdd(key)!;
                return new Action<int>(remaining =>
                {
                    if (remaining > 0)
                    {
                        invocations++;
                        recursiveAction(remaining - 1);
                    }
                });
            }),
        };

        var action = Assert.IsType<Action<int>>(cache.GetOrAdd(contextualRoot ? shape : key));
        action(depth);
        Assert.Equal(depth, invocations);
        Assert.Equal(1, factory.CallCount);
        Assert.Equal(!contextualRoot, ReferenceEquals(action, cache[typeof(int)]));

        invocations = 0;
        Assert.IsType<Action<int>>(cache[typeof(int)])(depth);
        Assert.Equal(depth, invocations);
        Assert.Single(cache);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TypeCache_ContextualFailuresAreNotCached(bool cacheExceptions)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        ITypeShape<int> key = provider.GetTypeShapeOrThrow<int>();
        var shape = CreateContextualShape(provider);
        var cache = new TypeCache(provider)
        {
            CacheExceptions = cacheExceptions,
            ValueBuilderFactory = _ => new DelegateBuilder((typeShape, _) =>
                typeShape.IsContextual ? throw new NotFiniteNumberException() : typeShape),
        };

        var first = Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(shape));
        var second = Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(shape));
        Assert.NotSame(first, second);
        Assert.Empty(cache);

        Assert.Same(key, cache.GetOrAdd(key));
        Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(shape));
        Assert.Same(key, cache[typeof(int)]);
        Assert.Single(cache);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TypeCache_ContextualRequestsIgnoreOrdinaryFailures(bool cacheExceptions)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        ITypeShape<int> key = provider.GetTypeShapeOrThrow<int>();
        var shape = CreateContextualShape(provider);
        var cache = new TypeCache(provider)
        {
            CacheExceptions = cacheExceptions,
            ValueBuilderFactory = _ => new DelegateBuilder((typeShape, _) =>
                typeShape.IsContextual ? typeShape : throw new NotFiniteNumberException()),
        };

        var first = Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(key));
        Assert.Same(shape, cache.GetOrAdd(shape));
        var second = Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(key));
        Assert.Equal(cacheExceptions, ReferenceEquals(first, second));
        Assert.Equal(cacheExceptions ? 1 : 0, cache.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TypeCache_FailedContextualRootsDoNotCommitChildren(bool cacheExceptions)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        ITypeShape<string> childShape = provider.GetTypeShapeOrThrow<string>();
        var cache = new TypeCache(provider)
        {
            CacheExceptions = cacheExceptions,
            ValueBuilderFactory = context => new DelegateBuilder((shape, _) =>
            {
                if (!shape.IsContextual)
                {
                    return new object();
                }

                context.GetOrAdd(childShape);
                throw new NotFiniteNumberException();
            }),
        };

        Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(CreateContextualShape(provider)));
        Assert.Empty(cache);
    }

    [Theory]
    [InlineData(CachedRoot.Missing)]
    [InlineData(CachedRoot.Value)]
    [InlineData(CachedRoot.Null)]
    [InlineData(CachedRoot.Exception)]
    public static void TypeCache_ContextualConflictRetryDoesNotReturnOrdinaryRoot(CachedRoot rootState)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        ITypeShape<int> key = provider.GetTypeShapeOrThrow<int>();
        ITypeShape<string> childShape = provider.GetTypeShapeOrThrow<string>();
        object winningChild = new();
        object canonicalResult = new();
        var canonicalException = new NotFiniteNumberException();
        int attempts = 0;
        var cache = new TypeCache(provider)
        {
            CacheExceptions = true,
            ValueBuilderFactory = context => new DelegateBuilder((shape, _) =>
            {
                if (!shape.IsContextual)
                {
                    if (shape.Type == typeof(string))
                    {
                        return new object();
                    }

                    return rootState switch
                    {
                        CachedRoot.Null => null,
                        CachedRoot.Exception => throw canonicalException,
                        _ => canonicalResult,
                    };
                }

                attempts++;
                Assert.InRange(attempts, 1, 2);
                object? child = context.GetOrAdd(childShape);
                if (attempts == 1)
                {
                    TypeCache parent = context.ParentCache!;
                    Assert.True(parent.TryAdd(typeof(string), winningChild));
                    if (rootState is CachedRoot.Exception)
                    {
                        Assert.Same(canonicalException, Assert.Throws<NotFiniteNumberException>(() => parent.GetOrAdd(key)));
                    }
                    else if (rootState is not CachedRoot.Missing)
                    {
                        parent.GetOrAdd(key);
                    }
                }

                return new GenerationResult(child);
            }),
        };

        var result = Assert.IsType<GenerationResult>(cache.GetOrAdd(CreateContextualShape(provider)));
        Assert.Equal(2, attempts);
        Assert.Same(winningChild, result.Child);
        Assert.Same(winningChild, cache[typeof(string)]);
        Assert.Equal(rootState is CachedRoot.Missing ? 1 : 2, cache.Count);
        if (rootState is CachedRoot.Exception)
        {
            Assert.Same(canonicalException, Assert.Throws<NotFiniteNumberException>(() => cache.GetOrAdd(typeof(int))));
        }
        else if (rootState is not CachedRoot.Missing)
        {
            Assert.Same(rootState is CachedRoot.Null ? null : canonicalResult, cache.GetOrAdd(typeof(int)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TypeCache_ContextualRequestsStillValidateProvider(bool warmCache)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        var cache = new TypeCache(provider)
        {
            ValueBuilderFactory = _ => new IdBuilderFactory(),
        };
        if (warmCache)
        {
            cache.GetOrAdd(provider.GetTypeShapeOrThrow<int>());
        }

        var shape = CreateContextualShape(ReflectionTypeShapeProvider.Default);
        TypeGenerationContext context = cache.CreateGenerationContext();
        Assert.Throws<ArgumentException>(() => cache.GetOrAdd(shape));
        Assert.Throws<ArgumentException>(() => context.GetOrAdd(shape));
        Assert.Throws<ArgumentException>(() => context.TryGetValue(shape, out _));
        Assert.Empty(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TypeCache_OrdinaryShapesNeedNotBeInternedOrExported(bool exportShapes)
    {
        var provider = new UninternedProvider(exportShapes);
        var first = new SourceGenObjectTypeShape<int> { Provider = provider };
        var second = new SourceGenObjectTypeShape<int> { Provider = provider };
        var cache = new TypeCache(provider)
        {
            ValueBuilderFactory = _ => new IdBuilderFactory(),
        };

        Assert.False(first.IsContextual);
        Assert.Same(first, cache.GetOrAdd(first));
        Assert.Same(first, cache.GetOrAdd(second));
        Assert.Same(first, cache.GetOrAdd(typeof(int)));
        Assert.Single(cache);
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

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    public static async Task MultiProviderTypeCache_ConcurrentContextualRootsOnlyCacheOrdinaryChildren(int count)
    {
        var provider = Witness.GeneratedTypeShapeProvider;
        ITypeShape<string> childShape = provider.GetTypeShapeOrThrow<string>();
        var shape = CreateContextualShape(provider);
        var cache = new MultiProviderTypeCache
        {
            ValueBuilderFactory = context => new DelegateBuilder((typeShape, _) =>
                typeShape.IsContextual ? new GenerationResult(context.GetOrAdd(childShape)) : new object()),
        };

        object?[] results = await Task.WhenAll(Enumerable.Range(0, count).Select(_ => Task.Run(() => cache.GetOrAdd(shape))));
        TypeCache scopedCache = cache.GetScopedCache(shape);
        Assert.Single(scopedCache);
        Assert.False(scopedCache.ContainsKey(typeof(int)));
        object? child = scopedCache[typeof(string)];
        Assert.All(results, result => Assert.Same(child, Assert.IsType<GenerationResult>(result).Child));
        Assert.Equal(count, results.Distinct().Count());
    }

    [Fact]
    public static void MultiProviderTypeCache_NullParameters_ThrowsArgumentNullException()
    {
        MultiProviderTypeCache cache = new();
        Assert.Throws<ArgumentNullException>(() => cache.GetScopedCache(null!));
        Assert.Throws<ArgumentNullException>(() => cache.GetOrAdd(null!));
    }

    private static ITypeShape<int> CreateContextualShape(ITypeShapeProvider provider) =>
        new SourceGenObjectTypeShape<int> { Provider = provider, IsContextual = true };

    private sealed class DelegateBuilder(Func<ITypeShape, object?, object?> invoke) : ITypeShapeFunc
    {
        public object? Invoke<T>(ITypeShape<T> typeShape, object? state = null) => invoke(typeShape, state);
    }

    private sealed class GenerationResult(object? child)
    {
        public object? Child { get; } = child;
    }

    private sealed class UninternedProvider(bool exportShapes) : ITypeShapeProvider
    {
        public ITypeShape? GetTypeShape(Type type) =>
            exportShapes && type == typeof(int) ? new SourceGenObjectTypeShape<int> { Provider = this } : null;
    }

    public enum CachedRoot
    {
        Missing,
        Value,
        Null,
        Exception,
    }
}