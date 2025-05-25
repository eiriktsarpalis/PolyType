using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionEnumerableTypeShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TEnumerable>(provider), IEnumerableTypeShape<TEnumerable, TElement>
{
    private readonly object _syncObject = new();
    private CollectionConstructionStrategy? _constructionStrategy;
    private ConstructionWithComparer? _constructionComparer;
    private ConstructorInfo? _defaultCtor;
    private ConstructorInfo? _defaultCtorWithComparer;
    private MethodInfo? _addMethod;
    private MethodBase? _enumerableCtor;
    private MethodBase? _enumerableCtorWithComparer;
    private MethodBase? _spanCtor;
    private MethodBase? _spanCtorWithComparer;
    private ConstructorInfo? _listCtor;
    private ConstructorInfo? _listCtorWithComparer;
    private bool _isFrozenSet;
    private bool _discoveryComplete;

    private Setter<TEnumerable, TElement>? _addDelegate;
    private Func<TEnumerable>? _defaultCtorDelegate;
    private Func<IEnumerable<TElement>, TEnumerable>? _enumerableCtorDelegate;
    private SpanConstructor<TElement, TEnumerable>? _spanCtorDelegate;

    private Func<List<TElement>, IEqualityComparer<TElement>, TEnumerable>? _listCtorValuesEqualityComparerDelegate;
    private Func<List<TElement>, IComparer<TElement>, TEnumerable>? _listCtorValuesComparerDelegate;
    private Func<IEqualityComparer<TElement>, List<TElement>, TEnumerable>? _listCtorEqualityComparerValuesDelegate;
    private Func<IComparer<TElement>, List<TElement>, TEnumerable>? _listCtorComparerValuesDelegate;
    private Func<IEqualityComparer<TElement>, TEnumerable>? _defaultCtorWithEqualityComparerDelegate;
    private Func<IComparer<TElement>, TEnumerable>? _defaultCtorWithComparerDelegate;
    private Func<object, SpanConstructor<TElement, TEnumerable>>? _spanCtorDelegateFromComparerDelegate;

    public virtual CollectionComparerOptions ComparerOptions
    {
        get
        {
            DetermineConstructionStrategy();
            return ToComparerConstruction(_constructionComparer.Value);
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
        return _addDelegate ??= Provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, TElement>(_addMethod);
    }

    public virtual Func<TEnumerable> GetDefaultConstructor(CollectionConstructionOptions<TElement>? collectionConstructionOptions = null)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support default constructors.");
        }

        // We'll use a shared delegate when no comparer applies.
        object? relevantComparer = GetRelevantComparer(collectionConstructionOptions);
        if (relevantComparer is null || _defaultCtorWithComparer is null)
        {
            return _defaultCtorDelegate ?? CreateDefaultConstructor();

            Func<TEnumerable> CreateDefaultConstructor()
            {
                lock (_syncObject)
                {
                    if (_defaultCtorDelegate is { } defaultCtorDelegate)
                    {
                        return defaultCtorDelegate;
                    }

                    Debug.Assert(_defaultCtor != null);
                    return _defaultCtorDelegate = Provider.MemberAccessor.CreateDefaultConstructor<TEnumerable>(new MethodConstructorShapeInfo(typeof(TEnumerable), _defaultCtor, parameters: []));
                }
            }
        }
        else
        {
            switch (relevantComparer)
            {
                case IEqualityComparer<TElement> ec:
                    _defaultCtorWithEqualityComparerDelegate ??= Provider.MemberAccessor.CreateFuncDelegate<IEqualityComparer<TElement>, TEnumerable>(_defaultCtorWithComparer);
                    return () => _defaultCtorWithEqualityComparerDelegate(ec);
                case IComparer<TElement> c:
                    _defaultCtorWithComparerDelegate ??= Provider.MemberAccessor.CreateFuncDelegate<IComparer<TElement>, TEnumerable>(_defaultCtorWithComparer);
                    return () => _defaultCtorWithComparerDelegate(c);
                default:
                    throw new NotSupportedException();
            }
        }
    }

    public virtual Func<IEnumerable<TElement>, TEnumerable> GetEnumerableConstructor(CollectionConstructionOptions<TElement>? collectionConstructionOptions = null)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support enumerable constructors.");
        }

        // We'll use a shared delegate when no comparer applies.
        object? relevantComparer = GetRelevantComparer(collectionConstructionOptions);
        if (relevantComparer is null || _enumerableCtorWithComparer is null)
        {
            DebugExt.Assert(_enumerableCtor != null);
            return _enumerableCtorDelegate ?? CreateEnumerableConstructor();

            Func<IEnumerable<TElement>, TEnumerable> CreateEnumerableConstructor()
            {
                lock (_syncObject)
                {
                    if (_enumerableCtorDelegate is { } enumerableCtorDelegate)
                    {
                        return enumerableCtorDelegate;
                    }

                    return _enumerableCtorDelegate = _enumerableCtor switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEnumerable<TElement>, TEnumerable>(ctorInfo),
                        MethodInfo enumerableFactory when _isFrozenSet => CreateFrozenSetDelegate(enumerableFactory),
                        _ => ((MethodInfo)_enumerableCtor).CreateDelegate<Func<IEnumerable<TElement>, TEnumerable>>(),
                    };

                    static Func<IEnumerable<TElement>, TEnumerable> CreateFrozenSetDelegate(MethodInfo enumerableFactory)
                    {
                        // FrozenSet only exposes one factory overload accepting IEqualityComparer
                        var factoryDelegate = enumerableFactory.CreateDelegate<Func<IEnumerable<TElement>, IEqualityComparer<TElement>?, TEnumerable>>();
                        return values => factoryDelegate(values, null);
                    }
                }
            }
        }
        else
        {
            switch (_constructionComparer)
            {
                case ConstructionWithComparer.ValuesEqualityComparer:
                    Func<IEnumerable<TElement>, IEqualityComparer<TElement>, TEnumerable> del = _enumerableCtorWithComparer switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEnumerable<TElement>, IEqualityComparer<TElement>, TEnumerable>(ctorInfo),
                        MethodInfo methodInfo => methodInfo.CreateDelegate<Func<IEnumerable<TElement>, IEqualityComparer<TElement>, TEnumerable>>(),
                        _ => throw new NotSupportedException(),
                    };

                    return values => del(values, (IEqualityComparer<TElement>)relevantComparer);
                case ConstructionWithComparer.EqualityComparerValues:
                    Func<IEqualityComparer<TElement>, IEnumerable<TElement>, TEnumerable> del2 = _enumerableCtorWithComparer switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEqualityComparer<TElement>, IEnumerable<TElement>, TEnumerable>(ctorInfo),
                        MethodInfo methodInfo => methodInfo.CreateDelegate<Func<IEqualityComparer<TElement>, IEnumerable<TElement>, TEnumerable>>(),
                        _ => throw new NotSupportedException(),
                    };

                    return values => del2((IEqualityComparer<TElement>)relevantComparer, values);
                case ConstructionWithComparer.ValuesComparer:
                    Func<IEnumerable<TElement>, IComparer<TElement>, TEnumerable> del3 = _enumerableCtorWithComparer switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEnumerable<TElement>, IComparer<TElement>, TEnumerable>(ctorInfo),
                        MethodInfo methodInfo => methodInfo.CreateDelegate<Func<IEnumerable<TElement>, IComparer<TElement>, TEnumerable>>(),
                        _ => throw new NotSupportedException(),
                    };

                    return values => del3(values, (IComparer<TElement>)relevantComparer);
                case ConstructionWithComparer.ComparerValues:
                    Func<IComparer<TElement>, IEnumerable<TElement>, TEnumerable> del4 = _enumerableCtorWithComparer switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IComparer<TElement>, IEnumerable<TElement>, TEnumerable>(ctorInfo),
                        MethodInfo methodInfo => methodInfo.CreateDelegate<Func<IComparer<TElement>, IEnumerable<TElement>, TEnumerable>>(),
                        _ => throw new NotSupportedException(),
                    };

                    return values => del4((IComparer<TElement>)relevantComparer, values);
                default:
                    throw new NotSupportedException();
            }
        }
    }

    public virtual SpanConstructor<TElement, TEnumerable> GetSpanConstructor(CollectionConstructionOptions<TElement>? collectionConstructionOptions = null)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        // We'll use a shared delegate when no comparer applies.
        object? relevantComparer = GetRelevantComparer(collectionConstructionOptions);
        if (relevantComparer is null || (_spanCtorWithComparer is null && _listCtorWithComparer is null))
        {
            return _spanCtorDelegate ?? CreateSpanConstructor();

            SpanConstructor<TElement, TEnumerable> CreateSpanConstructor()
            {
                lock (_syncObject)
                {
                    if (_spanCtorDelegate is { } spanCtorDelegate)
                    {
                        return spanCtorDelegate;
                    }

                    if (_listCtor is ConstructorInfo listCtor)
                    {
                        var listCtorDelegate = Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, TEnumerable>(listCtor);
                        return _spanCtorDelegate = span => listCtorDelegate(CollectionHelpers.CreateList(span));
                    }

                    return _spanCtorDelegate = _spanCtor switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateSpanConstructorDelegate<TElement, TEnumerable>(ctorInfo),
                        MethodInfo methodInfo => methodInfo.CreateDelegate<SpanConstructor<TElement, TEnumerable>>(),
                        _ => throw new NotSupportedException($"_spanCtor is {_spanCtor}."),
                    };
                }
            }
        }
        else
        {
            if (_listCtorWithComparer is ConstructorInfo listCtorWithComparer)
            {
                switch (_constructionComparer)
                {
                    case ConstructionWithComparer.ValuesEqualityComparer:
                        _listCtorValuesEqualityComparerDelegate ??= Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, IEqualityComparer<TElement>, TEnumerable>(listCtorWithComparer);
                        return span => _listCtorValuesEqualityComparerDelegate(CollectionHelpers.CreateList(span), (IEqualityComparer<TElement>)relevantComparer);
                    case ConstructionWithComparer.ValuesComparer:
                        _listCtorValuesComparerDelegate ??= Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, IComparer<TElement>, TEnumerable>(listCtorWithComparer);
                        return span => _listCtorValuesComparerDelegate(CollectionHelpers.CreateList(span), (IComparer<TElement>)relevantComparer);
                    case ConstructionWithComparer.EqualityComparerValues:
                        _listCtorEqualityComparerValuesDelegate ??= Provider.MemberAccessor.CreateFuncDelegate<IEqualityComparer<TElement>, List<TElement>, TEnumerable>(listCtorWithComparer);
                        return span => _listCtorEqualityComparerValuesDelegate((IEqualityComparer<TElement>)relevantComparer, CollectionHelpers.CreateList(span));
                    case ConstructionWithComparer.ComparerValues:
                        _listCtorComparerValuesDelegate ??= Provider.MemberAccessor.CreateFuncDelegate<IComparer<TElement>, List<TElement>, TEnumerable>(listCtorWithComparer);
                        return span => _listCtorComparerValuesDelegate((IComparer<TElement>)relevantComparer, CollectionHelpers.CreateList(span));
                    default: throw new NotSupportedException();
                }
            }

            DebugExt.Assert(_constructionComparer is not null);
            _spanCtorDelegateFromComparerDelegate ??= _spanCtorWithComparer switch
            {
                ConstructorInfo ctorInfoWithComparer => Provider.MemberAccessor.CreateSpanConstructorDelegate<TElement, TElement, TEnumerable>(ctorInfoWithComparer, _constructionComparer.Value),
                MethodInfo ctorInfoWithComparer => CreateSpanMethodDelegate<TElement, TElement, TEnumerable>(ctorInfoWithComparer, _constructionComparer.Value),
                _ => throw new NotSupportedException(),
            };

            return _spanCtorDelegateFromComparerDelegate(relevantComparer);
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

    [MemberNotNull(nameof(_constructionComparer), nameof(_constructionStrategy))]
    private void DetermineConstructionStrategy()
    {
        // There are many fields that are initialized by this method.
        // We actually initialize them sometimes multiple times along the way.
        // For thread-safety, we need to ensure that we do not recognize halfway initialized as fully initialized.
        // _discoveryComplete should be sufficient, but the compiler wants to see that we initialized the two fields
        // that we guarantee by attribute are initialized, so we check for null there too, though it's superfluous.
        if (!_discoveryComplete || _constructionComparer is null || _constructionStrategy is null)
        {
            lock (_syncObject)
            {
                if (!_discoveryComplete || _constructionComparer is null || _constructionStrategy is null)
                {
                    Helper();
                    _discoveryComplete = true;
                }
            }
        }

        [MemberNotNull(nameof(_constructionComparer), nameof(_constructionStrategy))]
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
                    _defaultCtor = defaultCtor;
                    _addMethod = addMethod;
                    (_constructionComparer, _defaultCtorWithComparer) = FindComparerConstructorOverload(defaultCtor);
                    _constructionStrategy = CollectionConstructionStrategy.Mutable;
                    return;
                }
            }

            if (Provider.Options.UseReflectionEmit && typeof(TEnumerable).GetConstructor([typeof(ReadOnlySpan<TElement>)]) is ConstructorInfo spanCtor)
            {
                // Cannot invoke constructors with ROS parameters without Ref.Emit
                _spanCtor = spanCtor;
                _constructionStrategy = CollectionConstructionStrategy.Span;
                (_constructionComparer, _spanCtorWithComparer) = FindComparerConstructorOverload(spanCtor);
                return;
            }

            if (typeof(TEnumerable).GetConstructor([typeof(IEnumerable<TElement>)]) is ConstructorInfo enumerableCtor)
            {
                _enumerableCtor = enumerableCtor;
                _constructionStrategy = CollectionConstructionStrategy.Enumerable;
                (_constructionComparer, _enumerableCtorWithComparer) = FindComparerConstructorOverload(enumerableCtor);
                return;
            }

            if (typeof(TEnumerable).GetConstructors()
                .FirstOrDefault(ctor => ctor.GetParameters() is [{ ParameterType: { IsGenericType: true } paramTy }] && paramTy.IsAssignableFrom(typeof(List<TElement>)))
                is ConstructorInfo listCtor)
            {
                // Handle types accepting IList<T> or IReadOnlyList<T> such as ReadOnlyCollection<T>
                _listCtor = listCtor;
                _constructionStrategy = CollectionConstructionStrategy.Span;
                (_constructionComparer, _listCtorWithComparer) = FindComparerConstructorOverload(listCtor);
                return;
            }

            if (typeof(TEnumerable).IsInterface)
            {
                if (typeof(TEnumerable).IsAssignableFrom(typeof(List<TElement>)))
                {
                    // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                    MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateList), BindingFlags.Public | BindingFlags.Static);
                    _spanCtor = gm?.MakeGenericMethod(typeof(TElement));
                    _constructionStrategy = _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
                    (_constructionComparer, _spanCtorWithComparer) = FindComparerConstructionOverload(_spanCtor);
                    return;
                }

                if (typeof(TEnumerable).IsAssignableFrom(typeof(HashSet<TElement>)))
                {
                    // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                    MethodInfo? gm = typeof(CollectionHelpers).GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == nameof(CollectionHelpers.CreateHashSet) && m.GetParameters().Length == 1);
                    _spanCtor = gm?.MakeGenericMethod(typeof(TElement));
                    _constructionStrategy = _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
                    (_constructionComparer, _spanCtorWithComparer) = FindComparerConstructionOverload(_spanCtor);
                    return;
                }

                if (typeof(TEnumerable).IsAssignableFrom(typeof(IList)))
                {
                    // Handle IList, ICollection and IEnumerable interfaces using List<object?>
                    MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateList), BindingFlags.Public | BindingFlags.Static);
                    _spanCtor = gm?.MakeGenericMethod(typeof(object));
                    _constructionStrategy = _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
                    (_constructionComparer, _spanCtorWithComparer) = FindComparerConstructionOverload(_spanCtor);
                    return;
                }
            }

            if (typeof(TEnumerable) is { Name: "FSharpList`1", Namespace: "Microsoft.FSharp.Collections" })
            {
                Type? module = typeof(TEnumerable).Assembly.GetType("Microsoft.FSharp.Collections.ListModule");
                _enumerableCtor = module?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name is "OfSeq")
                    .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                    .Select(m => m.MakeGenericMethod(typeof(TElement)))
                    .FirstOrDefault();

                _constructionStrategy = _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
                (_constructionComparer, _enumerableCtorWithComparer) = FindComparerConstructionOverload(_enumerableCtor);
                return;
            }

            // move this later in priority order so it doesn't skip comparer options when they exist.
            if (typeof(TEnumerable).TryGetCollectionBuilderAttribute(typeof(TElement), out MethodInfo? builderMethod))
            {
                _spanCtor = builderMethod;
                _constructionStrategy = CollectionConstructionStrategy.Span;
                _constructionComparer = ConstructionWithComparer.None;
                return;
            }

            _constructionStrategy = CollectionConstructionStrategy.None;
            _constructionComparer = ConstructionWithComparer.None;

            [MemberNotNullWhen(true, nameof(_constructionStrategy), nameof(_constructionComparer))]
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
                    Type? factoryType = typeof(TEnumerable).Assembly.GetType("System.Collections.Frozen.FrozenSet");
                    _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => m.Name is "ToFrozenSet")
                        .Where(m =>
                            m.GetParameters() is [ParameterInfo p1, ParameterInfo p2] &&
                            p1.ParameterType.IsIEnumerable() &&
                            p2.ParameterType is { IsGenericType: true } p2Type && p2Type.GetGenericTypeDefinition() == typeof(IEqualityComparer<>))
                        .Select(m => m.MakeGenericMethod(typeof(TElement)))
                        .FirstOrDefault();

                    if (_enumerableCtor != null)
                    {
                        _enumerableCtorWithComparer = _enumerableCtor;
                        _constructionComparer = ConstructionWithComparer.ValuesEqualityComparer;
                        _constructionStrategy = CollectionConstructionStrategy.Enumerable;
                        _isFrozenSet = true;
                        return true;
                    }
                }

                return false;

                [MemberNotNullWhen(true, nameof(_constructionStrategy), nameof(_constructionComparer))]
                bool FindCreateRangeMethods(string typeName, bool? equalityComparer = null)
                {
                    Type? factoryType = typeof(TEnumerable).Assembly.GetType(typeName);

                    // First try for the Span-based factory methods.
                    _constructionStrategy = CollectionConstructionStrategy.Span;
                    _spanCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name is "Create" && m.GetParameters() is [{ ParameterType: { IsGenericType: true, Name: "ReadOnlySpan`1", Namespace: "System", GenericTypeArguments: [Type { IsGenericParameter: true }] } }])
                        ?.MakeGenericMethod(typeof(TElement));
                    (_constructionComparer, _spanCtorWithComparer) = FindComparerConstructionOverload(_spanCtor);

                    if (_spanCtor is not null)
                    {
                        return true;
                    }

                    _constructionStrategy = CollectionConstructionStrategy.Enumerable;
                    _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name is "CreateRange" && m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                        ?.MakeGenericMethod(typeof(TElement));
                    (_constructionComparer, _enumerableCtorWithComparer) = FindComparerConstructionOverload(_enumerableCtor);

                    return _enumerableCtor is not null;
                }
            }
        }
    }

    private object? GetRelevantComparer(CollectionConstructionOptions<TElement>? collectionConstructionOptions)
        => GetRelevantComparer(collectionConstructionOptions, ComparerOptions);
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
    public override CollectionComparerOptions ComparerOptions => CollectionComparerOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<TElement[], IEnumerable<TElement>> GetGetEnumerable() => static array => array;
    public override SpanConstructor<TElement, TElement[]> GetSpanConstructor(CollectionConstructionOptions<TElement>? collectionConstructionOptions = null) => static span => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MultiDimensionalArrayTypeShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider, int rank)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
    where TEnumerable : IEnumerable
{
    public override CollectionComparerOptions ComparerOptions => CollectionComparerOptions.None;
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
    public override CollectionComparerOptions ComparerOptions => CollectionComparerOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<ReadOnlyMemory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable(memory);
    public override SpanConstructor<TElement, ReadOnlyMemory<TElement>> GetSpanConstructor(CollectionConstructionOptions<TElement>? collectionConstructionOptions = null) => static span => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MemoryTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<Memory<TElement>, TElement>(provider)
{
    public override CollectionComparerOptions ComparerOptions => CollectionComparerOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<Memory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable((ReadOnlyMemory<TElement>)memory);
    public override SpanConstructor<TElement, Memory<TElement>> GetSpanConstructor(CollectionConstructionOptions<TElement>? collectionConstructionOptions = null) => static span => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionAsyncEnumerableShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
{
    public override CollectionComparerOptions ComparerOptions => CollectionComparerOptions.None;
    public override bool IsAsyncEnumerable => true;
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable() =>
        static _ => throw new InvalidOperationException("Sync enumeration of IAsyncEnumerable instances is not supported.");
}

internal delegate TDeclaringType SpanECConstructor<TElement, TKey, TDeclaringType>(ReadOnlySpan<TElement> values, IEqualityComparer<TKey> comparer);
internal delegate TDeclaringType SpanCConstructor<TElement, TKey, TDeclaringType>(ReadOnlySpan<TElement> values, IComparer<TKey> comparer);
internal delegate TDeclaringType ECSpanConstructor<TElement, TKey, TDeclaringType>(IEqualityComparer<TKey> comparer, ReadOnlySpan<TElement> values);
internal delegate TDeclaringType CSpanConstructor<TElement, TKey, TDeclaringType>(IComparer<TKey> comparer, ReadOnlySpan<TElement> values);
