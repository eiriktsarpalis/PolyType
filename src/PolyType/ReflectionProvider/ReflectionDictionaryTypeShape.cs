using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TDictionary>(provider), IDictionaryTypeShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    private readonly object _syncObject = new();
    private CollectionConstructionStrategy? _constructionStrategy;
    private ConstructorInfo? _defaultCtor;
    private ConstructorInfo? _defaultCtorWithComparer;
    private MethodInfo? _addMethod;
    private MethodBase? _enumerableCtor;
    private MethodBase? _enumerableCtorWithComparer;
    private MethodBase? _spanCtor;
    private MethodBase? _spanCtorWithComparer;
    private ConstructorInfo? _dictionaryCtor;
    private ConstructorInfo? _dictionaryCtorWithComparer;
    private bool _isFrozenDictionary;
    private bool _isFSharpMap;
    private ConstructionWithComparer? _constructionComparer;
    private bool _discoveryComplete;

    private Setter<TDictionary, KeyValuePair<TKey, TValue>>? _addDelegate;
    private Func<TDictionary>? _defaultCtorDelegate;
    private Func<IEqualityComparer<TKey>, TDictionary>? _defaultCtorWithEqualityComparerDelegate;
    private Func<IComparer<TKey>, TDictionary>? _defaultCtorWithComparerDelegate;
    private Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>? _enumerableCtorDelegate;
    private SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>? _spanCtorDelegate;

    private Func<IEnumerable<KeyValuePair<TKey, TValue>>, IEqualityComparer<TKey>, TDictionary>? _enumerableCtorValuesEqualityComparerDelegate;
    private Func<IEqualityComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>? _enumerableCtorEqualityComparerValuesDelegate;
    private Func<IEnumerable<KeyValuePair<TKey, TValue>>, IComparer<TKey>, TDictionary>? _enumerableCtorValuesComparerDelegate;
    private Func<IComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>? _enumerableCtorComparerValuesDelegate;
    private Func<object, SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>>? _spanCtorDelegateFromComparerDelegate;

    private Func<Dictionary<TKey, TValue>, IEqualityComparer<TKey>, TDictionary>? _spanCtorEqualityComparer;
    private Func<SortedDictionary<TKey, TValue>, IComparer<TKey>, TDictionary>? _spanCtorComparer;

    public sealed override TypeShapeKind Kind => TypeShapeKind.Dictionary;
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitDictionary(this, state);

    public CollectionComparerOptions ComparerOptions
    {
        get
        {
            if (_constructionComparer is null)
            {
                DetermineConstructionStrategy();
            }

            return ToComparerConstruction(_constructionComparer.Value);
        }
    }

    public CollectionConstructionStrategy ConstructionStrategy
    {
        get
        {
            if (_constructionStrategy is null)
            {
                DetermineConstructionStrategy();
            }

            return _constructionStrategy.Value;
        }
    }

    public ITypeShape<TKey> KeyType => Provider.GetShape<TKey>();
    public ITypeShape<TValue> ValueType => Provider.GetShape<TValue>();
    ITypeShape IDictionaryTypeShape.KeyType => KeyType;
    ITypeShape IDictionaryTypeShape.ValueType => ValueType;

    public abstract Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary();

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        DebugExt.Assert(_addMethod != null);
        return _addDelegate ??= Provider.MemberAccessor.CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(_addMethod);
    }

    public Func<TDictionary> GetDefaultConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        // We'll use a shared delegate when no comparer applies.
        object? relevantComparer = GetRelevantComparer(collectionConstructionOptions);
        if (relevantComparer is null || _defaultCtorWithComparer is null)
        {
            DebugExt.Assert(_defaultCtor != null);

            return _defaultCtorDelegate ??= CreateDefaultCtor();
            Func<TDictionary> CreateDefaultCtor()
            {
                return Provider.MemberAccessor.CreateDefaultConstructor<TDictionary>(new MethodConstructorShapeInfo(typeof(TDictionary), _defaultCtor, parameters: []));
            }
        }
        else
        {
            switch (relevantComparer)
            {
                case IEqualityComparer<TKey> ec:
                    _defaultCtorWithEqualityComparerDelegate ??= Provider.MemberAccessor.CreateFuncDelegate<IEqualityComparer<TKey>, TDictionary>(_defaultCtorWithComparer);
                    return () => _defaultCtorWithEqualityComparerDelegate(ec);
                case IComparer<TKey> c:
                    _defaultCtorWithComparerDelegate ??= Provider.MemberAccessor.CreateFuncDelegate<IComparer<TKey>, TDictionary>(_defaultCtorWithComparer);
                    return () => _defaultCtorWithComparerDelegate(c);
                default:
                    throw new NotSupportedException();
            }
        }
    }

    public Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> GetEnumerableConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support enumerable constructors.");
        }

        DebugExt.Assert(_enumerableCtor != null);

        // We'll use a shared delegate when no comparer applies.
        object? relevantComparer = GetRelevantComparer(collectionConstructionOptions);
        if (relevantComparer is null || _enumerableCtorWithComparer is null)
        {
            return _enumerableCtorDelegate ??= CreateEnumerableCtor();
            Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> CreateEnumerableCtor()
            {
                if (_isFSharpMap)
                {
                    var mapOfSeqDelegate = ((MethodInfo)_enumerableCtor).CreateDelegate<Func<IEnumerable<Tuple<TKey, TValue>>, TDictionary>>();
                    return kvps => mapOfSeqDelegate(kvps.Select(kvp => new Tuple<TKey, TValue>(kvp.Key, kvp.Value)));
                }

                if (_isFrozenDictionary)
                {
                    // FrozenDictionary only exposes a constructor accepting an IEqualityComparer<TKey> as a second parameter.
                    Debug.Assert(_enumerableCtorWithComparer is MethodInfo);
                    var ctorWithComparer = ((MethodInfo)_enumerableCtorWithComparer!).CreateDelegate<Func<IEnumerable<KeyValuePair<TKey, TValue>>, IEqualityComparer<TKey>?, TDictionary>>();
                    return kvps => ctorWithComparer(kvps, null);
                }

                return _enumerableCtor switch
                {
                    ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>(ctorInfo),
                    _ => ((MethodInfo)_enumerableCtor).CreateDelegate<Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>>(),
                };
            }
        }
        else
        {
            switch (_constructionComparer)
            {
                case ConstructionWithComparer.ValuesEqualityComparer:
                    _enumerableCtorValuesEqualityComparerDelegate ??= _enumerableCtorWithComparer switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, IEqualityComparer<TKey>, TDictionary>(ctorInfo),
                        MethodInfo methodInfo => methodInfo.CreateDelegate<Func<IEnumerable<KeyValuePair<TKey, TValue>>, IEqualityComparer<TKey>, TDictionary>>(),
                        _ => throw new NotSupportedException(),
                    };

                    return values => _enumerableCtorValuesEqualityComparerDelegate(values, (IEqualityComparer<TKey>)relevantComparer);
                case ConstructionWithComparer.EqualityComparerValues:
                    _enumerableCtorEqualityComparerValuesDelegate ??= _enumerableCtorWithComparer switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEqualityComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>(ctorInfo),
                        MethodInfo methodInfo => methodInfo.CreateDelegate<Func<IEqualityComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>>(),
                        _ => throw new NotSupportedException(),
                    };

                    return values => _enumerableCtorEqualityComparerValuesDelegate((IEqualityComparer<TKey>)relevantComparer, values);
                case ConstructionWithComparer.ValuesComparer:
                    _enumerableCtorValuesComparerDelegate ??= _enumerableCtorWithComparer switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, IComparer<TKey>, TDictionary>(ctorInfo),
                        MethodInfo methodInfo => methodInfo.CreateDelegate<Func<IEnumerable<KeyValuePair<TKey, TValue>>, IComparer<TKey>, TDictionary>>(),
                        _ => throw new NotSupportedException(),
                    };

                    return values => _enumerableCtorValuesComparerDelegate(values, (IComparer<TKey>)relevantComparer);
                case ConstructionWithComparer.ComparerValues:
                    _enumerableCtorComparerValuesDelegate ??= _enumerableCtorWithComparer switch
                    {
                        ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>(ctorInfo),
                        MethodInfo methodInfo => methodInfo.CreateDelegate<Func<IComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>>(),
                        _ => throw new NotSupportedException(),
                    };

                    return values => _enumerableCtorComparerValuesDelegate((IComparer<TKey>)relevantComparer, values);
                default:
                    throw new NotSupportedException();
            }
        }
    }

    public SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> GetSpanConstructor(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        // We'll use a shared delegate when no comparer applies.
        object? relevantComparer = GetRelevantComparer(collectionConstructionOptions);
        if (relevantComparer is null || _spanCtorWithComparer is null)
        {
            return _spanCtorDelegate ??= CreateSpanConstructor();
            SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> CreateSpanConstructor()
            {
                if (_dictionaryCtor is ConstructorInfo dictionaryCtor)
                {
                    var dictionaryCtorDelegate = Provider.MemberAccessor.CreateFuncDelegate<Dictionary<TKey, TValue>, TDictionary>(dictionaryCtor);
                    return span => dictionaryCtorDelegate(CollectionHelpers.CreateDictionary(span, null));
                }

                return _spanCtor switch
                {
                    ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateSpanConstructorDelegate<KeyValuePair<TKey, TValue>, TDictionary>(ctorInfo),
                    MethodInfo methodInfo => methodInfo.CreateDelegate<SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>>(),
                    _ => throw new NotSupportedException(),
                };
            }
        }
        else
        {
            if (_dictionaryCtorWithComparer is ConstructorInfo dictionaryCtorWithComparer)
            {
                switch (relevantComparer)
                {
                    case IEqualityComparer<TKey> equalityComparer:
                        _spanCtorEqualityComparer ??= Provider.MemberAccessor.CreateFuncDelegate<Dictionary<TKey, TValue>, IEqualityComparer<TKey>, TDictionary>(dictionaryCtorWithComparer);
                        return span => _spanCtorEqualityComparer(CollectionHelpers.CreateDictionary(span, equalityComparer), equalityComparer);
                    case IComparer<TKey> comparer:
                        _spanCtorComparer ??= Provider.MemberAccessor.CreateFuncDelegate<SortedDictionary<TKey, TValue>, IComparer<TKey>, TDictionary>(dictionaryCtorWithComparer);
                        return span => _spanCtorComparer(CollectionHelpers.CreateSortedDictionary(span, comparer), comparer);
                    default:
                        throw new NotSupportedException();
                }
            }

            DebugExt.Assert(_constructionComparer is not null);
            _spanCtorDelegateFromComparerDelegate ??= _spanCtorWithComparer switch
            {
                ConstructorInfo ctorInfoWithComparer => Provider.MemberAccessor.CreateSpanConstructorDelegate<KeyValuePair<TKey, TValue>, TKey, TDictionary>(ctorInfoWithComparer, _constructionComparer.Value),
                MethodInfo ctorInfoWithComparer => CreateSpanMethodDelegate<KeyValuePair<TKey, TValue>, TKey, TDictionary>(ctorInfoWithComparer, _constructionComparer.Value),
                _ => throw new NotSupportedException(),
            };

            return _spanCtorDelegateFromComparerDelegate(relevantComparer);
        }
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
            // TODO resolve CollectionBuilderAttribute once added for Dictionary types

            Type dictionaryType = typeof(TDictionary);
            if (dictionaryType.IsInterface)
            {
                if (dictionaryType.IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
                {
                    // Handle IDictionary, IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                    dictionaryType = typeof(Dictionary<TKey, TValue>);
                }
                else if (dictionaryType == typeof(IDictionary))
                {
                    // Handle IDictionary using Dictionary<object, object>
                    Debug.Assert(typeof(TKey) == typeof(object) && typeof(TValue) == typeof(object));
                    dictionaryType = typeof(Dictionary<object, object>);
                }
            }

            if (dictionaryType.GetConstructor([]) is ConstructorInfo defaultCtor)
            {
                MethodInfo? addMethod = dictionaryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m =>
                        m.Name is "set_Item" or "Add" &&
                        m.GetParameters() is [ParameterInfo key, ParameterInfo value] &&
                        key.ParameterType == typeof(TKey) && value.ParameterType == typeof(TValue))
                    .OrderByDescending(m => m.Name) // Prefer set_Item over Add
                    .FirstOrDefault();

                if (!typeof(TDictionary).IsValueType)
                {
                    // If no indexer was found, check for potential explicit interface implementations.
                    // Only do so if the type is not a value type, since this would force boxing otherwise.
                    if (addMethod is null && typeof(IDictionary<TKey, TValue>).IsAssignableFrom(dictionaryType))
                    {
                        addMethod = typeof(IDictionary<TKey, TValue>).GetMethod("set_Item", BindingFlags.Public | BindingFlags.Instance);
                    }

                    if (addMethod is null && typeof(IDictionary).IsAssignableFrom(dictionaryType) && typeof(TKey) == typeof(object))
                    {
                        addMethod = typeof(IDictionary).GetMethod("set_Item", BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                if (addMethod != null)
                {
                    _defaultCtor = defaultCtor;
                    _addMethod = addMethod;
                    (_constructionComparer, _defaultCtorWithComparer) = FindComparerConstructorOverload(defaultCtor);
                    _constructionStrategy = CollectionConstructionStrategy.Mutable;
                    return;
                }
            }

            if (Provider.Options.UseReflectionEmit && dictionaryType.GetConstructor([typeof(ReadOnlySpan<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo spanCtor)
            {
                // Cannot invoke constructors with ROS parameters without Ref.Emit
                _spanCtor = spanCtor;
                (_constructionComparer, _spanCtorWithComparer) = FindComparerConstructorOverload(spanCtor);
                _constructionStrategy = CollectionConstructionStrategy.Span;
                return;
            }

            if (dictionaryType.GetConstructor([typeof(IEnumerable<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo enumerableCtor)
            {
                _enumerableCtor = enumerableCtor;
                (_constructionComparer, _enumerableCtorWithComparer) = FindComparerConstructorOverload(enumerableCtor);
                _constructionStrategy = CollectionConstructionStrategy.Enumerable;
                return;
            }

            if (dictionaryType.GetConstructors()
                .FirstOrDefault(ctor => ctor.GetParameters() is [{ ParameterType: Type { IsGenericType: true } paramTy }] && paramTy.IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
                is ConstructorInfo dictionaryCtor)
            {
                // Handle types with ctors accepting IDictionary or IReadOnlyDictionary such as ReadOnlyDictionary<TKey, TValue>
                _dictionaryCtor = dictionaryCtor;
                (_constructionComparer, _dictionaryCtorWithComparer) = FindComparerConstructorOverload(dictionaryCtor);
                _constructionStrategy = CollectionConstructionStrategy.Span;
                return;
            }

            if (dictionaryType is { Name: "ImmutableDictionary`2", Namespace: "System.Collections.Immutable" })
            {
                Type? factoryType = dictionaryType.Assembly.GetType("System.Collections.Immutable.ImmutableDictionary");
                _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name is "CreateRange")
                    .Where(m => m.GetParameters() is [ParameterInfo p1] && p1.ParameterType.IsIEnumerable())
                    .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                    .FirstOrDefault();

                (_constructionComparer, _enumerableCtorWithComparer) = FindComparerConstructionOverload(_enumerableCtor);
                _constructionStrategy = _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
                return;
            }

            if (dictionaryType is { Name: "ImmutableSortedDictionary`2", Namespace: "System.Collections.Immutable" })
            {
                Type? factoryType = dictionaryType.Assembly.GetType("System.Collections.Immutable.ImmutableSortedDictionary");
                _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name is "CreateRange")
                    .Where(m => m.GetParameters() is [ParameterInfo p1] && p1.ParameterType.IsIEnumerable())
                    .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                    .FirstOrDefault();

                (_constructionComparer, _enumerableCtorWithComparer) = FindComparerConstructionOverload(_enumerableCtor);
                _constructionStrategy = _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
                return;
            }

            if (dictionaryType is { Name: "FrozenDictionary`2", Namespace: "System.Collections.Frozen" })
            {
                Type? factoryType = dictionaryType.Assembly.GetType("System.Collections.Frozen.FrozenDictionary");
                _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name is "ToFrozenDictionary")
                    .Where(m =>
                        m.GetParameters() is [ParameterInfo p1, ParameterInfo p2] &&
                        p1.ParameterType.IsIEnumerable() &&
                        p2.ParameterType is { IsGenericType: true } p2Type && p2Type.GetGenericTypeDefinition() == typeof(IEqualityComparer<>))
                    .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                    .FirstOrDefault();

                if (_enumerableCtor != null)
                {
                    _enumerableCtorWithComparer = _enumerableCtor;
                    _constructionComparer = ConstructionWithComparer.ValuesEqualityComparer;
                    _constructionStrategy = CollectionConstructionStrategy.Enumerable;
                    _isFrozenDictionary = true;
                    return;
                }
            }

            if (dictionaryType is { Name: "FSharpMap`2", Namespace: "Microsoft.FSharp.Collections" })
            {
                Type? module = dictionaryType.Assembly.GetType("Microsoft.FSharp.Collections.MapModule");
                _enumerableCtor = module?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name is "OfSeq")
                    .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                    .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                    .FirstOrDefault();

                _isFSharpMap = _enumerableCtor != null;
                (_constructionComparer, _enumerableCtorWithComparer) = FindComparerConstructionOverload(_enumerableCtor);
                _constructionStrategy = _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
                return;
            }

            _constructionComparer = ConstructionWithComparer.None;
            _constructionStrategy = CollectionConstructionStrategy.None;
            return;

            (ConstructionWithComparer, ConstructorInfo?) FindComparerConstructorOverload(ConstructorInfo? nonComparerOverload)
            {
                var (comparer, overload) = FindComparerConstructionOverload(nonComparerOverload);
                return (comparer, (ConstructorInfo?)overload);
            }
        }
    }

    protected override CollectionConstructorParameterType ClassifyConstructorParameter(ParameterInfo parameter)
    {
        if (parameter.ParameterType is { IsGenericType: true, GenericTypeArguments: [Type typeArg] } p && p.GetGenericTypeDefinition() == typeof(IComparer<>) && typeArg == typeof(TKey))
        {
            return CollectionConstructorParameterType.IComparerOfT;
        }
        else if (parameter.ParameterType is { IsGenericType: true, GenericTypeArguments: [Type typeArg2] } p2 && p2.GetGenericTypeDefinition() == typeof(IEqualityComparer<>) && typeArg2 == typeof(TKey))
        {
            return CollectionConstructorParameterType.IEqualityComparerOfT;
        }
        else if (parameter is { ParameterType: { IsGenericType: true, GenericTypeArguments: [Type { IsGenericType: true, GenericTypeArguments: [Type k, Type v] } kvTypeArg] } }
            && kvTypeArg.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
            && k == KeyType.Type
            && v == ValueType.Type)
        {
            return CollectionConstructorParameterType.CollectionOfT;
        }

        return CollectionConstructorParameterType.Unrecognized;
    }

    private object? GetRelevantComparer(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
        => GetRelevantComparer(collectionConstructionOptions, ComparerOptions);
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionDictionaryOfTShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(provider)
    where TDictionary : IDictionary<TKey, TValue>
    where TKey : notnull
{
    public override Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => CollectionHelpers.AsReadOnlyDictionary<TDictionary, TKey, TValue>(dict);
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionReadOnlyDictionaryShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(provider)
    where TDictionary : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    public override Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => dict;
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionNonGenericDictionaryShape<TDictionary>(ReflectionTypeShapeProvider provider) : ReflectionDictionaryTypeShape<TDictionary, object, object?>(provider)
    where TDictionary : IDictionary
{
    public override Func<TDictionary, IReadOnlyDictionary<object, object?>> GetGetDictionary()
    {
        return static obj => CollectionHelpers.AsReadOnlyDictionary(obj);
    }
}
