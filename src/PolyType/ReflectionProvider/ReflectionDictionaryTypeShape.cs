using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using PolyType.Abstractions;
using PolyType.SourceGenModel;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TDictionary>(provider), IDictionaryTypeShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    private readonly object _syncObject = new();
    private CollectionConstructionStrategy? _constructionStrategy;
    private ConstructorInfo? _mutableCtor;
    private MethodInfo? _addMethod;
    private MethodBase? _enumerableCtor;
    private MethodBase? _spanCtor;
    private ConstructorInfo? _dictionaryCtor;
    private bool _isFrozenDictionary;
    private bool _isFSharpMap;
    private ConstructionSignature? _constructionComparer;
    private bool _discoveryComplete;

    private Setter<TDictionary, KeyValuePair<TKey, TValue>>? _addDelegate;
    private MutableCollectionConstructor<TKey, TDictionary>? _mutableCtorDelegate;
    private Func<IEqualityComparer<TKey>, TDictionary>? _defaultCtorWithEqualityComparerDelegate;
    private Func<IComparer<TKey>, TDictionary>? _defaultCtorWithComparerDelegate;
    private EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? _enumerableCtorDelegate;
    private SpanConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? _spanCtorDelegate;

    private Func<IEnumerable<KeyValuePair<TKey, TValue>>, IEqualityComparer<TKey>, TDictionary>? _enumerableCtorValuesEqualityComparerDelegate;
    private Func<IEqualityComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>? _enumerableCtorEqualityComparerValuesDelegate;
    private Func<IEnumerable<KeyValuePair<TKey, TValue>>, IComparer<TKey>, TDictionary>? _enumerableCtorValuesComparerDelegate;
    private Func<IComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>? _enumerableCtorComparerValuesDelegate;
    private Func<object, SpanConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>>? _spanCtorDelegateFromComparerDelegate;

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

    public virtual ConstructionSignature ConstructorSignature
    {
        get
        {
            DetermineConstructionStrategy();
            return _constructionComparer.Value;
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
        if (_addDelegate is null)
        {
            lock (_syncObject)
            {
                return _addDelegate ??= Provider.MemberAccessor.CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(_addMethod);
            }
        }

        return _addDelegate;
    }

    public MutableCollectionConstructor<TKey, TDictionary> GetMutableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        DebugExt.Assert(_mutableCtor != null);

        return _mutableCtorDelegate ?? CreateMutableCtor();

        MutableCollectionConstructor<TKey, TDictionary> CreateMutableCtor()
        {
            MutableCollectionConstructor<TKey, TDictionary> mutableCtorDelegate;
            switch (ConstructorSignature)
            {
                case ConstructionSignature.None:
                    Func<TDictionary> ctor = CreateConstructorDelegate(_mutableCtor);
                    mutableCtorDelegate = (in CollectionConstructionOptions<TKey>? options) => ctor();
                    break;
                case ConstructionSignature.EqualityComparer:
                    Func<IEqualityComparer<TKey>?, TDictionary> ctor2 = CreateConstructorDelegate<IEqualityComparer<TKey>?>(_mutableCtor);
                    mutableCtorDelegate = (in CollectionConstructionOptions<TKey>? options) => ctor2(options?.EqualityComparer);
                    break;
                case ConstructionSignature.Comparer:
                    Func<IComparer<TKey>?, TDictionary> ctor3 = CreateConstructorDelegate<IComparer<TKey>?>(_mutableCtor);
                    mutableCtorDelegate = (in CollectionConstructionOptions<TKey>? options) => ctor3(options?.Comparer);
                    break;
                default:
                    throw new NotSupportedException($"{ConstructorSignature} is not supported for mutable constructors.");
            }

            return Interlocked.CompareExchange(ref _mutableCtorDelegate, mutableCtorDelegate, null) ?? mutableCtorDelegate;
        }
    }

    public EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> GetEnumerableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support enumerable constructors.");
        }

        DebugExt.Assert(_enumerableCtor != null);

        return _enumerableCtorDelegate ?? CreateEnumerableCtor();

        EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> CreateEnumerableCtor()
        {
            EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> enumerableCtorDelegate;

            if (_isFSharpMap)
            {
                var mapOfSeqDelegate = ((MethodInfo)_enumerableCtor).CreateDelegate<Func<IEnumerable<Tuple<TKey, TValue>>, TDictionary>>();
                enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => mapOfSeqDelegate(values.Select(kvp => new Tuple<TKey, TValue>(kvp.Key, kvp.Value)));
            }
            else
            {
                switch (ConstructorSignature)
                {
                    case ConstructionSignature.Values:
                        var ctor = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>>(_enumerableCtor);
                        enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => ctor(values);
                        break;
                    case ConstructionSignature.ValuesEqualityComparer:
                        var ctor2 = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, IEqualityComparer<TKey>?>(_enumerableCtor);
                        enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => ctor2(values, options?.EqualityComparer);
                        break;
                    case ConstructionSignature.ValuesComparer:
                        var ctor3 = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, IComparer<TKey>?>(_enumerableCtor);
                        enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => ctor3(values, options?.Comparer);
                        break;
                    case ConstructionSignature.EqualityComparerValues:
                        var ctor4 = CreateConstructorDelegate<IEqualityComparer<TKey>?, IEnumerable<KeyValuePair<TKey, TValue>>>(_enumerableCtor);
                        enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => ctor4(options?.EqualityComparer, values);
                        break;
                    case ConstructionSignature.ComparerValues:
                        var ctor5 = CreateConstructorDelegate<IComparer<TKey>?, IEnumerable<KeyValuePair<TKey, TValue>>>(_enumerableCtor);
                        enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => ctor5(options?.Comparer, values);
                        break;
                    default: throw new NotSupportedException();
                }
            }

            return Interlocked.CompareExchange(ref _enumerableCtorDelegate, enumerableCtorDelegate, null) ?? enumerableCtorDelegate;
        }
    }

    public SpanConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> GetSpanConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        return _spanCtorDelegate ??= CreateSpanConstructor();

        SpanConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> CreateSpanConstructor()
        {
            SpanConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> spanCtorDelegate;
            if (_dictionaryCtor is ConstructorInfo dictCtor)
            {
                switch (ConstructorSignature)
                {
                    // TODO: add ReadOnlyDictionary tests with comparers.
                    case ConstructionSignature.Values:
                        var dictCtorDelegate = Provider.MemberAccessor.CreateFuncDelegate<Dictionary<TKey, TValue>, TDictionary>(dictCtor);
                        spanCtorDelegate = (ReadOnlySpan<KeyValuePair<TKey, TValue>> span, in CollectionConstructionOptions<TKey>? options) => dictCtorDelegate(CollectionHelpers.CreateDictionary(span, options?.EqualityComparer));
                        break;
                    default:
                        throw new NotSupportedException($"{ConstructorSignature} is not supported for span constructors.");
                }
            }
            else
            {
                DebugExt.Assert(_spanCtor is not null);
                switch (ConstructorSignature)
                {
                    case ConstructionSignature.Values:
                        var ctor = CreateSpanConstructorDelegate<KeyValuePair<TKey, TValue>>(_spanCtor);
                        spanCtorDelegate = (ReadOnlySpan<KeyValuePair<TKey, TValue>> span, in CollectionConstructionOptions<TKey>? options) => ctor(span);
                        break;
                    case ConstructionSignature.ValuesEqualityComparer:
                        var ctor2 = CreateSpanECConstructorDelegate<TKey, KeyValuePair<TKey, TValue>>(_spanCtor);
                        spanCtorDelegate = (ReadOnlySpan<KeyValuePair<TKey, TValue>> span, in CollectionConstructionOptions<TKey>? options) => ctor2(span, options?.EqualityComparer);
                        break;
                    default:
                        throw new NotSupportedException($"{ConstructorSignature} is not supported for span constructors.");
                }
            }

            return Interlocked.CompareExchange(ref _spanCtorDelegate, spanCtorDelegate, null) ?? spanCtorDelegate;
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
            //if (Type == typeof(ReadOnlyDictionary<int, int>)) Debugger.Launch();
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
                    _addMethod = addMethod;
                    (_constructionComparer, _mutableCtor) = FindComparerConstructorOverload(ConstructionSignature.None, defaultCtor);
                    _constructionStrategy = CollectionConstructionStrategy.Mutable;
                    return;
                }
            }

            if (Provider.Options.UseReflectionEmit && dictionaryType.GetConstructor([typeof(ReadOnlySpan<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo spanCtor)
            {
                // Cannot invoke constructors with ROS parameters without Ref.Emit
                (_constructionComparer, _spanCtor) = FindComparerConstructorOverload(ConstructionSignature.Values, spanCtor);
                _constructionStrategy = CollectionConstructionStrategy.Span;
                return;
            }

            if (dictionaryType.GetConstructor([typeof(IEnumerable<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo enumerableCtor)
            {
                (_constructionComparer, _enumerableCtor) = FindComparerConstructorOverload(ConstructionSignature.Values, enumerableCtor);
                _constructionStrategy = CollectionConstructionStrategy.Enumerable;
                return;
            }

            if (dictionaryType.GetConstructors()
                .FirstOrDefault(ctor => ctor.GetParameters() is [{ ParameterType: Type { IsGenericType: true } paramTy }] && paramTy.IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
                is ConstructorInfo dictionaryCtor)
            {
                // Handle types with ctors accepting IDictionary or IReadOnlyDictionary such as ReadOnlyDictionary<TKey, TValue>
                (_constructionComparer, _dictionaryCtor) = FindComparerConstructorOverload(ConstructionSignature.Values, dictionaryCtor);
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

                (_constructionComparer, _enumerableCtor) = FindComparerConstructionOverload(ConstructionSignature.Values, _enumerableCtor);
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

                (_constructionComparer, _enumerableCtor) = FindComparerConstructionOverload(ConstructionSignature.Values, _enumerableCtor);
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
                    _constructionComparer = ConstructionSignature.ValuesEqualityComparer;
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
                (_constructionComparer, _enumerableCtor) = FindComparerConstructionOverload(ConstructionSignature.Values, _enumerableCtor);
                _constructionStrategy = _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
                return;
            }

            _constructionComparer = ConstructionSignature.None;
            _constructionStrategy = CollectionConstructionStrategy.None;
            return;

            (ConstructionSignature, ConstructorInfo?) FindComparerConstructorOverload(ConstructionSignature nonComparerSignature, ConstructorInfo? nonComparerOverload)
            {
                var (comparer, overload) = FindComparerConstructionOverload(nonComparerSignature, nonComparerOverload);
                return (comparer, (ConstructorInfo?)overload ?? nonComparerOverload);
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
