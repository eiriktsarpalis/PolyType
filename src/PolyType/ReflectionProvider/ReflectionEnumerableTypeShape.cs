using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionEnumerableTypeShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TEnumerable>(provider), IEnumerableTypeShape<TEnumerable, TElement>
{
    private readonly object _syncObject = new();
    private CollectionConstructionStrategy? _constructionStrategy;
    private (MethodBase Method, ConstructionSignature Signature)? _factory;
    private (MethodBase Method, ConstructionSignature Signature)? _factoryWithComparer;
    private MethodInfo? _addMethod;
    private bool _isListCtor;
    private bool _discoveryComplete;

    private Setter<TEnumerable, TElement>? _addDelegate;
    private MutableCollectionConstructor<TElement, TEnumerable>? _mutableCtorDelegate;
    private EnumerableCollectionConstructor<TElement, TElement, TEnumerable>? _enumerableCtorDelegate;
    private SpanConstructor<TElement, TElement, TEnumerable>? _spanCtorDelegate;

    public virtual SupportedCollectionConstructionOptions SupportedConstructionOptions
    {
        get
        {
            DetermineConstructionStrategy();
            return _factoryWithComparer is null ? SupportedCollectionConstructionOptions.None : ToComparerConstruction(_factoryWithComparer.Value.Signature);
        }
    }

    public virtual CollectionConstructionStrategy ConstructionStrategy
    {
        get
        {
            DetermineConstructionStrategy();
            return _constructionStrategy.Value;
        }
    }

    public virtual int Rank => 1;
    public virtual bool IsAsyncEnumerable => false;
    public abstract Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();

    public sealed override TypeShapeKind Kind => TypeShapeKind.Enumerable;
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnumerable(this, state);
    public ITypeShape<TElement> ElementType => Provider.GetShape<TElement>();
    ITypeShape IEnumerableTypeShape.ElementType => ElementType;

    public virtual Setter<TEnumerable, TElement> GetAddElement()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support mutation.");
        }

        DebugExt.Assert(_addMethod != null);
        if (_addDelegate is null)
        {
            lock (_syncObject)
            {
                return _addDelegate ??= Provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, TElement>(_addMethod);
            }
        }

        return _addDelegate;
    }

    public virtual MutableCollectionConstructor<TElement, TEnumerable> GetMutableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support default constructors.");
        }

        return _mutableCtorDelegate ?? CreateDefaultConstructor();

        MutableCollectionConstructor<TElement, TEnumerable> CreateDefaultConstructor()
        {
            DebugExt.Assert(_factory != null);
            Func<TEnumerable> ctor = CreateConstructorDelegate(_factory.Value.Method);
            MutableCollectionConstructor<TElement, TEnumerable> mutableCtorDelegate;
            switch ((_factory, _factoryWithComparer))
            {
                case ({ Signature: ConstructionSignature.None }, null):
                    mutableCtorDelegate = (in CollectionConstructionOptions<TElement>? options) => ctor();
                    break;
                case ({ Signature: ConstructionSignature.None }, { Signature: ConstructionSignature.EqualityComparer }):
                    {
                        Func<IEqualityComparer<TElement>, TEnumerable> comparerCtor = CreateConstructorDelegate<IEqualityComparer<TElement>>(_factoryWithComparer.Value.Method);
                        mutableCtorDelegate = (in CollectionConstructionOptions<TElement>? options) => options?.EqualityComparer is null ? ctor() : comparerCtor(options.Value.EqualityComparer);
                    }

                    break;
                case ({ Signature: ConstructionSignature.None }, { Signature: ConstructionSignature.Comparer }):
                    {
                        Func<IComparer<TElement>, TEnumerable> comparerCtor = CreateConstructorDelegate<IComparer<TElement>>(_factoryWithComparer.Value.Method);
                        mutableCtorDelegate = (in CollectionConstructionOptions<TElement>? options) => options?.Comparer is null ? ctor() : comparerCtor(options.Value.Comparer);
                    }

                    break;
                default: throw CreateUnsupportedConstructorException();
            }

            return Interlocked.CompareExchange(ref _mutableCtorDelegate, mutableCtorDelegate, null) ?? mutableCtorDelegate;
        }
    }

    public virtual EnumerableCollectionConstructor<TElement, TElement, TEnumerable> GetEnumerableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support enumerable constructors.");
        }

        return _enumerableCtorDelegate ?? CreateEnumerableConstructor();

        EnumerableCollectionConstructor<TElement, TElement, TEnumerable> CreateEnumerableConstructor()
        {
            EnumerableCollectionConstructor<TElement, TElement, TEnumerable> enumerableCtorDelegate;
            switch ((_factory, _factoryWithComparer))
            {
                case ({ Signature: ConstructionSignature.Values }, null):
                    {
                        var ctor = CreateConstructorDelegate<IEnumerable<TElement>>(_factory.Value.Method);
                        enumerableCtorDelegate = (IEnumerable<TElement> values, in CollectionConstructionOptions<TElement>? options) => ctor(values);
                    }

                    break;
                case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ComparerValues }):
                    {
                        var ctor = CreateConstructorDelegate<IEnumerable<TElement>>(_factory.Value.Method);
                        var comparerCtor = CreateConstructorDelegate<IComparer<TElement>, IEnumerable<TElement>>(_factoryWithComparer.Value.Method);
                        enumerableCtorDelegate = (IEnumerable<TElement> values, in CollectionConstructionOptions<TElement>? options) => options?.Comparer is null ? ctor(values) : comparerCtor(options.Value.Comparer, values);
                    }

                    break;
                case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.EqualityComparerValues }):
                    {
                        var ctor = CreateConstructorDelegate<IEnumerable<TElement>>(_factory.Value.Method);
                        var comparerCtor = CreateConstructorDelegate<IEqualityComparer<TElement>, IEnumerable<TElement>>(_factoryWithComparer.Value.Method);
                        enumerableCtorDelegate = (IEnumerable<TElement> values, in CollectionConstructionOptions<TElement>? options) => options?.EqualityComparer is null ? ctor(values) : comparerCtor(options.Value.EqualityComparer, values);
                    }

                    break;
                case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ValuesComparer }):
                    {
                        var ctor = CreateConstructorDelegate<IEnumerable<TElement>>(_factory.Value.Method);
                        var comparerCtor = CreateConstructorDelegate<IEnumerable<TElement>, IComparer<TElement>>(_factoryWithComparer.Value.Method);
                        enumerableCtorDelegate = (IEnumerable<TElement> values, in CollectionConstructionOptions<TElement>? options) => options?.Comparer is null ? ctor(values) : comparerCtor(values, options.Value.Comparer);
                    }

                    break;
                case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ValuesEqualityComparer }):
                    {
                        var ctor = CreateConstructorDelegate<IEnumerable<TElement>>(_factory.Value.Method);
                        var comparerCtor = CreateConstructorDelegate<IEnumerable<TElement>, IEqualityComparer<TElement>>(_factoryWithComparer.Value.Method);
                        enumerableCtorDelegate = (IEnumerable<TElement> values, in CollectionConstructionOptions<TElement>? options) => options?.EqualityComparer is null ? ctor(values) : comparerCtor(values, options.Value.EqualityComparer);
                    }

                    break;
                case (null, { Signature: ConstructionSignature.ValuesEqualityComparer }):
                    {
                        // When only a comparer overload is available, we assume it accepts null.
                        var comparerCtor = CreateConstructorDelegate<IEnumerable<TElement>, IEqualityComparer<TElement>?>(_factoryWithComparer.Value.Method);
                        enumerableCtorDelegate = (IEnumerable<TElement> values, in CollectionConstructionOptions<TElement>? options) => comparerCtor(values, options?.EqualityComparer);
                    }

                    break;
                default: throw CreateUnsupportedConstructorException();
            }

            return Interlocked.CompareExchange(ref _enumerableCtorDelegate, enumerableCtorDelegate, null) ?? enumerableCtorDelegate;
        }
    }

    public virtual SpanConstructor<TElement, TElement, TEnumerable> GetSpanConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        return _spanCtorDelegate ?? CreateSpanConstructor();

        SpanConstructor<TElement, TElement, TEnumerable> CreateSpanConstructor()
        {
            SpanConstructor<TElement, TElement, TEnumerable> spanCtorDelegate;
            if (_isListCtor)
            {
                switch ((_factory, _factoryWithComparer))
                {
                    case ({ Signature: ConstructionSignature.Values }, null):
                        {
                            var ctor = Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, TEnumerable>((ConstructorInfo)_factory.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => ctor(CollectionHelpers.CreateList(span));
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.EqualityComparerValues }):
                        {
                            var ctor = Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, TEnumerable>((ConstructorInfo)_factory.Value.Method);
                            var comparerCtor = Provider.MemberAccessor.CreateFuncDelegate<IEqualityComparer<TElement>, List<TElement>, TEnumerable>((ConstructorInfo)_factoryWithComparer.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => options?.EqualityComparer is null ? ctor(CollectionHelpers.CreateList(span)) : comparerCtor(options.Value.EqualityComparer, CollectionHelpers.CreateList(span));
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ComparerValues }):
                        {
                            var ctor = Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, TEnumerable>((ConstructorInfo)_factory.Value.Method);
                            var comparerCtor = Provider.MemberAccessor.CreateFuncDelegate<IComparer<TElement>, List<TElement>, TEnumerable>((ConstructorInfo)_factoryWithComparer.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => options?.Comparer is null ? ctor(CollectionHelpers.CreateList(span)) : comparerCtor(options.Value.Comparer, CollectionHelpers.CreateList(span));
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ValuesEqualityComparer }):
                        {
                            var ctor = Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, TEnumerable>((ConstructorInfo)_factory.Value.Method);
                            var comparerCtor = Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, IEqualityComparer<TElement>, TEnumerable>((ConstructorInfo)_factoryWithComparer.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => options?.EqualityComparer is null ? ctor(CollectionHelpers.CreateList(span)) : comparerCtor(CollectionHelpers.CreateList(span), options.Value.EqualityComparer);
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ValuesComparer }):
                        {
                            var ctor = Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, TEnumerable>((ConstructorInfo)_factory.Value.Method);
                            var comparerCtor = Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, IComparer<TElement>, TEnumerable>((ConstructorInfo)_factoryWithComparer.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => options?.Comparer is null ? ctor(CollectionHelpers.CreateList(span)) : comparerCtor(CollectionHelpers.CreateList(span), options.Value.Comparer);
                        }

                        break;
                    default: throw CreateUnsupportedConstructorException();
                }
            }
            else
            {
                switch ((_factory, _factoryWithComparer))
                {
                    case ({ Signature: ConstructionSignature.Values }, null):
                        {
                            var ctor = CreateSpanConstructorDelegate<TElement>(_factory.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => ctor(span);
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ValuesEqualityComparer }):
                        {
                            var ctor = CreateSpanConstructorDelegate<TElement>(_factory.Value.Method);
                            var comparerCtor = CreateSpanECConstructorDelegate<TElement, TElement>(_factoryWithComparer.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => options?.EqualityComparer is null ? ctor(span) : comparerCtor(span, options.Value.EqualityComparer);
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.EqualityComparerValues }):
                        {
                            var ctor = CreateSpanConstructorDelegate<TElement>(_factory.Value.Method);
                            var comparerCtor = CreateECSpanConstructorDelegate<TElement, TElement>(_factoryWithComparer.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => options?.EqualityComparer is null ? ctor(span) : comparerCtor(options.Value.EqualityComparer, span);
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ComparerValues }):
                        {
                            var ctor = CreateSpanConstructorDelegate<TElement>(_factory.Value.Method);
                            var comparerCtor = CreateCSpanConstructorDelegate<TElement, TElement>(_factoryWithComparer.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => options?.Comparer is null ? ctor(span) : comparerCtor(options.Value.Comparer, span);
                        }

                        break;
                    default: throw CreateUnsupportedConstructorException();
                }
            }

            return Interlocked.CompareExchange(ref _spanCtorDelegate, spanCtorDelegate, null) ?? spanCtorDelegate;
        }
    }

    protected override CollectionConstructorParameterType ClassifyConstructorParameter(ParameterInfo parameter)
    {
        if (parameter.ParameterType is { IsGenericType: true, GenericTypeArguments: [Type typeArg] } p && p.GetGenericTypeDefinition() == typeof(IComparer<>) && typeArg == ElementType.Type)
        {
            return CollectionConstructorParameterType.IComparerOfT;
        }
        else if (parameter.ParameterType is { IsGenericType: true, GenericTypeArguments: [Type typeArg2] } p2 && p2.GetGenericTypeDefinition() == typeof(IEqualityComparer<>) && typeArg2 == ElementType.Type)
        {
            return CollectionConstructorParameterType.IEqualityComparerOfT;
        }
        else if (parameter is { ParameterType: { IsGenericType: true, GenericTypeArguments: [Type typeArg3] } } && typeArg3 == ElementType.Type)
        {
            return CollectionConstructorParameterType.CollectionOfT;
        }

        return CollectionConstructorParameterType.Unrecognized;
    }

    [MemberNotNull(nameof(_constructionStrategy))]
    private void DetermineConstructionStrategy()
    {
        // There are many fields that are initialized by this method.
        // We actually initialize them sometimes multiple times along the way.
        // For thread-safety, we need to ensure that we do not recognize halfway initialized as fully initialized.
        // _discoveryComplete should be sufficient, but the compiler wants to see that we initialized the two fields
        // that we guarantee by attribute are initialized, so we check for null there too, though it's superfluous.
        if (!_discoveryComplete || _constructionStrategy is null)
        {
            lock (_syncObject)
            {
                if (!_discoveryComplete || _constructionStrategy is null)
                {
                    Helper();
                    _discoveryComplete = true;
                }
            }
        }

        [MemberNotNull(nameof(_constructionStrategy))]
        void Helper()
        {
            if (TryGetImmutableCollectionFactory())
            {
                return;
            }

            if (typeof(TEnumerable).GetConstructor([]) is ConstructorInfo defaultCtor)
            {
                MethodInfo? addMethod = null;
                foreach (MethodInfo methodInfo in typeof(TEnumerable).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (methodInfo.Name is "Add" or "Enqueue" or "Push" &&
                        methodInfo.GetParameters() is [ParameterInfo parameter] &&
                        parameter.ParameterType == typeof(TElement))
                    {
                        addMethod = methodInfo;
                        break;
                    }
                }

                if (!typeof(TEnumerable).IsValueType)
                {
                    // If no Add method was found, check for potential explicit interface implementations.
                    // Only do so if the type is not a value type, since this would force boxing otherwise.
                    if (addMethod is null && typeof(ICollection<TElement>).IsAssignableFrom(typeof(TEnumerable)))
                    {
                        addMethod = typeof(ICollection<TElement>).GetMethod(nameof(ICollection<TElement>.Add));
                    }

                    if (addMethod is null && typeof(IList).IsAssignableFrom(typeof(TEnumerable)) && typeof(TElement) == typeof(object))
                    {
                        addMethod = typeof(IList).GetMethod(nameof(IList.Add));
                    }
                }

                if (addMethod is not null)
                {
                    _factory = (defaultCtor, ConstructionSignature.None);
                    _addMethod = addMethod;
                    SetComparerConstructionOverload(CollectionConstructionStrategy.Mutable);
                    return;
                }
            }

            if (Provider.Options.UseReflectionEmit && typeof(TEnumerable).GetConstructor([typeof(ReadOnlySpan<TElement>)]) is ConstructorInfo spanCtor)
            {
                // Cannot invoke constructors with ROS parameters without Ref.Emit
                _factory = (spanCtor, ConstructionSignature.Values);
                SetComparerConstructionOverload(CollectionConstructionStrategy.Span);
                return;
            }

            if (typeof(TEnumerable).GetConstructor([typeof(IEnumerable<TElement>)]) is ConstructorInfo enumerableCtor)
            {
                _factory = (enumerableCtor, ConstructionSignature.Values);
                SetComparerConstructionOverload(CollectionConstructionStrategy.Enumerable);
                return;
            }

            if (typeof(TEnumerable).GetConstructors()
                .FirstOrDefault(ctor => ctor.GetParameters() is [{ ParameterType: { IsGenericType: true } paramTy }] && paramTy.IsAssignableFrom(typeof(List<TElement>)))
                is ConstructorInfo listCtor)
            {
                // Handle types accepting IList<T> or IReadOnlyList<T> such as ReadOnlyCollection<T>
                _isListCtor = true;
                _factory = (listCtor, ConstructionSignature.Values);
                SetComparerConstructionOverload(CollectionConstructionStrategy.Span);
                return;
            }

            if (typeof(TEnumerable).IsInterface)
            {
                if (typeof(TEnumerable).IsAssignableFrom(typeof(List<TElement>)))
                {
                    // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                    MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateList), BindingFlags.Public | BindingFlags.Static);
                    MethodInfo? factory = gm?.MakeGenericMethod(typeof(TElement));
                    if (factory is not null)
                    {
                        _factory = (factory, ConstructionSignature.Values);
                    }

                    SetComparerConstructionOverload(CollectionConstructionStrategy.Span);
                    return;
                }

                if (typeof(TEnumerable).IsAssignableFrom(typeof(HashSet<TElement>)))
                {
                    // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                    MethodInfo? gm = typeof(CollectionHelpers).GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == nameof(CollectionHelpers.CreateHashSet) && m.GetParameters().Length == 1);
                    MethodInfo? factory = gm?.MakeGenericMethod(typeof(TElement));
                    if (factory is not null)
                    {
                        _factory = (factory, ConstructionSignature.Values);
                    }

                    SetComparerConstructionOverload(CollectionConstructionStrategy.Span);
                    return;
                }

                if (typeof(TEnumerable).IsAssignableFrom(typeof(IList)))
                {
                    // Handle IList, ICollection and IEnumerable interfaces using List<object?>
                    MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateList), BindingFlags.Public | BindingFlags.Static);
                    MethodInfo? factory = gm?.MakeGenericMethod(typeof(object));
                    if (factory is not null)
                    {
                        _factory = (factory, ConstructionSignature.Values);
                    }

                    SetComparerConstructionOverload(CollectionConstructionStrategy.Span);
                    return;
                }
            }

            if (typeof(TEnumerable) is { Name: "FSharpList`1", Namespace: "Microsoft.FSharp.Collections" })
            {
                Type? module = typeof(TEnumerable).Assembly.GetType("Microsoft.FSharp.Collections.ListModule");
                MethodInfo? factory = module?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name is "OfSeq")
                    .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                    .Select(m => m.MakeGenericMethod(typeof(TElement)))
                    .FirstOrDefault();
                if (factory is not null)
                {
                    _factory = (factory, ConstructionSignature.Values);
                }

                SetComparerConstructionOverload(CollectionConstructionStrategy.Enumerable);
                return;
            }

            // move this later in priority order so it doesn't skip comparer options when they exist.
            if (typeof(TEnumerable).TryGetCollectionBuilderAttribute(typeof(TElement), out MethodInfo? builderMethod))
            {
                _factory = (builderMethod, ConstructionSignature.Values);
                SetComparerConstructionOverload(CollectionConstructionStrategy.Span);
                return;
            }

            _constructionStrategy = CollectionConstructionStrategy.None;

            [MemberNotNullWhen(true, nameof(_constructionStrategy))]
            bool TryGetImmutableCollectionFactory()
            {
                if (typeof(TEnumerable) is { Name: "ImmutableArray`1", Namespace: "System.Collections.Immutable" })
                {
                    return FindCreateRangeMethods("System.Collections.Immutable.ImmutableArray");
                }

                if (typeof(TEnumerable) is { Name: "ImmutableList`1", Namespace: "System.Collections.Immutable" })
                {
                    return FindCreateRangeMethods("System.Collections.Immutable.ImmutableList");
                }

                if (typeof(TEnumerable) is { Name: "ImmutableQueue`1", Namespace: "System.Collections.Immutable" })
                {
                    return FindCreateRangeMethods("System.Collections.Immutable.ImmutableQueue");
                }

                if (typeof(TEnumerable) is { Name: "ImmutableStack`1", Namespace: "System.Collections.Immutable" })
                {
                    return FindCreateRangeMethods("System.Collections.Immutable.ImmutableStack");
                }

                if (typeof(TEnumerable) is { Name: "ImmutableHashSet`1", Namespace: "System.Collections.Immutable" })
                {
                    return FindCreateRangeMethods("System.Collections.Immutable.ImmutableHashSet");
                }

                if (typeof(TEnumerable) is { Name: "ImmutableSortedSet`1", Namespace: "System.Collections.Immutable" })
                {
                    return FindCreateRangeMethods("System.Collections.Immutable.ImmutableSortedSet");
                }

                if (typeof(TEnumerable) is { Name: "FrozenSet`1", Namespace: "System.Collections.Frozen" })
                {
                    // FrozenSet only has overloads that take comparers.
                    Type? factoryType = typeof(TEnumerable).Assembly.GetType("System.Collections.Frozen.FrozenSet");
                    MethodInfo? factoryWithComparer = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => m.Name is "ToFrozenSet")
                        .Where(m =>
                            m.GetParameters() is [ParameterInfo p1, ParameterInfo p2] &&
                            p1.ParameterType.IsIEnumerable() &&
                            p2.ParameterType is { IsGenericType: true } p2Type && p2Type.GetGenericTypeDefinition() == typeof(IEqualityComparer<>))
                        .Select(m => m.MakeGenericMethod(typeof(TElement)))
                        .FirstOrDefault();

                    if (factoryWithComparer != null)
                    {
                        _factoryWithComparer = (factoryWithComparer, ConstructionSignature.ValuesEqualityComparer);
                        _constructionStrategy = CollectionConstructionStrategy.Enumerable;
                        return true;
                    }
                }

                return false;

                [MemberNotNullWhen(true, nameof(_constructionStrategy))]
                bool FindCreateRangeMethods(string typeName, bool? equalityComparer = null)
                {
                    Type? factoryType = typeof(TEnumerable).Assembly.GetType(typeName);

                    // First try for the Span-based factory methods.
                    MethodInfo? factory = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name is "Create" && m.GetParameters() is [{ ParameterType: { IsGenericType: true, Name: "ReadOnlySpan`1", Namespace: "System", GenericTypeArguments: [Type { IsGenericParameter: true }] } }])
                        ?.MakeGenericMethod(typeof(TElement));

                    if (factory is not null)
                    {
                        _factory = (factory, ConstructionSignature.Values);
                        SetComparerConstructionOverload(CollectionConstructionStrategy.Span);
                        return true;
                    }

                    factory = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name is "CreateRange" && m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                        ?.MakeGenericMethod(typeof(TElement));
                    if (factory is not null)
                    {
                        _factory = (factory, ConstructionSignature.Values);
                    }

                    SetComparerConstructionOverload(CollectionConstructionStrategy.Enumerable);
                    return _factory is not null;
                }
            }
        }
    }

    [MemberNotNull(nameof(_constructionStrategy))]
    private void SetComparerConstructionOverload(CollectionConstructionStrategy strategy)
    {
        if (_factory is null)
        {
            _constructionStrategy = CollectionConstructionStrategy.None;
            return;
        }

        _constructionStrategy = strategy;
        _factoryWithComparer = FindComparerConstructionOverload(_factory.Value.Method, _factory.Value.Signature);
    }

    private NotSupportedException CreateUnsupportedConstructorException() => new NotSupportedException($"({_factory?.Signature}, {_factoryWithComparer?.Signature}) constructor is not supported for this type.");

    private object? GetRelevantComparer(CollectionConstructionOptions<TElement> collectionConstructionOptions)
        => GetRelevantComparer<TElement>(collectionConstructionOptions, SupportedConstructionOptions);
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionEnumerableTypeOfTShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
    where TEnumerable : IEnumerable<TElement>
{
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable;
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionNonGenericEnumerableTypeShape<TEnumerable>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TEnumerable, object?>(provider)
    where TEnumerable : IEnumerable
{
    public override Func<TEnumerable, IEnumerable<object?>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<object?>();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionArrayTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TElement[], TElement>(provider)
{
    public override SupportedCollectionConstructionOptions SupportedConstructionOptions => SupportedCollectionConstructionOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<TElement[], IEnumerable<TElement>> GetGetEnumerable() => static array => array;
    public override SpanConstructor<TElement, TElement, TElement[]> GetSpanConstructor() => static (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MultiDimensionalArrayTypeShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider, int rank)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
    where TEnumerable : IEnumerable
{
    public override SupportedCollectionConstructionOptions SupportedConstructionOptions => SupportedCollectionConstructionOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.None;
    public override int Rank => rank;
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<TElement>();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReadOnlyMemoryTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<ReadOnlyMemory<TElement>, TElement>(provider)
{
    public override SupportedCollectionConstructionOptions SupportedConstructionOptions => SupportedCollectionConstructionOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<ReadOnlyMemory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable(memory);
    public override SpanConstructor<TElement, TElement, ReadOnlyMemory<TElement>> GetSpanConstructor() => static (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MemoryTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<Memory<TElement>, TElement>(provider)
{
    public override SupportedCollectionConstructionOptions SupportedConstructionOptions => SupportedCollectionConstructionOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<Memory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable((ReadOnlyMemory<TElement>)memory);
    public override SpanConstructor<TElement, TElement, Memory<TElement>> GetSpanConstructor() => static (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement>? options) => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionAsyncEnumerableShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
{
    public override SupportedCollectionConstructionOptions SupportedConstructionOptions => SupportedCollectionConstructionOptions.None;
    public override bool IsAsyncEnumerable => true;
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable() =>
        static _ => throw new InvalidOperationException("Sync enumeration of IAsyncEnumerable instances is not supported.");
}

internal delegate TDeclaringType SpanOnlyConstructor<TElement, TDeclaringType>(ReadOnlySpan<TElement> values);
internal delegate TDeclaringType SpanECConstructor<TKey, TElement, TDeclaringType>(ReadOnlySpan<TElement> values, IEqualityComparer<TKey> comparer);
internal delegate TDeclaringType SpanCConstructor<TKey, TElement, TDeclaringType>(ReadOnlySpan<TElement> values, IComparer<TKey> comparer);
internal delegate TDeclaringType ECSpanConstructor<TKey, TElement, TDeclaringType>(IEqualityComparer<TKey> comparer, ReadOnlySpan<TElement> values);
internal delegate TDeclaringType CSpanConstructor<TKey, TElement, TDeclaringType>(IComparer<TKey> comparer, ReadOnlySpan<TElement> values);
